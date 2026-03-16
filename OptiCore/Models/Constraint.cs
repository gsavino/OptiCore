using OptiCore.Abstract;

namespace OptiCore.Models;

/// <summary>
/// Represents a linear constraint in the optimization model (e.g., 2x1 + 3x2 &lt;= 12).
/// Each constraint has a name for identification, a list of variable coefficients forming
/// the left-hand side, a comparison operator (&lt;=, &gt;=, or =), and a right-hand side constant value.
/// </summary>
/// <param name="ConstraintName">A descriptive name identifying this constraint.</param>
/// <param name="Coefficients">The variable-coefficient pairs forming the left-hand side of the constraint.</param>
/// <param name="Operator">The comparison operator: "&lt;=", "&gt;=", or "=".</param>
/// <param name="Rhs">The right-hand side constant value of the constraint.</param>
public record Constraint(
    string ConstraintName,
    List<Term> Coefficients,
    string Operator,
    double Rhs
) : ConstraintBase
{
    /// <inheritdoc />
    public override List<Term> Coefficients { get; init; } = Coefficients;
}
