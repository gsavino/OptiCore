using OptiCore.BranchAndBound.Strategies;

namespace OptiCore.BranchAndBound;

/// <summary>
/// Configuration options for the Branch & Bound solver.
/// </summary>
public class BranchBoundOptions
{
    /// <summary>
    /// Maximum number of nodes to explore (default: 100,000).
    /// </summary>
    public int MaxNodes { get; init; } = 100_000;

    /// <summary>
    /// Maximum time in seconds (default: 3600 = 1 hour).
    /// </summary>
    public double MaxTimeSeconds { get; init; } = 3600;

    /// <summary>
    /// Relative gap tolerance for optimality (default: 1e-4 = 0.01%).
    /// Solution is considered optimal when (UB - LB) / |LB| <= GapTolerance.
    /// </summary>
    public double GapTolerance { get; init; } = 1e-4;

    /// <summary>
    /// Absolute gap tolerance (default: 1e-6).
    /// Used when incumbent is near zero.
    /// </summary>
    public double AbsoluteGapTolerance { get; init; } = 1e-6;

    /// <summary>
    /// Tolerance for considering a value as integer (default: 1e-6).
    /// </summary>
    public double IntegralityTolerance { get; init; } = 1e-6;

    /// <summary>
    /// Number of best solutions to keep in the solution pool (default: 5).
    /// </summary>
    public int SolutionPoolSize { get; init; } = 5;

    /// <summary>
    /// The node selection strategy to use.
    /// </summary>
    public INodeSelectionStrategy? NodeSelectionStrategy { get; init; }

    /// <summary>
    /// The branching variable selection strategy to use.
    /// </summary>
    public IBranchingStrategy? BranchingStrategy { get; init; }

    /// <summary>
    /// Whether to enable cut generation (Branch & Cut).
    /// </summary>
    public bool EnableCuts { get; init; } = false;

    /// <summary>
    /// Maximum number of cut rounds per node (default: 10).
    /// </summary>
    public int MaxCutRoundsPerNode { get; init; } = 10;

    /// <summary>
    /// Minimum improvement in LP bound to continue cut generation (default: 1e-4).
    /// </summary>
    public double MinCutImprovement { get; init; } = 1e-4;

    /// <summary>
    /// Creates a default set of options.
    /// </summary>
    public static BranchBoundOptions Default => new();

    /// <summary>
    /// Creates options optimized for quick solutions (may not be optimal).
    /// </summary>
    public static BranchBoundOptions Quick => new()
    {
        MaxNodes = 10_000,
        MaxTimeSeconds = 60,
        GapTolerance = 0.01 // 1% gap
    };

    /// <summary>
    /// Creates options optimized for proving optimality.
    /// </summary>
    public static BranchBoundOptions Optimal => new()
    {
        MaxNodes = 1_000_000,
        MaxTimeSeconds = 7200,
        GapTolerance = 1e-6,
        EnableCuts = true
    };
}
