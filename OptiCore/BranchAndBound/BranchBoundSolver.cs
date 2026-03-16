// OptiCore v1.2.5 - Branch & Bound solver with binary variable support
using System.Diagnostics;
using OptiCore.BranchAndBound.Strategies;
using OptiCore.BranchAndBound.Strategies.Branching;
using OptiCore.BranchAndBound.Strategies.NodeSelection;
using OptiCore.Enums;
using OptiCore.Models;
using OptiCore.Solver;

namespace OptiCore.BranchAndBound;

/// <summary>
/// Branch &amp; Bound solver for Integer (ILP) and Mixed-Integer Linear Programming (MILP) problems.
/// Works by recursively partitioning the feasible region: at each node, the LP relaxation is solved;
/// if the solution has fractional integer variables, the problem is split into two sub-problems by
/// bounding the fractional variable (x &lt;= floor and x &gt;= ceil). Nodes are pruned when their LP bound
/// cannot improve the best known integer solution (incumbent). Supports configurable node selection
/// and branching strategies.
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
    /// Main solve method. Creates the root node with initial bounds, then iterates the B&amp;B loop:
    /// select node, solve LP relaxation, check integrality, prune or branch. Returns when the tree
    /// is exhausted, gap tolerance is met, or resource limits are hit.
    /// </summary>
    /// <returns>A <see cref="BranchBoundResult"/> containing the best solution found, status, and statistics.</returns>
    public BranchBoundResult Solve()
    {
        _stopwatch = Stopwatch.StartNew();
        _solutionPool = new SolutionPool(_options.SolutionPoolSize, _isMaximization);

        try
        {
            // Initialize with root node that includes initial bounds from integer variables
            var initialBounds = CreateInitialBounds();
            var rootNode = BranchNode.CreateRootWithBounds(initialBounds);
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

    /// <summary>
    /// Processes a single B&amp;B tree node: applies variable bounds to create a bounded LP, solves it
    /// with the simplex method, checks if the solution should be pruned (worse than incumbent),
    /// checks integrality, and either records an integer solution or creates child branches.
    /// </summary>
    /// <param name="node">The node to process.</param>
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

    /// <summary>
    /// Creates two child nodes from a branching decision: a down branch (x &lt;= floor) and
    /// an up branch (x &gt;= ceil), and adds them to the open node list.
    /// </summary>
    /// <param name="parent">The parent node being branched.</param>
    /// <param name="decision">The branching decision specifying variable and bound values.</param>
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

    /// <summary>
    /// Creates a new <see cref="LinearModel"/> with additional constraints enforcing the variable bounds
    /// accumulated along the B&amp;B tree path from root to the current node.
    /// </summary>
    /// <param name="model">The original linear model.</param>
    /// <param name="bounds">The variable bounds to enforce as constraints.</param>
    /// <returns>A new model with bound constraints appended, or the original model if no bounds exist.</returns>
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
                    Coefficients: CreateSingleVariableCoefficients(model.Variables, bound.VariableName, 1.0),
                    Operator: "<=",
                    Rhs: bound.UpperBound.Value
                );
                newConstraints.Add(constraint);
            }

            if (bound.LowerBound.HasValue)
            {
                // x >= lb constraint (now handled natively by simplex)
                var constraint = new Constraint(
                    ConstraintName: $"_branch_{bound.VariableName}_lb_{bound.LowerBound.Value}",
                    Coefficients: CreateSingleVariableCoefficients(model.Variables, bound.VariableName, 1.0),
                    Operator: ">=",
                    Rhs: bound.LowerBound.Value
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

    /// <summary>
    /// Helper to build a coefficient list for a bound constraint where only one variable has a
    /// non-zero coefficient. All other variables receive a coefficient of zero, as required by
    /// the constraint model.
    /// </summary>
    /// <param name="variables">The full list of variables in the model.</param>
    /// <param name="targetVariable">The variable to assign the non-zero coefficient to.</param>
    /// <param name="coefficient">The coefficient value for the target variable.</param>
    /// <returns>A list of <see cref="Term"/> instances with only the target variable having a non-zero coefficient.</returns>
    private static List<Term> CreateSingleVariableCoefficients(IReadOnlyList<Term> variables, string targetVariable, double coefficient)
    {
        var coefficients = new List<Term>();
        foreach (var v in variables)
        {
            if (v.TermName.Equals(targetVariable, StringComparison.OrdinalIgnoreCase))
            {
                coefficients.Add(new Term(v.TermName, coefficient));
            }
            else
            {
                coefficients.Add(new Term(v.TermName, 0.0));
            }
        }
        return coefficients;
    }

    /// <summary>
    /// Checks whether the relative or absolute optimality gap between the incumbent and the best
    /// open-node bound is within the configured tolerance.
    /// </summary>
    /// <returns><c>true</c> if the gap is satisfied or no open nodes remain; otherwise <c>false</c>.</returns>
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

    /// <summary>
    /// Returns the best (tightest) LP relaxation bound among all open nodes.
    /// For maximization returns the maximum; for minimization returns the minimum.
    /// </summary>
    /// <returns>The best bound, or <c>null</c> if no open nodes have been solved.</returns>
    private double? GetBestBoundFromOpenNodes()
    {
        var nodesWithBounds = _openNodes.Where(n => n.LpBound.HasValue).ToList();
        if (nodesWithBounds.Count == 0)
            return null;

        return _isMaximization
            ? nodesWithBounds.Max(n => n.LpBound!.Value)
            : nodesWithBounds.Min(n => n.LpBound!.Value);
    }

    /// <summary>
    /// Creates a <see cref="BranchBoundResult"/> with <see cref="BranchBoundStatus.Optimal"/> status
    /// using the current incumbent solution and statistics.
    /// </summary>
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

    /// <summary>
    /// Creates a <see cref="BranchBoundResult"/> for early termination (node limit or time limit).
    /// If an incumbent exists, returns a feasible result with the current gap; otherwise returns
    /// a result with only statistics.
    /// </summary>
    /// <param name="status">The termination reason.</param>
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

    /// <summary>
    /// Collects and returns the current solving statistics.
    /// </summary>
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

    /// <summary>
    /// Infers integer variable definitions from the model type. For ILP models, all variables are
    /// treated as integer. For other model types, returns an empty list.
    /// </summary>
    /// <param name="model">The linear model.</param>
    /// <returns>A list of <see cref="IntegerTerm"/> definitions.</returns>
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

    /// <summary>
    /// Sets up initial variable bounds from integer variable definitions, ensuring binary variables
    /// start with 0 &lt;= x &lt;= 1. For general integer variables, applies any explicit lower/upper
    /// bounds provided in the <see cref="IntegerTerm"/> definitions.
    /// </summary>
    /// <returns>A list of <see cref="VariableBound"/> constraints for the root node.</returns>
    private List<VariableBound> CreateInitialBounds()
    {
        var bounds = new List<VariableBound>();

        foreach (var intVar in _integerVariables)
        {
            double? lowerBound = null;
            double? upperBound = null;

            if (intVar.Type == VariableType.Binary)
            {
                // Binary variables must be between 0 and 1
                lowerBound = 0.0;
                upperBound = 1.0;
            }
            else if (intVar.Type == VariableType.Integer)
            {
                // Use explicit bounds if provided
                if (intVar.LowerBound.HasValue)
                    lowerBound = intVar.LowerBound.Value;
                if (intVar.UpperBound.HasValue)
                    upperBound = intVar.UpperBound.Value;
            }

            // Only add if there are actual bounds to enforce
            if (lowerBound.HasValue || upperBound.HasValue)
            {
                bounds.Add(new VariableBound(intVar.TermName, lowerBound, upperBound));
            }
        }

        return bounds;
    }
}
