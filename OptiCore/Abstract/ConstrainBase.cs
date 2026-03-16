using System;
using OptiCore.Models;

namespace OptiCore.Abstract;

/// <summary>
/// Abstract base record providing coefficient lookup functionality shared by both constraints
/// and the objective function. Serves as the foundation for any linear expression in the model
/// (constraints and objective).
/// </summary>
public abstract record ConstraintBase
{
    /// <summary>
    /// The list of terms (variable-coefficient pairs) that form the linear expression.
    /// </summary>
    public abstract List<Term> Coefficients { get; init; }

    /// <summary>
    /// Retrieves the coefficient for a given variable name.
    /// Returns 0.0 if the variable is not found in this expression. Uses case-insensitive matching.
    /// </summary>
    /// <param name="variableName">The name of the variable to look up.</param>
    /// <returns>The coefficient value for the variable, or 0.0 if not found.</returns>
    public double GetCoefficient(string variableName) =>
        Coefficients?
            .FirstOrDefault(t =>
                string.Equals(t.TermName, variableName, StringComparison.OrdinalIgnoreCase)
            )?.Coefficient ?? 0.0;
}
