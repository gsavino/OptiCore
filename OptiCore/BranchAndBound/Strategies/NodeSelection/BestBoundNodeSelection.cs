namespace OptiCore.BranchAndBound.Strategies.NodeSelection;

/// <summary>
/// Selects the node with the best LP bound.
/// This strategy tends to find good solutions quickly and provides tight bounds,
/// but may explore many nodes.
/// </summary>
public class BestBoundNodeSelection : INodeSelectionStrategy
{
    /// <inheritdoc />
    public string Name => "Best Bound";

    /// <inheritdoc />
    public BranchNode? SelectNode(IReadOnlyList<BranchNode> openNodes, double? incumbent, bool isMaximization)
    {
        if (openNodes.Count == 0)
            return null;

        // Filter nodes that have been solved (have an LP bound)
        var solvedNodes = openNodes.Where(n => n.LpBound.HasValue).ToList();

        if (solvedNodes.Count == 0)
        {
            // If no nodes have been solved yet, return the first pending node
            return openNodes.FirstOrDefault(n => n.Status == NodeStatus.Pending);
        }

        // Select the node with the best bound
        if (isMaximization)
        {
            // For maximization, select node with highest upper bound
            return solvedNodes.OrderByDescending(n => n.LpBound!.Value).First();
        }
        else
        {
            // For minimization, select node with lowest lower bound
            return solvedNodes.OrderBy(n => n.LpBound!.Value).First();
        }
    }
}
