using System.Diagnostics;
using OptiCore.BranchAndBound.Strategies;
using OptiCore.BranchAndBound.Strategies.Branching;
using OptiCore.BranchAndBound.Strategies.NodeSelection;
using OptiCore.Enums;
using OptiCore.Models;
using OptiCore.Solver;

namespace OptiCore.BranchAndBound;

/// <summary>
/// Branch & Bound solver for Integer and Mixed-Integer Linear Programming.
/// </summary>
public class BranchBoundSolver
{
    private readonly LinearModel _originalModel;
    private readonly IReadOnlyList<IntegerTerm> _integerVariables;
    private readonly BranchBoundOptions _options;
    private readonly INodeSelectionStrategy _nodeSelection;
    private readonly IBranchingStrategy _branchingStrategy;
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
    /// Creates a new Branch & Bound solver.
    /// </summary>
    /// <param name="model">The linear model to solve.</param>
    /// <param name="integerVariables">The list of integer variables (if null, uses model's Variables with VariableType from model type).</param>
    /// <param name="options">Solver options (if null, uses defaults).</param>
    public BranchBoundSolver(LinearModel model, IReadOnlyList<IntegerTerm>? integerVariables = null, BranchBoundOptions? options = null)
    {
        _originalModel = model;
        _options = options ?? BranchBoundOptions.Default;
        _isMaximization = model.Objective.Goal == ObjectiveType.MAX;

        // Set up integer variables
        _integerVariables = integerVariables ?? CreateIntegerVariablesFromModel(model);

        // Set up strategies
        _nodeSelection = _options.NodeSelectionStrategy ?? new BestBoundNodeSelection();
        _branchingStrategy = _options.BranchingStrategy ?? new MostFractionalBranching();
    }

    /// <summary>
    /// Solves the integer programming problem.
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

            // Main B&B loop
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

                // Check if gap tolerance is met
                if (IsGapSatisfied())
                {
                    return CreateOptimalResult();
                }

                // Select next node to process
                var node = _nodeSelection.SelectNode(_openNodes, _solutionPool.IncumbentValue, _isMaximization);
                if (node == null)
                {
                    // Try to get any pending node
                    node = _openNodes.FirstOrDefault(n => n.Status == NodeStatus.Pending);
                    if (node == null)
                        break;
                }

                _openNodes.Remove(node);

                // Process the node
                ProcessNode(node);
            }

            // B&B tree exhausted
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

    private void ProcessNode(BranchNode node)
    {
        _nodesExplored++;
        _maxDepthReached = Math.Max(_maxDepthReached, node.Depth);

        // Build model with branching bounds
        var boundedModel = ApplyVariableBounds(_originalModel, node.VariableBounds);

        // Solve LP relaxation
        ModelResult lpResult;
        try
        {
            var simplex = new OptiCoreSimplex(boundedModel);
            lpResult = simplex.GetOptimalValues();
        }
        catch
        {
            // LP is infeasible
            _nodesFathomedInfeasible++;
            return;
        }

        double lpObjective = lpResult.OptimalResult;

        // Check for infeasibility (simple check - very large/small values indicate issues)
        if (double.IsNaN(lpObjective) || double.IsInfinity(lpObjective))
        {
            _nodesFathomedInfeasible++;
            return;
        }

        // Update node with LP solution
        node = node.WithLpSolution(lpObjective, lpResult.Terms);

        // Check if bound is worse than incumbent (prune)
        if (_solutionPool.ShouldPrune(lpObjective, _options.IntegralityTolerance))
        {
            _nodesPrunedByBound++;
            return;
        }

        // Check integrality of integer variables
        var branchDecision = _branchingStrategy.SelectBranchingVariable(node, _integerVariables, _options.IntegralityTolerance);

        if (branchDecision == null)
        {
            // All integer variables are integral - found an integer solution!
            _integerSolutionsFound++;
            _solutionPool.TryAdd(lpResult, lpObjective);
            return;
        }

        // Branch on the selected variable
        CreateChildNodes(node, branchDecision);
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

    private LinearModel ApplyVariableBounds(LinearModel model, IReadOnlyList<VariableBound> bounds)
    {
        if (bounds.Count == 0)
            return model;

        var newConstraints = new List<Constraint>(model.ConstraintsList);

        foreach (var bound in bounds)
        {
            if (bound.UpperBound.HasValue)
            {
                // x <= ub constraint
                var constraint = new Constraint(
                    ConstraintName: $"_branch_{bound.VariableName}_ub_{bound.UpperBound.Value}",
                    Coefficients: new List<Term> { new Term(bound.VariableName, 1.0) },
                    Operator: "<=",
                    Rhs: bound.UpperBound.Value
                );
                newConstraints.Add(constraint);
            }

            if (bound.LowerBound.HasValue)
            {
                // x >= lb constraint (converted to -x <= -lb)
                var constraint = new Constraint(
                    ConstraintName: $"_branch_{bound.VariableName}_lb_{bound.LowerBound.Value}",
                    Coefficients: new List<Term> { new Term(bound.VariableName, -1.0) },
                    Operator: "<=",
                    Rhs: -bound.LowerBound.Value
                );
                newConstraints.Add(constraint);
            }
        }

        return new LinearModel(
            ModelKind: model.ModelKind,
            Objective: model.Objective,
            ConstraintsList: newConstraints,
            Variables: model.Variables
        );
    }

    private bool IsGapSatisfied()
    {
        if (!_solutionPool.IncumbentValue.HasValue)
            return false;

        if (_openNodes.Count == 0)
            return true;

        // Get the best bound from open nodes
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
        else
        {
            return new BranchBoundResult
            {
                Status = status,
                Statistics = GetStatistics()
            };
        }
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
            SolveTimeSeconds = _stopwatch.Elapsed.TotalSeconds
        };
    }

    private static IReadOnlyList<IntegerTerm> CreateIntegerVariablesFromModel(LinearModel model)
    {
        // For ILP, all variables are integer; for MILP, need explicit specification
        if (model.ModelKind == ModelType.IntegerLinearProgramming)
        {
            return model.Variables.Select(v => IntegerTerm.FromTerm(v, VariableType.Integer)).ToList();
        }

        // For regular LP or MILP without explicit specification, return empty list
        return Array.Empty<IntegerTerm>();
    }
}
