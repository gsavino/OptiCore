using OptiCore.Enums;
using OptiCore.Models;

namespace OptiCore.Cuts.Generators;

/// <summary>
/// Generates Gomory mixed-integer cuts for mixed-integer programs.
///
/// The GMI cut handles continuous and integer variables differently:
/// - For integer non-basic variables with f_ij <= f_i0: coefficient is f_ij
/// - For integer non-basic variables with f_ij > f_i0: coefficient is f_i0 * (1 - f_ij) / (1 - f_i0)
/// - For continuous non-basic variables with a_ij >= 0: coefficient is a_ij
/// - For continuous non-basic variables with a_ij < 0: coefficient is -a_ij * f_i0 / (1 - f_i0)
/// </summary>
public class GomoryMixedIntegerCutGenerator : GomoryCutGeneratorBase
{
    /// <inheritdoc />
    public override string Name => "GomoryMixedInteger";

    /// <inheritdoc />
    public override CutType CutType => CutType.GomoryMixedInteger;

    /// <inheritdoc />
    public override int Priority => 90;

    /// <summary>
    /// Generates Gomory Mixed-Integer (GMI) cuts from the current LP relaxation.
    /// Similar to fractional cuts but handles continuous and integer variables differently,
    /// making it valid for mixed-integer programs.
    /// </summary>
    /// <param name="context">The cut generation context containing the current simplex tableau.</param>
    /// <param name="maxCuts">Maximum number of cuts to generate.</param>
    /// <returns>A collection of valid GMI cuts.</returns>
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
    /// Generates a single GMI cut from a tableau row. The coefficient computation depends
    /// on whether each variable is integer or continuous: integer variables use fractional
    /// parts with a complementary formula for f_ij > f_i0, while continuous variables use
    /// the raw coefficient with a scaling factor for negative values.
    /// </summary>
    /// <param name="context">The cut generation context containing the current simplex tableau.</param>
    /// <param name="row">The tableau row index to generate the cut from.</param>
    /// <returns>A valid GMI cut, or null if the row does not yield a useful cut.</returns>
    private Cut? GenerateCutFromRow(CutGenerationContext context, int row)
    {
        double b_i = context.GetRhs(row);
        double f_i0 = GetFractionalPart(b_i);

        if (!IsFractional(b_i))
            return null;

        // Guard against division by zero
        double oneMinusF_i0 = 1.0 - f_i0;
        if (Math.Abs(oneMinusF_i0) < 1e-10)
            return null;

        int numOriginalVars = context.NumberOfOriginalVariables;
        var coefficients = new double[numOriginalVars];
        var variableNames = context.VariableNames.Take(numOriginalVars).ToList();

        // For each non-basic variable, compute the GMI coefficient
        for (int j = 0; j < numOriginalVars; j++)
        {
            double a_ij = context.GetTableauCoefficient(row, j);

            if (context.IsIntegerVariable(j))
            {
                // Integer variable
                double f_ij = GetFractionalPart(a_ij);

                if (f_ij <= f_i0)
                {
                    // f_ij
                    coefficients[j] = f_ij;
                }
                else
                {
                    // f_i0 * (1 - f_ij) / (1 - f_i0)
                    coefficients[j] = f_i0 * (1.0 - f_ij) / oneMinusF_i0;
                }
            }
            else
            {
                // Continuous variable
                if (a_ij >= 0)
                {
                    // a_ij
                    coefficients[j] = a_ij;
                }
                else
                {
                    // -a_ij * f_i0 / (1 - f_i0)
                    coefficients[j] = -a_ij * f_i0 / oneMinusF_i0;
                }
            }
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
