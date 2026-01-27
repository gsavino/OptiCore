namespace OptiCore.BranchAndBound.Strategies.NodeSelection;

/// <summary>
/// Selects nodes based on a weighted estimate combining LP bound and depth.
/// This hybrid strategy balances exploration (best bound) and exploitation (depth first).
/// </summary>
public class BestEstimateNodeSelection : INodeSelectionStrategy
{
    /// <summary>
    /// Weight for the bound component (0-1). Higher values favor better bounds.
    /// </summary>
    public double BoundWeight { get; init; } = 0.7;

    /// <summary>
    /// Weight for the depth component (0-1). Higher values favor deeper nodes.
    /// </summary>
    public double DepthWeight => 1.0 - BoundWeight;

    /// <inheritdoc />
    public string Name => "Best Estimate";

    /// <inheritdoc />
    public BranchNode? SelectNode(IReadOnlyList<BranchNode> openNodes, double? incumbent, bool isMaximization)
    {
        if (openNodes.Count == 0)
            return null;

        // Need at least some solved nodes to compute estimates
        var solvedNodes = openNodes.Where(n => n.LpBound.HasValue).ToList();

        if (solvedNodes.Count == 0)
        {
            // Return first pending or any node
            return openNodes.FirstOrDefault(n => n.Status == NodeStatus.Pending) ?? openNodes.First();
        }

        // Find bound range for normalization
        double minBound = solvedNodes.Min(n => n.LpBound!.Value);
        double maxBound = solvedNodes.Max(n => n.LpBound!.Value);
        double boundRange = maxBound - minBound;
        if (boundRange < 1e-10) boundRange = 1.0;

        int maxDepth = solvedNodes.Max(n => n.Depth);
        if (maxDepth == 0) maxDepth = 1;

        BranchNode? bestNode = null;
        double bestScore = double.NegativeInfinity;

        foreach (var node in solvedNodes)
        {
            double normalizedBound;
            if (isMaximization)
            {
                // Higher bound is better for maximization
                normalizedBound = (node.LpBound!.Value - minBound) / boundRange;
            }
            else
            {
                // Lower bound is better for minimization
                normalizedBound = (maxBound - node.LpBound!.Value) / boundRange;
            }

            double normalizedDepth = (double)node.Depth / maxDepth;

            double score = BoundWeight * normalizedBound + DepthWeight * normalizedDepth;

            if (score > bestScore)
            {
                bestScore = score;
                bestNode = node;
            }
        }

        return bestNode;
    }
}
