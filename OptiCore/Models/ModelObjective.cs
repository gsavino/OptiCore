using System;
using OptiCore.Abstract;
using OptiCore.Enums;

namespace OptiCore.Models;

public record ModelObjective(
    ObjectiveType Goal,
    List<Term> Coefficients
) : ConstraintBase
{
public override List<Term> Coefficients { get; init; } = Coefficients;
}

