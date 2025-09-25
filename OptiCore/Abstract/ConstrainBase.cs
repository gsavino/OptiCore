using System;
using OptiCore.Models;

namespace OptiCore.Abstract;

public abstract record ConstraintBase
{
    public abstract List<Term> Coefficients { get; init; }

    public double GetCoefficient(string variableName) =>
        Coefficients?
            .FirstOrDefault(t =>
                string.Equals(t.TermName, variableName, StringComparison.OrdinalIgnoreCase)
            )?.Coefficient ?? 0.0;
}
