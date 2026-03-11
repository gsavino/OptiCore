// OptiCore v1.2.4 - Simplex solver with Big-M method support - core file
using OptiCore.Enums;
using OptiCore.Models;

namespace OptiCore.Solver;

/// <summary>
/// Simplex algorithm implementation for solving Linear Programming problems.
/// Supports maximization and minimization with Big-M method for artificial variables.
/// </summary>
public class OptiCoreSimplex
{
    public double[,] SimplexMatrix { get; set; }
    public int MaxRows { get; set; }
    public int MaxCols { get; set; }
    public int NumberOfVariables { get; set; }
    public LinearModel MyLinearModel { get; set; }

    private bool IsSolved { get; set; }
    private ModelResult _result { get; set; }
    private bool _isUnbounded { get; set; }
    private bool _isInfeasible { get; set; }

    // Track where artificial variables start in the matrix
    private int _artificialStartIndex;
    private int _artificialCount;

    public OptiCoreSimplex(LinearModel myModel)
    {
        MyLinearModel = myModel;
        SimplexMatrix = myModel.GetMatrix();
        MaxRows = SimplexMatrix.GetLength(0);
        MaxCols = SimplexMatrix.GetLength(1);
        NumberOfVariables = myModel.GetNumberOfVariables();
        IsSolved = false;
        _isUnbounded = false;
        _isInfeasible = false;
        _result = new ModelResult();

        // Calculate artificial variable positions
        CalculateArtificialVariableInfo();
    }

    private void CalculateArtificialVariableInfo()
    {
        int slackCount = 0;
        _artificialCount = 0;

        foreach (var constraint in MyLinearModel.ConstraintsList)
        {
            string op = constraint.Operator.Trim();
            if (op == "<=" || op == "≤")
            {
                slackCount++;
            }
            else if (op == ">=" || op == "≥")
            {
                slackCount++;
                _artificialCount++;
            }
            else if (op == "=" || op == "==")
            {
                _artificialCount++;
            }
        }

        _artificialStartIndex = NumberOfVariables + slackCount;
    }

    public ModelResult GetOptimalValues()
    {
        if (!IsSolved)
        {
            SolveSimplex();

            if (_isInfeasible)
            {
                _result.OptimalResult = double.NaN;
                // Return empty terms or terms with NaN
                for (int v = 0; v < NumberOfVariables; v++)
                {
                    _result.Terms.Add(new Term(MyLinearModel.Variables[v].TermName, double.NaN));
                }
            }
            else if (_isUnbounded)
            {
                _result.OptimalResult = MyLinearModel.Objective.Goal == ObjectiveType.MAX
                    ? double.PositiveInfinity
                    : double.NegativeInfinity;

                for (int v = 0; v < NumberOfVariables; v++)
                {
                    _result.Terms.Add(new Term(MyLinearModel.Variables[v].TermName, double.NaN));
                }
            }
            else
            {
                ExtractSolution();
            }

            IsSolved = true;
        }
        return _result;
    }

    /// <summary>
    /// Checks if the solution is infeasible (artificial variables in basis with non-zero values).
    /// </summary>
    public bool IsInfeasible => _isInfeasible;

    /// <summary>
    /// Checks if the solution is unbounded.
    /// </summary>
    public bool IsUnbounded => _isUnbounded;

    /// <summary>
    /// Extracts variable values from the final simplex tableau.
    /// A variable is basic if its column is a unit vector (exactly one 1, rest 0s).
    /// Basic variable value = RHS of the row where the 1 appears.
    /// Non-basic variable value = 0.
    /// </summary>
    private void ExtractSolution()
    {
        const double tolerance = 1e-9;

        for (int v = 0; v < NumberOfVariables; v++)
        {
            string varName = MyLinearModel.Variables[v].TermName;
            double value = 0;

            // Check if this variable is basic (unit vector in its column)
            int basicRow = -1;
            bool isBasic = true;

            for (int row = 0; row < MaxRows - 1; row++) // Exclude objective row
            {
                double coeff = SimplexMatrix[row, v];

                if (Math.Abs(coeff - 1.0) < tolerance)
                {
                    if (basicRow == -1)
                    {
                        basicRow = row;
                    }
                    else
                    {
                        // More than one 1 in the column - not a unit vector
                        isBasic = false;
                        break;
                    }
                }
                else if (Math.Abs(coeff) > tolerance)
                {
                    // Non-zero value that's not 1 - not a unit vector
                    isBasic = false;
                    break;
                }
            }

            if (isBasic && basicRow >= 0)
            {
                // Basic variable: read value from RHS
                value = SimplexMatrix[basicRow, MaxCols - 1];

                // Handle floating point precision - round very small values to 0
                if (Math.Abs(value) < tolerance)
                {
                    value = 0;
                }
            }
            // else: non-basic variable, value stays 0

            _result.Terms.Add(new Term(varName, value));
        }

        // Optimal objective value is in the RHS of the objective row
        double optimalValue = SimplexMatrix[MaxRows - 1, MaxCols - 1];

        // For minimization, we need to negate the result since we store the
        // objective coefficients directly (not negated) in GetMatrix()
        if (MyLinearModel.Objective.Goal == ObjectiveType.MIN)
        {
            optimalValue = -optimalValue;
        }

        _result.OptimalResult = optimalValue;
    }

    private void SolveSimplex()
    {
        int maxIterations = 1000;
        int iteration = 0;

        while (iteration < maxIterations)
        {
            // Find entering variable (most negative reduced cost)
            int pivotColumn = GetPivotColumn();

            if (pivotColumn == -1)
            {
                // No negative reduced costs - optimal solution found
                // Check if artificial variables are still in the basis with non-zero values
                if (!CheckFeasibility())
                {
                    _isInfeasible = true;
                }
                break;
            }

            // Find leaving variable (minimum ratio test)
            int pivotRow = GetPivotRow(pivotColumn);

            if (pivotRow == -1)
            {
                // No valid pivot row - problem is unbounded
                _isUnbounded = true;
                break;
            }

            // Perform pivot operation
            Pivot(pivotRow, pivotColumn);

            iteration++;
        }
    }

    /// <summary>
    /// Checks if the solution is feasible by verifying artificial variables
    /// are not in the basis with non-zero values.
    /// </summary>
    private bool CheckFeasibility()
    {
        if (_artificialCount == 0) return true;

        const double tolerance = 1e-6;

        for (int a = 0; a < _artificialCount; a++)
        {
            int col = _artificialStartIndex + a;

            // Check if this artificial variable is basic
            int basicRow = -1;
            bool isBasic = true;

            for (int row = 0; row < MaxRows - 1; row++)
            {
                double coeff = SimplexMatrix[row, col];

                if (Math.Abs(coeff - 1.0) < tolerance)
                {
                    if (basicRow == -1)
                    {
                        basicRow = row;
                    }
                    else
                    {
                        isBasic = false;
                        break;
                    }
                }
                else if (Math.Abs(coeff) > tolerance)
                {
                    isBasic = false;
                    break;
                }
            }

            if (isBasic && basicRow >= 0)
            {
                // Artificial variable is basic - check its value
                double value = SimplexMatrix[basicRow, MaxCols - 1];

                if (Math.Abs(value) > tolerance)
                {
                    // Artificial variable has non-zero value - infeasible
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Finds the entering variable by selecting the column with the most negative
    /// reduced cost in the objective row (for maximization).
    /// Returns -1 if all reduced costs are non-negative (optimal).
    /// </summary>
    private int GetPivotColumn()
    {
        const double tolerance = -1e-9;
        int pivotColumn = -1;
        double minValue = tolerance; // Only consider values < 0

        // Check all columns except RHS
        for (int col = 0; col < MaxCols - 1; col++)
        {
            double reducedCost = SimplexMatrix[MaxRows - 1, col];
            if (reducedCost < minValue)
            {
                minValue = reducedCost;
                pivotColumn = col;
            }
        }

        return pivotColumn;
    }

    /// <summary>
    /// Finds the leaving variable using the minimum ratio test.
    /// Only considers rows where the pivot column value is strictly positive.
    /// Returns -1 if no valid pivot row exists (unbounded).
    /// </summary>
    private int GetPivotRow(int pivotColumn)
    {
        const double tolerance = 1e-9;
        int pivotRow = -1;
        double minRatio = double.MaxValue;

        for (int row = 0; row < MaxRows - 1; row++) // Exclude objective row
        {
            double pivotColValue = SimplexMatrix[row, pivotColumn];

            if (pivotColValue > tolerance) // Only positive values
            {
                double rhs = SimplexMatrix[row, MaxCols - 1];

                // RHS should be non-negative in a valid tableau
                if (rhs >= -tolerance)
                {
                    double ratio = rhs / pivotColValue;

                    if (ratio < minRatio)
                    {
                        minRatio = ratio;
                        pivotRow = row;
                    }
                }
            }
        }

        return pivotRow;
    }

    /// <summary>
    /// Performs the pivot operation on the simplex tableau.
    /// </summary>
    private void Pivot(int pivotRow, int pivotColumn)
    {
        double pivotElement = SimplexMatrix[pivotRow, pivotColumn];

        // Step 1: Divide pivot row by pivot element to make pivot = 1
        for (int col = 0; col < MaxCols; col++)
        {
            SimplexMatrix[pivotRow, col] /= pivotElement;
        }

        // Step 2: Eliminate pivot column in all other rows (including objective row)
        for (int row = 0; row < MaxRows; row++)
        {
            if (row == pivotRow) continue;

            double factor = SimplexMatrix[row, pivotColumn];

            if (Math.Abs(factor) > 1e-12) // Only process if factor is non-zero
            {
                for (int col = 0; col < MaxCols; col++)
                {
                    SimplexMatrix[row, col] -= factor * SimplexMatrix[pivotRow, col];
                }
            }
        }
    }
}
