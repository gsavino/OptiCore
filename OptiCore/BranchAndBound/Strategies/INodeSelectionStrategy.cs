namespace OptiCore.BranchAndBound.Strategies;

/// <summary>
/// Strategy interface for selecting the next node to process in the B&B tree.
/// </summary>
public interface INodeSelectionStrategy
{
    /// <summary>
    /// Gets the name of this strategy.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Selects the next node to process from the list of open nodes.
    /// </summary>
    /// <param name="openNodes">The list of pending nodes.</param>
    /// <param name="incumbent">The current incumbent objective value (null if none).</param>
    /// <param name="isMaximization">True if maximizing, false if minimizing.</param>
    /// <returns>The selected node, or null if no suitable node found.</returns>
    BranchNode? SelectNode(IReadOnlyList<BranchNode> openNodes, double? incumbent, bool isMaximization);
}
