using OptiCore.Models;

namespace OptiCore.Cuts;

/// <summary>
/// Provides context for cut generation including the current LP solution and tableau.
/// </summary>
public class CutGenerationContext
{
    /// <summary>
    /// The simplex tableau after solving the LP relaxation.
    /// </summary>
    public double[,] SimplexMatrix { get; }

    /// <summary>
    /// The names of variables in column order.
    /// </summary>
    public IReadOnlyList<string> VariableNames { get; }

    /// <summary>
    /// Indices of integer variables in the VariableNames list.
    /// </summary>
    public IReadOnlySet<int> IntegerVariableIndices { get; }

    /// <summary>
    /// The current LP solution values.
    /// </summary>
    public IReadOnlyList<Term> CurrentSolution { get; }

    /// <summary>
    /// The number of original decision variables (not including slacks).
    /// </summary>
    public int NumberOfOriginalVariables { get; }

    /// <summary>
    /// The number of rows in the tableau (constraints + 1 for objective).
    /// </summary>
    public int NumberOfRows { get; }

    /// <summary>
    /// The number of columns in the tableau.
    /// </summary>
    public int NumberOfColumns { get; }

    /// <summary>
    /// The current LP objective value.
    /// </summary>
    public double ObjectiveValue { get; }

    /// <summary>
    /// Tolerance for considering a value as integer.
    /// </summary>
    public double IntegralityTolerance { get; }

    /// <summary>
    /// The current generation round (for tracking cut age).
    /// </summary>
    public int Round { get; }

    /// <summary>
    /// Creates a new cut generation context.
    /// </summary>
    public CutGenerationContext(
        double[,] simplexMatrix,
        IReadOnlyList<string> variableNames,
        IReadOnlySet<int> integerVariableIndices,
        IReadOnlyList<Term> currentSolution,
        int numberOfOriginalVariables,
        double objectiveValue,
        int round = 0,
        double integralityTolerance = 1e-6)
    {
        SimplexMatrix = simplexMatrix;
        VariableNames = variableNames;
        IntegerVariableIndices = integerVariableIndices;
        CurrentSolution = currentSolution;
        NumberOfOriginalVariables = numberOfOriginalVariables;
        NumberOfRows = simplexMatrix.GetLength(0);
        NumberOfColumns = simplexMatrix.GetLength(1);
        ObjectiveValue = objectiveValue;
        Round = round;
        IntegralityTolerance = integralityTolerance;
    }

    /// <summary>
    /// Gets the value of a variable from the current solution.
    /// </summary>
    public double GetVariableValue(string variableName)
    {
        var term = CurrentSolution.FirstOrDefault(t =>
            t.TermName.Equals(variableName, StringComparison.OrdinalIgnoreCase));
        return term?.Coefficient ?? 0.0;
    }

    /// <summary>
    /// Gets the value of a variable by its index.
    /// </summary>
    public double GetVariableValue(int index)
    {
        if (index < 0 || index >= VariableNames.Count)
            return 0.0;
        return GetVariableValue(VariableNames[index]);
    }

    /// <summary>
    /// Gets the RHS value for a constraint row.
    /// </summary>
    public double GetRhs(int row)
    {
        return SimplexMatrix[row, NumberOfColumns - 1];
    }

    /// <summary>
    /// Gets a coefficient from the tableau.
    /// </summary>
    public double GetTableauCoefficient(int row, int col)
    {
        return SimplexMatrix[row, col];
    }

    /// <summary>
    /// Checks if a variable at a given index is an integer variable.
    /// </summary>
    public bool IsIntegerVariable(int index)
    {
        return IntegerVariableIndices.Contains(index);
    }

    /// <summary>
    /// Checks if a value is fractional.
    /// </summary>
    public bool IsFractional(double value)
    {
        return IntegerTerm.IsFractional(value, IntegralityTolerance);
    }

    /// <summary>
    /// Gets the fractional part of a value.
    /// </summary>
    public static double GetFractionalPart(double value)
    {
        return IntegerTerm.GetFractionalPart(value);
    }

    /// <summary>
    /// Finds the basic variable for a given row (the variable with coefficient 1 in that row
    /// and 0 in all other rows for that column).
    /// </summary>
    public int? FindBasicVariable(int row)
    {
        for (int col = 0; col < NumberOfColumns - 1; col++)
        {
            double coeff = SimplexMatrix[row, col];
            if (Math.Abs(coeff - 1.0) < IntegralityTolerance)
            {
                // Check if this column is a unit vector with 1 in this row
                bool isUnit = true;
                for (int r = 0; r < NumberOfRows - 1; r++)
                {
                    if (r != row && Math.Abs(SimplexMatrix[r, col]) > IntegralityTolerance)
                    {
                        isUnit = false;
                        break;
                    }
                }
                if (isUnit)
                    return col;
            }
        }
        return null;
    }

    /// <summary>
    /// Gets all rows where an integer variable is basic but has a fractional value.
    /// These are the source rows for Gomory cuts.
    /// </summary>
    public IEnumerable<int> GetFractionalBasicRows()
    {
        for (int row = 0; row < NumberOfRows - 1; row++)
        {
            double rhs = GetRhs(row);
            if (!IsFractional(rhs))
                continue;

            int? basicVar = FindBasicVariable(row);
            if (basicVar.HasValue && IsIntegerVariable(basicVar.Value))
            {
                yield return row;
            }
        }
    }

    /// <summary>
    /// Creates a solution dictionary from the current solution.
    /// </summary>
    public IReadOnlyDictionary<string, double> GetSolutionDictionary()
    {
        return CurrentSolution.ToDictionary(
            t => t.TermName,
            t => t.Coefficient,
            StringComparer.OrdinalIgnoreCase);
    }
}
