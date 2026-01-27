using System.Diagnostics;
using OptiCore.BranchAndBound;
using OptiCore.BranchAndBound.Strategies;
using OptiCore.BranchAndBound.Strategies.Branching;
using OptiCore.BranchAndBound.Strategies.NodeSelection;
using OptiCore.Cuts;
using OptiCore.Cuts.Generators;
using OptiCore.Enums;
using OptiCore.Models;

namespace OptiCore.Solver;

/// <summary>
/// Branch & Cut solver combining Branch & Bound with cutting planes.
/// </summary>
public class BranchAndCutSolver
{
    private readonly LinearModel _originalModel;
    private readonly IReadOnlyList<IntegerTerm> _integerVariables;
    private readonly BranchBoundOptions _options;
    private readonly INodeSelectionStrategy _nodeSelection;
    private readonly IBranchingStrategy _branchingStrategy;
    private readonly CutManager _cutManager;
    private readonly bool _isMaximization;

    private readonly List<BranchNode> _openNodes = new();
    private SolutionPool _solutionPool = null!;
    private int _nextNodeId = 1;
    private int _nodesExplored = 0;
    private int _nodesPrunedByBound = 0;
    private int _nodesFathomedInfeasible = 0;
    private int _integerSolutionsFound = 0;
    private int _maxDepthReached = 0;
    private Stopwatch _stopwatch = null!;

    /// <summary>
    /// Creates a new Branch & Cut solver.
    /// </summary>
    /// <param name="model">The linear model to solve.</param>
    /// <param name="integerVariables">The list of integer variables.</param>
    /// <param name="options">Solver options.</param>
    public BranchAndCutSolver(
        LinearModel model,
        IReadOnlyList<IntegerTerm>? integerVariables = null,
        BranchBoundOptions? options = null)
    {
        _originalModel = model;
        _options = options ?? new BranchBoundOptions { EnableCuts = true };
        _isMaximization = model.Objective.Goal == ObjectiveType.MAX;

        // Set up integer variables
        _integerVariables = integerVariables ?? CreateIntegerVariablesFromModel(model);

        // Set up strategies
        _nodeSelection = _options.NodeSelectionStrategy ?? new BestBoundNodeSelection();
        _branchingStrategy = _options.BranchingStrategy ?? new MostFractionalBranching();

        // Set up cut manager with Gomory generators
        _cutManager = new CutManager();
        if (_options.EnableCuts)
        {
            _cutManager.RegisterGenerator(new GomoryFractionalCutGenerator());
            _cutManager.RegisterGenerator(new GomoryMixedIntegerCutGenerator());
        }
    }

    /// <summary>
    /// Solves the integer programming problem using Branch & Cut.
    /// </summary>
    /// <returns>The result of the solve.</returns>
    public BranchBoundResult Solve()
    {
        _stopwatch = Stopwatch.StartNew();
        _solutionPool = new SolutionPool(_options.SolutionPoolSize, _isMaximization);

        try
        {
            // Initialize with root node
            var rootNode = BranchNode.CreateRoot();
            _openNodes.Add(rootNode);

            // Main B&C loop
            while (_openNodes.Count > 0)
            {
                // Check termination conditions
                if (_nodesExplored >= _options.MaxNodes)
                {
                    return CreateTerminationResult(BranchBoundStatus.NodeLimitReached);
                }

                if (_stopwatch.Elapsed.TotalSeconds >= _options.MaxTimeSeconds)
                {
                    return CreateTerminationResult(BranchBoundStatus.TimeLimitReached);
                }

                if (IsGapSatisfied())
                {
                    return CreateOptimalResult();
                }

                // Select next node
                var node = _nodeSelection.SelectNode(_openNodes, _solutionPool.IncumbentValue, _isMaximization);
                if (node == null)
                {
                    node = _openNodes.FirstOrDefault(n => n.Status == NodeStatus.Pending);
                    if (node == null) break;
                }

                _openNodes.Remove(node);

                // Process the node with cuts
                ProcessNodeWithCuts(node);
            }

            // Tree exhausted
            if (_solutionPool.Incumbent != null)
            {
                return CreateOptimalResult();
            }
            else
            {
                return BranchBoundResult.CreateInfeasible(GetStatistics());
            }
        }
        catch (Exception ex)
        {
            return BranchBoundResult.CreateError(ex.Message, GetStatistics());
        }
    }

    private void ProcessNodeWithCuts(BranchNode node)
    {
        _nodesExplored++;
        _maxDepthReached = Math.Max(_maxDepthReached, node.Depth);

        // Create working model with branch bounds
        var workingModel = new WorkingModel(_originalModel);
        workingModel.ApplyBranchBounds(node.VariableBounds);

        // Solve LP and generate cuts iteratively
        ModelResult? lpResult = null;
        double lpObjective = double.NaN;
        int cutRounds = 0;
        double previousBound = double.NegativeInfinity;

        while (cutRounds < _options.MaxCutRoundsPerNode)
        {
            // Build and solve LP
            var lpModel = workingModel.BuildLinearModel();

            try
            {
                var simplex = new OptiCoreSimplex(lpModel);
                lpResult = simplex.GetOptimalValues();
                lpObjective = lpResult.OptimalResult;
            }
            catch
            {
                _nodesFathomedInfeasible++;
                return;
            }

            if (double.IsNaN(lpObjective) || double.IsInfinity(lpObjective))
            {
                _nodesFathomedInfeasible++;
                return;
            }

            // Check if bound improved enough to continue cut generation
            double improvement = _isMaximization
                ? previousBound - lpObjective
                : lpObjective - previousBound;

            if (cutRounds > 0 && improvement < _options.MinCutImprovement)
            {
                break;
            }

            // Check if should prune
            if (_solutionPool.ShouldPrune(lpObjective, _options.IntegralityTolerance))
            {
                _nodesPrunedByBound++;
                return;
            }

            // Check integrality
            var branchDecision = _branchingStrategy.SelectBranchingVariable(
                node.WithLpSolution(lpObjective, lpResult.Terms),
                _integerVariables,
                _options.IntegralityTolerance);

            if (branchDecision == null)
            {
                // Integer feasible!
                _integerSolutionsFound++;
                _solutionPool.TryAdd(lpResult, lpObjective);
                return;
            }

            // Generate cuts if enabled
            if (!_options.EnableCuts)
                break;

            var cutContext = CreateCutContext(lpModel, lpResult, lpObjective, cutRounds);
            var newCuts = _cutManager.GenerateCuts(cutContext, node.Depth, _nodesExplored);

            if (newCuts.Count == 0)
                break;

            // Add violated cuts to working model
            int added = workingModel.AddCuts(newCuts);
            if (added == 0)
                break;

            previousBound = lpObjective;
            cutRounds++;
        }

        // After cut loop, check for branching
        if (lpResult == null)
            return;

        node = node.WithLpSolution(lpObjective, lpResult.Terms);

        // Final check for integrality
        var finalBranchDecision = _branchingStrategy.SelectBranchingVariable(
            node, _integerVariables, _options.IntegralityTolerance);

        if (finalBranchDecision == null)
        {
            _integerSolutionsFound++;
            _solutionPool.TryAdd(lpResult, lpObjective);
            return;
        }

        // Final prune check
        if (_solutionPool.ShouldPrune(lpObjective, _options.IntegralityTolerance))
        {
            _nodesPrunedByBound++;
            return;
        }

        // Branch
        CreateChildNodes(node, finalBranchDecision);
    }

    private CutGenerationContext CreateCutContext(
        LinearModel model,
        ModelResult lpResult,
        double objectiveValue,
        int round)
    {
        // Build variable names list
        var variableNames = model.Variables.Select(v => v.TermName).ToList();

        // Build integer variable indices set
        var integerIndices = new HashSet<int>();
        for (int i = 0; i < variableNames.Count; i++)
        {
            if (_integerVariables.Any(iv =>
                iv.TermName.Equals(variableNames[i], StringComparison.OrdinalIgnoreCase)))
            {
                integerIndices.Add(i);
            }
        }

        // Get the simplex matrix
        var matrix = model.GetMatrix();

        return new CutGenerationContext(
            simplexMatrix: matrix,
            variableNames: variableNames,
            integerVariableIndices: integerIndices,
            currentSolution: lpResult.Terms,
            numberOfOriginalVariables: model.GetNumberOfVariables(),
            objectiveValue: objectiveValue,
            round: round,
            integralityTolerance: _options.IntegralityTolerance
        );
    }

    private void CreateChildNodes(BranchNode parent, BranchingDecision decision)
    {
        // Down branch: x <= floor(value)
        var downNode = parent.CreateChild(
            _nextNodeId++,
            decision.VariableName,
            lowerBound: null,
            upperBound: decision.FloorValue,
            direction: "down"
        );

        // Up branch: x >= ceil(value)
        var upNode = parent.CreateChild(
            _nextNodeId++,
            decision.VariableName,
            lowerBound: decision.CeilValue,
            upperBound: null,
            direction: "up"
        );

        _openNodes.Add(downNode);
        _openNodes.Add(upNode);
    }

    private bool IsGapSatisfied()
    {
        if (!_solutionPool.IncumbentValue.HasValue)
            return false;

        if (_openNodes.Count == 0)
            return true;

        double? bestBound = GetBestBoundFromOpenNodes();
        if (!bestBound.HasValue)
            return false;

        double incumbent = _solutionPool.IncumbentValue.Value;
        double gap;

        if (Math.Abs(incumbent) < _options.AbsoluteGapTolerance)
        {
            gap = Math.Abs(bestBound.Value - incumbent);
            return gap <= _options.AbsoluteGapTolerance;
        }

        gap = Math.Abs(bestBound.Value - incumbent) / Math.Abs(incumbent);
        return gap <= _options.GapTolerance;
    }

    private double? GetBestBoundFromOpenNodes()
    {
        var nodesWithBounds = _openNodes.Where(n => n.LpBound.HasValue).ToList();
        if (nodesWithBounds.Count == 0)
            return null;

        return _isMaximization
            ? nodesWithBounds.Max(n => n.LpBound!.Value)
            : nodesWithBounds.Min(n => n.LpBound!.Value);
    }

    private BranchBoundResult CreateOptimalResult()
    {
        return BranchBoundResult.CreateOptimal(
            _solutionPool.Incumbent!,
            _solutionPool.IncumbentValue!.Value,
            _isMaximization,
            GetStatistics(),
            _solutionPool.AllSolutions
        );
    }

    private BranchBoundResult CreateTerminationResult(BranchBoundStatus status)
    {
        if (_solutionPool.Incumbent != null)
        {
            return BranchBoundResult.CreateFeasible(
                _solutionPool.Incumbent,
                _solutionPool.IncumbentValue!.Value,
                GetBestBoundFromOpenNodes() ?? _solutionPool.IncumbentValue.Value,
                _isMaximization,
                GetStatistics(),
                status,
                _solutionPool.AllSolutions
            );
        }

        return new BranchBoundResult
        {
            Status = status,
            Statistics = GetStatistics()
        };
    }

    private BranchBoundStatistics GetStatistics()
    {
        return new BranchBoundStatistics
        {
            NodesExplored = _nodesExplored,
            NodesPrunedByBound = _nodesPrunedByBound,
            NodesFathomedInfeasible = _nodesFathomedInfeasible,
            IntegerSolutionsFound = _integerSolutionsFound,
            MaxDepthReached = _maxDepthReached,
            OpenNodesRemaining = _openNodes.Count,
            SolveTimeSeconds = _stopwatch.Elapsed.TotalSeconds,
            CutsGenerated = _cutManager.TotalCutsGenerated,
            CutsAdded = _cutManager.TotalCutsAdded
        };
    }

    private static IReadOnlyList<IntegerTerm> CreateIntegerVariablesFromModel(LinearModel model)
    {
        if (model.ModelKind == ModelType.IntegerLinearProgramming)
        {
            return model.Variables.Select(v => IntegerTerm.FromTerm(v, VariableType.Integer)).ToList();
        }

        return Array.Empty<IntegerTerm>();
    }
}
