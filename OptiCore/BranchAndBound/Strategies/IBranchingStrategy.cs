using OptiCore.Models;

namespace OptiCore.BranchAndBound.Strategies;

/// <summary>
/// Strategy interface for selecting which variable to branch on.
/// </summary>
public interface IBranchingStrategy
{
    /// <summary>
    /// Gets the name of this strategy.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Selects the variable to branch on for the given node.
    /// </summary>
    /// <param name="node">The current node being processed.</param>
    /// <param name="integerVariables">The list of integer variables in the model.</param>
    /// <param name="tolerance">The tolerance for considering a value as integer.</param>
    /// <returns>The branching decision, or null if all integer variables are integral.</returns>
    BranchingDecision? SelectBranchingVariable(
        BranchNode node,
        IReadOnlyList<IntegerTerm> integerVariables,
        double tolerance);
}
