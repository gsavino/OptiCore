using OptiCore.Abstract;

namespace OptiCore.Models;


public record Constraint(
    string ConstraintName,
    List<Term> Coefficients,
    string Operator,
    double Rhs
) : ConstraintBase
{
    public override List<Term> Coefficients { get; init; } = Coefficients;
}


