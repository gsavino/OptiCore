namespace OptiCore.Models;

/// <summary>
/// A mutable tracking wrapper for a variable term, used internally during solver processing
/// to track which variables have been handled. The <see cref="WasProcessed"/> flag indicates
/// whether this term has been incorporated into the simplex tableau or another computation step.
/// </summary>
public class ControlTerm
{
    /// <summary>
    /// The name of the decision variable.
    /// </summary>
    public string TermName { get; set; }

    /// <summary>
    /// Indicates whether this term has been incorporated into the simplex tableau or another computation step.
    /// </summary>
    public bool WasProcessed { get; set; }

    /// <summary>
    /// The numeric coefficient associated with the variable.
    /// </summary>
    public double Coefficient { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ControlTerm"/> class.
    /// </summary>
    /// <param name="termName">The name of the decision variable.</param>
    /// <param name="coefficient">The numeric coefficient associated with the variable.</param>
    /// <param name="wasProcessed">Whether this term has already been processed. Defaults to false.</param>
    public ControlTerm(string termName, double coefficient, bool wasProcessed = false)
    {
        TermName = termName;
        WasProcessed = wasProcessed;
        Coefficient = coefficient;
    }
}
