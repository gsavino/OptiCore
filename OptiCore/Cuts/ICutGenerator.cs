using OptiCore.Enums;

namespace OptiCore.Cuts;

/// <summary>
/// Interface for cut generation algorithms.
/// </summary>
public interface ICutGenerator
{
    /// <summary>
    /// Gets the name of this cut generator.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the type of cuts this generator produces.
    /// </summary>
    CutType CutType { get; }

    /// <summary>
    /// Gets the priority of this generator (higher = run earlier).
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Generates cuts for the given context.
    /// </summary>
    /// <param name="context">The cut generation context with LP solution and tableau.</param>
    /// <param name="maxCuts">Maximum number of cuts to generate.</param>
    /// <returns>The generated cuts.</returns>
    IEnumerable<Cut> GenerateCuts(CutGenerationContext context, int maxCuts = 10);

    /// <summary>
    /// Determines if cuts should be generated at a given node.
    /// </summary>
    /// <param name="nodeDepth">The depth of the current node in the B&B tree.</param>
    /// <param name="nodeCount">The total number of nodes processed so far.</param>
    /// <returns>True if cuts should be generated.</returns>
    bool ShouldGenerateAtNode(int nodeDepth, int nodeCount);
}
