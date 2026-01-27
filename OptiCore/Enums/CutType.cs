namespace OptiCore.Enums;

/// <summary>
/// Specifies the type of cutting plane.
/// </summary>
public enum CutType
{
    /// <summary>
    /// Gomory fractional cut for pure integer programs.
    /// </summary>
    GomoryFractional,

    /// <summary>
    /// Gomory mixed-integer cut for mixed-integer programs.
    /// </summary>
    GomoryMixedInteger,

    /// <summary>
    /// Cover inequality for knapsack-type constraints.
    /// </summary>
    CoverInequality,

    /// <summary>
    /// Clique inequality from conflict graphs.
    /// </summary>
    CliqueInequality,

    /// <summary>
    /// Flow cover cut.
    /// </summary>
    FlowCover,

    /// <summary>
    /// Mixed-integer rounding cut.
    /// </summary>
    MixedIntegerRounding,

    /// <summary>
    /// User-defined custom cut.
    /// </summary>
    Custom
}
