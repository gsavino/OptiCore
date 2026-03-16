namespace OptiCore.Models;

/// <summary>
/// Holds the solution of a linear programming problem after the simplex solver has found the optimal values.
/// Contains the optimal objective function value (Z) and the values of each decision variable at the optimum.
/// The <see cref="Terms"/> list stores each variable with its optimal value in the Coefficient field.
/// </summary>
public class ModelResult
{
    /// <summary>
    /// The decision variable values at the optimal solution. Each term's Coefficient field
    /// holds the variable's optimal value (not its objective function coefficient).
    /// </summary>
    public List<Term> Terms  { get; set; }

    /// <summary>
    /// The optimal value of the objective function (Z) at the solution point.
    /// </summary>
    public double OptimalResult{ get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="ModelResult"/> with an empty term list and zero optimal result.
    /// </summary>
    public ModelResult()
    {
        Terms = [];
        OptimalResult = 0;
    }

    /// <summary>
    /// Returns a formatted string showing the optimal objective value and all decision variable values.
    /// </summary>
    /// <returns>A human-readable representation of the solution.</returns>
    public override string ToString()
    {
        string toPrint = $"Z = {OptimalResult} \n";
        foreach (var term in Terms)
        {
            toPrint += $"Variable {term.TermName}  = {term.Coefficient}  \n";
        }
        return toPrint;
    }
}
