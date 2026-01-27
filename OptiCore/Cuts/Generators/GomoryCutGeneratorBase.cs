using OptiCore.Enums;
using OptiCore.Models;

namespace OptiCore.Cuts.Generators;

/// <summary>
/// Base class for Gomory cut generators with shared functionality.
/// </summary>
public abstract class GomoryCutGeneratorBase : ICutGenerator
{
    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract CutType CutType { get; }

    /// <inheritdoc />
    public virtual int Priority => 100;

    /// <summary>
    /// Minimum fractional part to consider a value as fractional.
    /// </summary>
    protected double MinFractionalValue { get; init; } = 1e-4;

    /// <summary>
    /// Maximum fractional part to consider a value as fractional.
    /// </summary>
    protected double MaxFractionalValue { get; init; } = 1 - 1e-4;

    /// <summary>
    /// Minimum coefficient magnitude to include in the cut.
    /// </summary>
    protected double MinCoefficientMagnitude { get; init; } = 1e-10;

    private int _cutCounter = 0;

    /// <inheritdoc />
    public abstract IEnumerable<Cut> GenerateCuts(CutGenerationContext context, int maxCuts = 10);

    /// <inheritdoc />
    public virtual bool ShouldGenerateAtNode(int nodeDepth, int nodeCount)
    {
        // Generate at root and early nodes, then less frequently
        if (nodeDepth == 0) return true;
        if (nodeDepth <= 5) return true;
        if (nodeDepth <= 10 && nodeCount % 5 == 0) return true;
        return nodeCount % 20 == 0;
    }

    /// <summary>
    /// Gets the fractional part of a value.
    /// </summary>
    protected static double GetFractionalPart(double value)
    {
        double f = value - Math.Floor(value);
        return f;
    }

    /// <summary>
    /// Checks if a value is considered fractional.
    /// </summary>
    protected bool IsFractional(double value)
    {
        double f = GetFractionalPart(value);
        return f > MinFractionalValue && f < MaxFractionalValue;
    }

    /// <summary>
    /// Generates a unique cut ID.
    /// </summary>
    protected string GenerateCutId()
    {
        return $"{Name}_{++_cutCounter}";
    }

    /// <summary>
    /// Finds rows with fractional basic variables that are integer-constrained.
    /// </summary>
    protected IEnumerable<int> FindFractionalRows(CutGenerationContext context)
    {
        return context.GetFractionalBasicRows();
    }

    /// <summary>
    /// Creates a cut from the computed coefficients.
    /// </summary>
    protected Cut? CreateCut(
        string cutId,
        IReadOnlyList<string> variableNames,
        double[] coefficients,
        double rhs,
        int sourceRow,
        int round,
        CutGenerationContext context)
    {
        var terms = new List<Term>();

        for (int j = 0; j < variableNames.Count; j++)
        {
            if (Math.Abs(coefficients[j]) > MinCoefficientMagnitude)
            {
                terms.Add(new Term(variableNames[j], coefficients[j]));
            }
        }

        if (terms.Count == 0)
            return null;

        // Calculate efficacy (violation at current solution)
        double lhs = 0;
        foreach (var term in terms)
        {
            lhs += term.Coefficient * context.GetVariableValue(term.TermName);
        }
        double efficacy = lhs - rhs;

        if (efficacy <= MinFractionalValue)
            return null;

        return new Cut(
            cutId: cutId,
            cutType: CutType,
            coefficients: terms,
            rhs: rhs,
            op: ">=",
            sourceRow: sourceRow,
            generationRound: round,
            efficacy: efficacy
        );
    }

    /// <summary>
    /// Validates that the cut is violated by the current solution.
    /// </summary>
    protected bool ValidateCut(Cut cut, CutGenerationContext context, double minViolation = 1e-6)
    {
        var solutionDict = context.GetSolutionDictionary();
        return cut.IsViolated(solutionDict, minViolation);
    }
}
