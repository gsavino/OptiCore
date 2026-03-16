using OptiCore.Enums;
using OptiCore.Models;

namespace OptiCore.Cuts.Generators;

/// <summary>
/// Generates Gomory fractional cuts for pure integer programs.
///
/// For a row i with fractional RHS f_i0, the Gomory fractional cut is:
/// sum(f_ij * x_j) >= f_i0
/// where f_ij is the fractional part of the tableau coefficient a_ij.
/// </summary>
public class GomoryFractionalCutGenerator : GomoryCutGeneratorBase
{
    /// <inheritdoc />
    public override string Name => "GomoryFractional";

    /// <inheritdoc />
    public override CutType CutType => CutType.GomoryFractional;

    /// <inheritdoc />
    public override int Priority => 100;

    /// <summary>
    /// Generates Gomory fractional cuts from the current LP relaxation.
    /// Finds rows where an integer-constrained basic variable has a fractional value,
    /// prioritizes those closest to 0.5, and generates cuts by taking the fractional
    /// part of each tableau coefficient.
    /// </summary>
    /// <param name="context">The cut generation context containing the current simplex tableau.</param>
    /// <param name="maxCuts">Maximum number of cuts to generate.</param>
    /// <returns>A collection of valid Gomory fractional cuts.</returns>
    public override IEnumerable<Cut> GenerateCuts(CutGenerationContext context, int maxCuts = 10)
    {
        var cuts = new List<Cut>();
        var fractionalRows = FindFractionalRows(context).ToList();

        // Sort by most fractional (closest to 0.5)
        fractionalRows = fractionalRows
            .OrderByDescending(r => 0.5 - Math.Abs(GetFractionalPart(context.GetRhs(r)) - 0.5))
            .ToList();

        foreach (int row in fractionalRows)
        {
            if (cuts.Count >= maxCuts)
                break;

            var cut = GenerateCutFromRow(context, row);
            if (cut != null && ValidateCut(cut, context))
            {
                cuts.Add(cut);
            }
        }

        return cuts;
    }

    /// <summary>
    /// Generates a single Gomory fractional cut from a specific tableau row.
    /// The cut formula is: sum(frac(a_ij) * x_j) >= frac(b_i), where frac() denotes
    /// the fractional part. This cut is valid for pure integer programs where all
    /// variables are integers.
    /// </summary>
    /// <param name="context">The cut generation context containing the current simplex tableau.</param>
    /// <param name="row">The tableau row index to generate the cut from.</param>
    /// <returns>A valid cut, or null if the row does not yield a useful cut.</returns>
    private Cut? GenerateCutFromRow(CutGenerationContext context, int row)
    {
        double b_i = context.GetRhs(row);
        double f_i0 = GetFractionalPart(b_i);

        if (!IsFractional(b_i))
            return null;

        int numOriginalVars = context.NumberOfOriginalVariables;
        var coefficients = new double[numOriginalVars];
        var variableNames = context.VariableNames.Take(numOriginalVars).ToList();

        // For each non-basic variable, compute the fractional coefficient
        for (int j = 0; j < numOriginalVars; j++)
        {
            double a_ij = context.GetTableauCoefficient(row, j);
            double f_ij = GetFractionalPart(a_ij);

            // In Gomory fractional cut, all coefficients use simple fractional part
            coefficients[j] = f_ij;
        }

        return CreateCut(
            GenerateCutId(),
            variableNames,
            coefficients,
            f_i0,
            row,
            context.Round,
            context
        );
    }
}
