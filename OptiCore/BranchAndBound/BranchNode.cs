using OptiCore.Models;

namespace OptiCore.BranchAndBound;

/// <summary>
/// Represents the status of a node in the Branch & Bound tree.
/// </summary>
public enum NodeStatus
{
    /// <summary>Node is waiting to be processed.</summary>
    Pending,
    /// <summary>Node is currently being processed.</summary>
    Processing,
    /// <summary>Node was pruned (bound worse than incumbent).</summary>
    Pruned,
    /// <summary>Node was branched into child nodes.</summary>
    Branched,
    /// <summary>Node has an integer-feasible solution.</summary>
    Integral,
    /// <summary>Node's LP relaxation is infeasible.</summary>
    Fathomed
}

/// <summary>
/// Represents a variable bound constraint created during branching.
/// </summary>
/// <param name="VariableName">The name of the variable being bounded.</param>
/// <param name="LowerBound">The lower bound (null if unchanged).</param>
/// <param name="UpperBound">The upper bound (null if unchanged).</param>
public record VariableBound(
    string VariableName,
    double? LowerBound,
    double? UpperBound
);

/// <summary>
/// Represents an immutable node in the Branch & Bound tree.
/// </summary>
/// <param name="NodeId">Unique identifier for this node.</param>
/// <param name="ParentId">The ID of the parent node (null for root).</param>
/// <param name="Depth">The depth in the B&B tree (root = 0).</param>
/// <param name="VariableBounds">List of variable bounds applied at this node.</param>
/// <param name="LpBound">The LP relaxation bound at this node.</param>
/// <param name="LpSolution">The LP relaxation solution values.</param>
/// <param name="Status">The current status of this node.</param>
/// <param name="BranchVariableName">The variable that was branched on to create this node.</param>
/// <param name="BranchDirection">The branching direction ("down" for <= floor, "up" for >= ceil).</param>
public record BranchNode(
    int NodeId,
    int? ParentId,
    int Depth,
    IReadOnlyList<VariableBound> VariableBounds,
    double? LpBound = null,
    IReadOnlyList<Term>? LpSolution = null,
    NodeStatus Status = NodeStatus.Pending,
    string? BranchVariableName = null,
    string? BranchDirection = null
)
{
    /// <summary>
    /// Creates the root node with no variable bounds.
    /// </summary>
    public static BranchNode CreateRoot()
    {
        return new BranchNode(
            NodeId: 0,
            ParentId: null,
            Depth: 0,
            VariableBounds: Array.Empty<VariableBound>()
        );
    }

    /// <summary>
    /// Creates a child node by adding a new variable bound.
    /// </summary>
    /// <param name="childNodeId">The ID for the new child node.</param>
    /// <param name="variableName">The variable being bounded.</param>
    /// <param name="lowerBound">The new lower bound (null if unchanged).</param>
    /// <param name="upperBound">The new upper bound (null if unchanged).</param>
    /// <param name="direction">The branching direction.</param>
    /// <returns>A new child BranchNode.</returns>
    public BranchNode CreateChild(int childNodeId, string variableName,
        double? lowerBound, double? upperBound, string direction)
    {
        var newBound = new VariableBound(variableName, lowerBound, upperBound);
        var newBounds = VariableBounds.Append(newBound).ToList();

        return new BranchNode(
            NodeId: childNodeId,
            ParentId: NodeId,
            Depth: Depth + 1,
            VariableBounds: newBounds,
            Status: NodeStatus.Pending,
            BranchVariableName: variableName,
            BranchDirection: direction
        );
    }

    /// <summary>
    /// Creates a copy of this node with an updated LP solution.
    /// </summary>
    public BranchNode WithLpSolution(double lpBound, IReadOnlyList<Term> solution)
    {
        return this with { LpBound = lpBound, LpSolution = solution };
    }

    /// <summary>
    /// Creates a copy of this node with an updated status.
    /// </summary>
    public BranchNode WithStatus(NodeStatus newStatus)
    {
        return this with { Status = newStatus };
    }

    /// <summary>
    /// Gets the value of a variable from the LP solution.
    /// </summary>
    /// <param name="variableName">The variable name.</param>
    /// <returns>The value, or null if not found.</returns>
    public double? GetVariableValue(string variableName)
    {
        return LpSolution?.FirstOrDefault(t =>
            t.TermName.Equals(variableName, StringComparison.OrdinalIgnoreCase))?.Coefficient;
    }

    /// <summary>
    /// Gets all variable bounds for a specific variable accumulated from root to this node.
    /// </summary>
    /// <param name="variableName">The variable name.</param>
    /// <returns>Tuple of (effectiveLowerBound, effectiveUpperBound).</returns>
    public (double lower, double upper) GetEffectiveBounds(string variableName)
    {
        double lower = double.NegativeInfinity;
        double upper = double.PositiveInfinity;

        foreach (var bound in VariableBounds.Where(b =>
            b.VariableName.Equals(variableName, StringComparison.OrdinalIgnoreCase)))
        {
            if (bound.LowerBound.HasValue)
                lower = Math.Max(lower, bound.LowerBound.Value);
            if (bound.UpperBound.HasValue)
                upper = Math.Min(upper, bound.UpperBound.Value);
        }

        return (lower, upper);
    }
}

/// <summary>
/// Represents a branching decision for a variable.
/// </summary>
/// <param name="VariableName">The variable to branch on.</param>
/// <param name="CurrentValue">The current fractional value.</param>
/// <param name="FloorValue">The floor of the current value (for down branch).</param>
/// <param name="CeilValue">The ceiling of the current value (for up branch).</param>
public record BranchingDecision(
    string VariableName,
    double CurrentValue,
    double FloorValue,
    double CeilValue
);
