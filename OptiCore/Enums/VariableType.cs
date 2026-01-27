namespace OptiCore.Enums;

/// <summary>
/// Specifies the type of a decision variable in optimization models.
/// </summary>
public enum VariableType
{
    /// <summary>
    /// Variable can take any real value within its bounds.
    /// </summary>
    Continuous,

    /// <summary>
    /// Variable must take an integer value.
    /// </summary>
    Integer,

    /// <summary>
    /// Variable can only be 0 or 1 (special case of integer).
    /// </summary>
    Binary
}
