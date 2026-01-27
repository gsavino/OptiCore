using OptiCore.Models;

namespace OptiCore.BranchAndBound.Strategies.Branching;

/// <summary>
/// Selects the branching variable based on learned pseudo-costs.
/// Pseudo-costs estimate the per-unit degradation in the objective when branching on a variable.
/// </summary>
public class PseudoCostBranching : IBranchingStrategy
{
    // Pseudo-costs: (sum of degradations, count) for down and up branches
    private readonly Dictionary<string, (double sumDown, int countDown, double sumUp, int countUp)> _pseudoCosts = new();

    /// <summary>
    /// Minimum number of observations before using learned pseudo-costs.
    /// </summary>
    public int MinReliableCount { get; init; } = 5;

    /// <summary>
    /// Default pseudo-cost per unit when no data is available.
    /// </summary>
    public double DefaultPseudoCost { get; init; } = 1.0;

    /// <summary>
    /// Weight for down branch in score calculation.
    /// </summary>
    public double DownWeight { get; init; } = 1.0;

    /// <summary>
    /// Weight for up branch in score calculation.
    /// </summary>
    public double UpWeight { get; init; } = 1.0;

    /// <inheritdoc />
    public string Name => "Pseudo Cost";

    /// <inheritdoc />
    public BranchingDecision? SelectBranchingVariable(
        BranchNode node,
        IReadOnlyList<IntegerTerm> integerVariables,
        double tolerance)
    {
        if (node.LpSolution == null)
            return null;

        BranchingDecision? bestDecision = null;
        double bestScore = double.NegativeInfinity;

        foreach (var intVar in integerVariables)
        {
            var solutionTerm = node.LpSolution.FirstOrDefault(t =>
                t.TermName.Equals(intVar.TermName, StringComparison.OrdinalIgnoreCase));

            if (solutionTerm == null)
                continue;

            double value = solutionTerm.Coefficient;

            if (!IntegerTerm.IsFractional(value, tolerance))
                continue;

            double fractionalPart = IntegerTerm.GetFractionalPart(value);
            double floor = Math.Floor(value);
            double ceil = Math.Ceiling(value);

            // Check bounds
            var (lower, upper) = node.GetEffectiveBounds(intVar.TermName);
            double effectiveLower = Math.Max(lower, intVar.EffectiveLowerBound);
            double effectiveUpper = Math.Min(upper, intVar.EffectiveUpperBound);

            if (floor < effectiveLower && ceil > effectiveUpper)
                continue;

            // Get pseudo-costs for this variable
            var (pcDown, pcUp) = GetPseudoCosts(intVar.TermName);

            // Estimate degradation for each branch direction
            double downDeg = pcDown * fractionalPart;
            double upDeg = pcUp * (1.0 - fractionalPart);

            // Score using product rule (commonly used in practice)
            double score = Math.Max(downDeg * DownWeight, tolerance) * Math.Max(upDeg * UpWeight, tolerance);

            if (score > bestScore)
            {
                bestScore = score;
                bestDecision = new BranchingDecision(
                    VariableName: intVar.TermName,
                    CurrentValue: value,
                    FloorValue: Math.Max(floor, effectiveLower),
                    CeilValue: Math.Min(ceil, effectiveUpper)
                );
            }
        }

        return bestDecision;
    }

    /// <summary>
    /// Updates pseudo-costs based on observed branch degradation.
    /// </summary>
    /// <param name="variableName">The variable that was branched on.</param>
    /// <param name="direction">The branch direction ("down" or "up").</param>
    /// <param name="parentBound">The LP bound at the parent node.</param>
    /// <param name="childBound">The LP bound at the child node.</param>
    /// <param name="fractionalPart">The fractional part at the time of branching.</param>
    public void UpdatePseudoCost(
        string variableName,
        string direction,
        double parentBound,
        double childBound,
        double fractionalPart)
    {
        double degradation = Math.Abs(childBound - parentBound);
        double unitDegradation;

        if (direction == "down")
        {
            unitDegradation = fractionalPart > 1e-10 ? degradation / fractionalPart : 0;
        }
        else
        {
            double upPart = 1.0 - fractionalPart;
            unitDegradation = upPart > 1e-10 ? degradation / upPart : 0;
        }

        if (!_pseudoCosts.TryGetValue(variableName, out var current))
        {
            current = (0, 0, 0, 0);
        }

        if (direction == "down")
        {
            _pseudoCosts[variableName] = (
                current.sumDown + unitDegradation,
                current.countDown + 1,
                current.sumUp,
                current.countUp
            );
        }
        else
        {
            _pseudoCosts[variableName] = (
                current.sumDown,
                current.countDown,
                current.sumUp + unitDegradation,
                current.countUp + 1
            );
        }
    }

    /// <summary>
    /// Gets the current pseudo-costs for a variable.
    /// </summary>
    /// <param name="variableName">The variable name.</param>
    /// <returns>Tuple of (down pseudo-cost, up pseudo-cost).</returns>
    public (double down, double up) GetPseudoCosts(string variableName)
    {
        if (!_pseudoCosts.TryGetValue(variableName, out var costs))
        {
            return (DefaultPseudoCost, DefaultPseudoCost);
        }

        double pcDown = costs.countDown >= MinReliableCount
            ? costs.sumDown / costs.countDown
            : DefaultPseudoCost;

        double pcUp = costs.countUp >= MinReliableCount
            ? costs.sumUp / costs.countUp
            : DefaultPseudoCost;

        return (pcDown, pcUp);
    }

    /// <summary>
    /// Clears all learned pseudo-costs.
    /// </summary>
    public void Reset()
    {
        _pseudoCosts.Clear();
    }
}
