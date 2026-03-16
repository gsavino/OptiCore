using System;
using OptiCore.Abstract;
using OptiCore.Enums;

namespace OptiCore.Models;

/// <summary>
/// Represents the objective function of a linear programming model.
/// Defines the optimization goal (maximize or minimize) and the coefficients for each decision variable.
/// For example, "Maximize 3x1 + 5x2" would have Goal=MAX and coefficients [(x1, 3.0), (x2, 5.0)].
/// Inherits <see cref="ConstraintBase.GetCoefficient"/> from <see cref="ConstraintBase"/> for coefficient lookup.
/// </summary>
/// <param name="Goal">The optimization direction: maximize or minimize.</param>
/// <param name="Coefficients">The variable-coefficient pairs defining the objective function.</param>
public record ModelObjective(
    ObjectiveType Goal,
    List<Term> Coefficients
) : ConstraintBase
{
    /// <inheritdoc />
    public override List<Term> Coefficients { get; init; } = Coefficients;
}
