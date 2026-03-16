using System;

namespace OptiCore.Models;

/// <summary>
/// Represents a single term in a linear expression, pairing a variable name with its coefficient.
/// This is the fundamental building block for constraints and objective functions.
/// For example, in "3x1 + 5x2", the terms are (x1, 3.0) and (x2, 5.0).
/// Note: When used in <see cref="ModelResult"/>, the <paramref name="Coefficient"/> field stores
/// the solution value, not the objective coefficient.
/// </summary>
/// <param name="TermName">The name of the decision variable (e.g., "x1", "x2").</param>
/// <param name="Coefficient">The numeric coefficient associated with the variable.</param>
public record Term(
    string TermName,
    double Coefficient
);
