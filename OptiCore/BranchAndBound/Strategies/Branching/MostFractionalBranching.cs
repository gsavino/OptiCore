using OptiCore.Models;

namespace OptiCore.BranchAndBound.Strategies.Branching;

/// <summary>
/// Selects the variable with the value closest to 0.5 (most fractional).
/// This simple strategy often performs well in practice.
/// </summary>
public class MostFractionalBranching : IBranchingStrategy
{
    /// <inheritdoc />
    public string Name => "Most Fractional";

    /// <summary>
    /// Iterates all integer variables, finds those with fractional LP values, and selects the one
    /// whose fractional part is closest to 0.5 (maximally ambiguous between floor and ceil). Checks
    /// that branching would create feasible children by respecting variable bounds. Returns
    /// <c>null</c> if all integer variables have integral values.
    /// </summary>
    /// <inheritdoc />
    public BranchingDecision? SelectBranchingVariable(
        BranchNode node,
        IReadOnlyList<IntegerTerm> integerVariables,
        double tolerance)
    {
        if (node.LpSolution == null)
            return null;

        BranchingDecision? bestDecision = null;
        double bestFractionality = 0.0;

        foreach (var intVar in integerVariables)
        {
            // Find the value of this variable in the LP solution
            var solutionTerm = node.LpSolution.FirstOrDefault(t =>
                t.TermName.Equals(intVar.TermName, StringComparison.OrdinalIgnoreCase));

            if (solutionTerm == null)
                continue;

            double value = solutionTerm.Coefficient;

            // Skip if the variable is already integral
            if (!IntegerTerm.IsFractional(value, tolerance))
                continue;

            // Calculate fractionality (how close to 0.5)
            double fractionalPart = IntegerTerm.GetFractionalPart(value);
            double fractionality = 0.5 - Math.Abs(fractionalPart - 0.5);

            // Check bounds - the branching must create feasible children
            var (lower, upper) = node.GetEffectiveBounds(intVar.TermName);
            double floor = Math.Floor(value);
            double ceil = Math.Ceiling(value);

            // Adjust for variable's inherent bounds
            double effectiveLower = Math.Max(lower, intVar.EffectiveLowerBound);
            double effectiveUpper = Math.Min(upper, intVar.EffectiveUpperBound);

            // Skip if branching would create infeasible children
            if (floor < effectiveLower && ceil > effectiveUpper)
                continue;

            if (fractionality > bestFractionality)
            {
                bestFractionality = fractionality;
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
}
