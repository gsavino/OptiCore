namespace OptiCore.Enums;

/// <summary>
/// Specifies the type of optimization model.
/// </summary>
public enum ModelType
{
    /// <summary>
    /// Linear Programming - all variables are continuous.
    /// </summary>
    LinearProgramming,

    /// <summary>
    /// Integer Linear Programming - all decision variables must be integers.
    /// </summary>
    IntegerLinearProgramming,

    /// <summary>
    /// Mixed-Integer Linear Programming - some variables are integers, others continuous.
    /// </summary>
    MixedIntegerLinearProgramming
}
