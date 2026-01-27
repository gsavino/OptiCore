using OptiCore.Enums;

namespace OptiCore.Models;

/// <summary>
/// Represents a decision variable with type information for integer programming.
/// Extends the basic Term with variable type and optional bounds for branching.
/// </summary>
public record IntegerTerm(
    string TermName,
    double Coefficient,
    VariableType Type = VariableType.Continuous,
    double? LowerBound = null,
    double? UpperBound = null
) : Term(TermName, Coefficient)
{
    /// <summary>
    /// Default lower bound for binary variables.
    /// </summary>
    public const double BinaryLowerBound = 0.0;

    /// <summary>
    /// Default upper bound for binary variables.
    /// </summary>
    public const double BinaryUpperBound = 1.0;

    /// <summary>
    /// Default tolerance for integrality checks.
    /// </summary>
    public const double DefaultIntegralityTolerance = 1e-6;

    /// <summary>
    /// Gets the effective lower bound, accounting for binary variables.
    /// </summary>
    public double EffectiveLowerBound => Type == VariableType.Binary
        ? BinaryLowerBound
        : LowerBound ?? double.NegativeInfinity;

    /// <summary>
    /// Gets the effective upper bound, accounting for binary variables.
    /// </summary>
    public double EffectiveUpperBound => Type == VariableType.Binary
        ? BinaryUpperBound
        : UpperBound ?? double.PositiveInfinity;

    /// <summary>
    /// Checks if a value satisfies the integrality requirement for this variable.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="tolerance">The tolerance for considering a value as integer (default: 1e-6).</param>
    /// <returns>True if the value is feasible for this variable type.</returns>
    public bool IsFeasibleValue(double value, double tolerance = DefaultIntegralityTolerance)
    {
        // Continuous variables accept any value within bounds
        if (Type == VariableType.Continuous)
        {
            return value >= EffectiveLowerBound - tolerance &&
                   value <= EffectiveUpperBound + tolerance;
        }

        // Check integrality for Integer and Binary types
        double roundedValue = Math.Round(value);
        bool isIntegral = Math.Abs(value - roundedValue) <= tolerance;

        if (!isIntegral)
            return false;

        // Check bounds
        return value >= EffectiveLowerBound - tolerance &&
               value <= EffectiveUpperBound + tolerance;
    }

    /// <summary>
    /// Returns the fractional part of a value (distance from nearest integer below).
    /// </summary>
    /// <param name="value">The value to get the fractional part of.</param>
    /// <returns>The fractional part (0 to 1).</returns>
    public static double GetFractionalPart(double value)
    {
        return value - Math.Floor(value);
    }

    /// <summary>
    /// Checks if a value is fractional (not integer within tolerance).
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="tolerance">The tolerance for considering a value as integer.</param>
    /// <returns>True if the value is fractional.</returns>
    public static bool IsFractional(double value, double tolerance = DefaultIntegralityTolerance)
    {
        double fractionalPart = GetFractionalPart(value);
        return fractionalPart > tolerance && fractionalPart < (1.0 - tolerance);
    }

    /// <summary>
    /// Creates a new IntegerTerm with updated bounds (immutable pattern).
    /// </summary>
    /// <param name="newLowerBound">The new lower bound (null to keep existing).</param>
    /// <param name="newUpperBound">The new upper bound (null to keep existing).</param>
    /// <returns>A new IntegerTerm with the specified bounds.</returns>
    public IntegerTerm WithBounds(double? newLowerBound = null, double? newUpperBound = null)
    {
        return this with
        {
            LowerBound = newLowerBound ?? LowerBound,
            UpperBound = newUpperBound ?? UpperBound
        };
    }

    /// <summary>
    /// Creates an IntegerTerm from a basic Term with the specified type.
    /// </summary>
    /// <param name="term">The source Term.</param>
    /// <param name="type">The variable type.</param>
    /// <param name="lowerBound">Optional lower bound.</param>
    /// <param name="upperBound">Optional upper bound.</param>
    /// <returns>A new IntegerTerm.</returns>
    public static IntegerTerm FromTerm(Term term, VariableType type = VariableType.Continuous,
        double? lowerBound = null, double? upperBound = null)
    {
        return new IntegerTerm(term.TermName, term.Coefficient, type, lowerBound, upperBound);
    }

    /// <summary>
    /// Determines if this variable requires integrality constraints.
    /// </summary>
    public bool RequiresIntegrality => Type != VariableType.Continuous;
}
