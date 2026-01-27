using OptiCore.Models;

namespace OptiCore.BranchAndBound;

/// <summary>
/// Represents the termination status of the Branch & Bound solver.
/// </summary>
public enum BranchBoundStatus
{
    /// <summary>Optimal integer solution found.</summary>
    Optimal,
    /// <summary>A feasible solution was found but optimality was not proven.</summary>
    Feasible,
    /// <summary>The problem is infeasible (no integer solution exists).</summary>
    Infeasible,
    /// <summary>The LP relaxation is unbounded.</summary>
    Unbounded,
    /// <summary>Node limit was reached before proving optimality.</summary>
    NodeLimitReached,
    /// <summary>Time limit was reached before proving optimality.</summary>
    TimeLimitReached,
    /// <summary>An error occurred during solving.</summary>
    Error
}

/// <summary>
/// Statistics collected during Branch & Bound solving.
/// </summary>
public record BranchBoundStatistics
{
    /// <summary>Total number of nodes explored.</summary>
    public int NodesExplored { get; init; }

    /// <summary>Number of nodes pruned by bound.</summary>
    public int NodesPrunedByBound { get; init; }

    /// <summary>Number of nodes fathomed due to infeasibility.</summary>
    public int NodesFathomedInfeasible { get; init; }

    /// <summary>Number of integer-feasible nodes found.</summary>
    public int IntegerSolutionsFound { get; init; }

    /// <summary>Maximum depth reached in the B&B tree.</summary>
    public int MaxDepthReached { get; init; }

    /// <summary>Number of open nodes remaining when solver terminated.</summary>
    public int OpenNodesRemaining { get; init; }

    /// <summary>Total solving time in seconds.</summary>
    public double SolveTimeSeconds { get; init; }

    /// <summary>Total number of simplex iterations across all nodes.</summary>
    public long TotalSimplexIterations { get; init; }

    /// <summary>Number of cuts generated (if Branch & Cut enabled).</summary>
    public int CutsGenerated { get; init; }

    /// <summary>Number of cuts added to the model.</summary>
    public int CutsAdded { get; init; }

    /// <summary>
    /// Creates default statistics (all zeros).
    /// </summary>
    public static BranchBoundStatistics Empty => new();
}

/// <summary>
/// Represents the result of a Branch & Bound solve.
/// </summary>
public class BranchBoundResult
{
    /// <summary>
    /// The termination status of the solver.
    /// </summary>
    public BranchBoundStatus Status { get; init; }

    /// <summary>
    /// The best integer solution found (null if no feasible solution).
    /// </summary>
    public ModelResult? BestSolution { get; init; }

    /// <summary>
    /// The objective value of the best solution (null if no feasible solution).
    /// </summary>
    public double? ObjectiveValue { get; init; }

    /// <summary>
    /// The best known bound (dual bound).
    /// For maximization: upper bound on optimal value.
    /// For minimization: lower bound on optimal value.
    /// </summary>
    public double? BestBound { get; init; }

    /// <summary>
    /// The optimality gap as a fraction: (UB - LB) / |LB|.
    /// </summary>
    public double? Gap => CalculateGap();

    /// <summary>
    /// The optimality gap as a percentage.
    /// </summary>
    public double? GapPercent => Gap * 100;

    /// <summary>
    /// Statistics from the solve process.
    /// </summary>
    public BranchBoundStatistics Statistics { get; init; } = BranchBoundStatistics.Empty;

    /// <summary>
    /// Pool of best solutions found (up to pool size limit).
    /// </summary>
    public IReadOnlyList<ModelResult> SolutionPool { get; init; } = Array.Empty<ModelResult>();

    /// <summary>
    /// Error message if Status is Error.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Whether the problem was a maximization problem.
    /// </summary>
    public bool IsMaximization { get; init; }

    private double? CalculateGap()
    {
        if (!ObjectiveValue.HasValue || !BestBound.HasValue)
            return null;

        double incumbent = ObjectiveValue.Value;
        double bound = BestBound.Value;

        if (Math.Abs(incumbent) < 1e-10)
            return Math.Abs(bound - incumbent);

        return Math.Abs(bound - incumbent) / Math.Abs(incumbent);
    }

    /// <summary>
    /// Creates a result indicating optimality.
    /// </summary>
    public static BranchBoundResult CreateOptimal(ModelResult solution, double objectiveValue,
        bool isMaximization, BranchBoundStatistics statistics, IReadOnlyList<ModelResult>? solutionPool = null)
    {
        return new BranchBoundResult
        {
            Status = BranchBoundStatus.Optimal,
            BestSolution = solution,
            ObjectiveValue = objectiveValue,
            BestBound = objectiveValue,
            IsMaximization = isMaximization,
            Statistics = statistics,
            SolutionPool = solutionPool ?? new[] { solution }
        };
    }

    /// <summary>
    /// Creates a result indicating a feasible but not proven optimal solution.
    /// </summary>
    public static BranchBoundResult CreateFeasible(ModelResult solution, double objectiveValue,
        double bestBound, bool isMaximization, BranchBoundStatistics statistics,
        BranchBoundStatus terminationReason, IReadOnlyList<ModelResult>? solutionPool = null)
    {
        return new BranchBoundResult
        {
            Status = terminationReason,
            BestSolution = solution,
            ObjectiveValue = objectiveValue,
            BestBound = bestBound,
            IsMaximization = isMaximization,
            Statistics = statistics,
            SolutionPool = solutionPool ?? new[] { solution }
        };
    }

    /// <summary>
    /// Creates a result indicating infeasibility.
    /// </summary>
    public static BranchBoundResult CreateInfeasible(BranchBoundStatistics statistics)
    {
        return new BranchBoundResult
        {
            Status = BranchBoundStatus.Infeasible,
            Statistics = statistics
        };
    }

    /// <summary>
    /// Creates a result indicating an error.
    /// </summary>
    public static BranchBoundResult CreateError(string message, BranchBoundStatistics? statistics = null)
    {
        return new BranchBoundResult
        {
            Status = BranchBoundStatus.Error,
            ErrorMessage = message,
            Statistics = statistics ?? BranchBoundStatistics.Empty
        };
    }
}
