namespace OptiCore.BranchAndBound.Strategies.NodeSelection;

/// <summary>
/// Selects the deepest node in the B&B tree.
/// This strategy is memory-efficient and tends to find feasible solutions quickly,
/// but may not provide good bounds early on.
/// </summary>
public class DepthFirstNodeSelection : INodeSelectionStrategy
{
    /// <inheritdoc />
    public string Name => "Depth First";

    /// <summary>
    /// Selects the deepest pending node. Among nodes at the same depth, prefers the most recently
    /// added (scans from end of list). Falls back to the last node if no pending nodes are found.
    /// </summary>
    /// <inheritdoc />
    public BranchNode? SelectNode(IReadOnlyList<BranchNode> openNodes, double? incumbent, bool isMaximization)
    {
        if (openNodes.Count == 0)
            return null;

        // Select the deepest node (LIFO behavior)
        // Among nodes at the same depth, prefer the most recently added (last in list)
        BranchNode? selected = null;
        int maxDepth = -1;

        for (int i = openNodes.Count - 1; i >= 0; i--)
        {
            var node = openNodes[i];
            if (node.Status == NodeStatus.Pending && node.Depth > maxDepth)
            {
                maxDepth = node.Depth;
                selected = node;
            }
        }

        // If no pending node found, return any node
        if (selected == null)
        {
            return openNodes.LastOrDefault();
        }

        return selected;
    }
}
