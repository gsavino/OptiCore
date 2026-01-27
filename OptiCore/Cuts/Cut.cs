using OptiCore.Abstract;
using OptiCore.Enums;
using OptiCore.Models;

namespace OptiCore.Cuts;

/// <summary>
/// Represents a cutting plane that can be added to the LP relaxation.
/// </summary>
public record Cut : ConstraintBase
{
    /// <summary>
    /// Unique identifier for this cut.
    /// </summary>
    public string CutId { get; init; }

    /// <summary>
    /// The type of cut.
    /// </summary>
    public CutType CutType { get; init; }

    /// <summary>
    /// The coefficients of the cut (left-hand side).
    /// </summary>
    public override List<Term> Coefficients { get; init; }

    /// <summary>
    /// The right-hand side value.
    /// </summary>
    public double Rhs { get; init; }

    /// <summary>
    /// The operator ("<=", ">=", or "=").
    /// </summary>
    public string Operator { get; init; }

    /// <summary>
    /// The source row in the tableau from which this cut was generated (if applicable).
    /// </summary>
    public int? SourceRow { get; init; }

    /// <summary>
    /// Hash for deduplication.
    /// </summary>
    public int CutHash { get; init; }

    /// <summary>
    /// Number of times this cut has been used (activity tracking).
    /// </summary>
    public int ActivityCount { get; set; }

    /// <summary>
    /// The iteration/round when this cut was generated.
    /// </summary>
    public int GenerationRound { get; init; }

    /// <summary>
    /// The efficacy (violation) when the cut was generated.
    /// </summary>
    public double Efficacy { get; init; }

    /// <summary>
    /// Creates a new cut.
    /// </summary>
    public Cut(
        string cutId,
        CutType cutType,
        IEnumerable<Term> coefficients,
        double rhs,
        string op,
        int? sourceRow = null,
        int generationRound = 0,
        double efficacy = 0)
    {
        CutId = cutId;
        CutType = cutType;
        Coefficients = coefficients.ToList();
        Rhs = rhs;
        Operator = op;
        SourceRow = sourceRow;
        GenerationRound = generationRound;
        Efficacy = efficacy;
        CutHash = ComputeHash();
        ActivityCount = 0;
    }

    /// <summary>
    /// Converts this cut to a Constraint for adding to a model.
    /// </summary>
    public Constraint ToConstraint()
    {
        return new Constraint(
            ConstraintName: CutId,
            Coefficients: Coefficients.ToList(),
            Operator: Operator,
            Rhs: Rhs
        );
    }

    /// <summary>
    /// Evaluates the left-hand side of the cut at a given solution.
    /// </summary>
    /// <param name="solution">The solution values.</param>
    /// <returns>The LHS value.</returns>
    public double EvaluateLhs(IReadOnlyDictionary<string, double> solution)
    {
        double lhs = 0;
        foreach (var term in Coefficients)
        {
            if (solution.TryGetValue(term.TermName, out double value))
            {
                lhs += term.Coefficient * value;
            }
        }
        return lhs;
    }

    /// <summary>
    /// Evaluates the left-hand side using a list of terms.
    /// </summary>
    public double EvaluateLhs(IEnumerable<Term> solution)
    {
        var dict = solution.ToDictionary(
            t => t.TermName,
            t => t.Coefficient,
            StringComparer.OrdinalIgnoreCase);
        return EvaluateLhs(dict);
    }

    /// <summary>
    /// Calculates the violation of this cut at a given solution.
    /// </summary>
    /// <param name="solution">The solution values.</param>
    /// <returns>The violation (positive if violated, negative if satisfied).</returns>
    public double GetViolation(IReadOnlyDictionary<string, double> solution)
    {
        double lhs = EvaluateLhs(solution);

        return Operator switch
        {
            "<=" => lhs - Rhs,
            ">=" => Rhs - lhs,
            "=" => Math.Abs(lhs - Rhs),
            _ => 0
        };
    }

    /// <summary>
    /// Checks if this cut is violated by the given solution.
    /// </summary>
    /// <param name="solution">The solution to check.</param>
    /// <param name="tolerance">The tolerance for violation.</param>
    /// <returns>True if the cut is violated.</returns>
    public bool IsViolated(IReadOnlyDictionary<string, double> solution, double tolerance = 1e-6)
    {
        return GetViolation(solution) > tolerance;
    }

    private int ComputeHash()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + CutType.GetHashCode();

            // Sort coefficients by variable name for consistent hashing
            var sortedCoeffs = Coefficients.OrderBy(t => t.TermName).ToList();
            foreach (var term in sortedCoeffs)
            {
                hash = hash * 31 + term.TermName.GetHashCode();
                hash = hash * 31 + Math.Round(term.Coefficient, 6).GetHashCode();
            }

            hash = hash * 31 + Math.Round(Rhs, 6).GetHashCode();
            hash = hash * 31 + Operator.GetHashCode();
            return hash;
        }
    }

    /// <summary>
    /// Creates a cut with normalized coefficients (scales so largest coeff is 1).
    /// </summary>
    public Cut Normalize()
    {
        if (Coefficients.Count == 0)
            return this;

        double maxCoeff = Coefficients.Max(t => Math.Abs(t.Coefficient));
        if (maxCoeff < 1e-10)
            return this;

        var normalizedCoeffs = Coefficients
            .Select(t => new Term(t.TermName, t.Coefficient / maxCoeff))
            .ToList();

        return this with
        {
            Coefficients = normalizedCoeffs,
            Rhs = Rhs / maxCoeff,
            CutHash = ComputeHash()
        };
    }
}
