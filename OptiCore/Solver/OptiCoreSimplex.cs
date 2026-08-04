// OptiCore v1.2.5 - Simplex solver with Big-M method support - core file
using OptiCore.Enums;
using OptiCore.Models;

namespace OptiCore.Solver;

/// <summary>
/// Core simplex method implementation for solving Linear Programming (LP) problems.
/// Operates on a tableau matrix where each row represents a constraint and the last row
/// is the objective function. The algorithm iteratively selects entering variables (most
/// negative reduced cost) and leaving variables (minimum ratio test), performing pivot
/// operations to improve the solution until optimality is reached. Supports the Big-M
/// method for handling >= and = constraints via artificial variables.
/// </summary>
public class OptiCoreSimplex
{
    /// <summary>
    /// The simplex tableau matrix. Rows = constraints + 1 (objective row).
    /// Columns = decision variables + slack/surplus + artificial + RHS.
    /// </summary>
    public double[,] SimplexMatrix { get; set; }

    /// <summary>
    /// Number of rows in the tableau matrix (constraints + objective row).
    /// </summary>
    public int MaxRows { get; set; }

    /// <summary>
    /// Number of columns in the tableau matrix (all variables + RHS).
    /// </summary>
    public int MaxCols { get; set; }

    /// <summary>
    /// Count of original decision variables (excludes slack/surplus and artificial variables).
    /// </summary>
    public int NumberOfVariables { get; set; }

    /// <summary>
    /// The linear model being solved.
    /// </summary>
    public LinearModel MyLinearModel { get; set; }

    private bool IsSolved { get; set; }
    private ModelResult _result { get; set; }
    private bool _isUnbounded { get; set; }
    private bool _isInfeasible { get; set; }
    private bool _isNotConverged { get; set; }

    /// <summary>
    /// For each constraint row, the column index of the variable currently basic in that row.
    /// Updated on every pivot. This is the source of truth for basicness: inferring it from
    /// column shape is ambiguous because two columns can hold identical unit vectors
    /// (e.g. a single-row variable and the artificial variable of its >= row).
    /// </summary>
    private int[] _basisVariable = [];

    /// <summary>
    /// Column index where artificial variables start in the tableau.
    /// </summary>
    private int _artificialStartIndex;

    /// <summary>
    /// Total number of artificial variables in the tableau.
    /// </summary>
    private int _artificialCount;

    /// <summary>
    /// Initializes the simplex solver by building the tableau matrix from the linear model
    /// and calculating artificial variable positions.
    /// </summary>
    /// <param name="myModel">The linear programming model to solve.</param>
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

    /// <summary>
    /// Counts slack and artificial variables needed based on constraint types.
    /// Determines where artificial variable columns start in the tableau.
    /// &lt;= constraints need one slack variable; >= constraints need one slack and one artificial;
    /// = constraints need one artificial variable.
    /// </summary>
    private void CalculateArtificialVariableInfo()
    {
        int slackCount = 0;
        _artificialCount = 0;

        // Must mirror GetMatrix, which works on the normalized constraints
        // (negative-RHS rows are flipped and may change slack/artificial layout).
        var constraints = MyLinearModel.GetNormalizedConstraints();

        foreach (var constraint in constraints)
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

        InitializeBasis(constraints);
    }

    /// <summary>
    /// Records the initial basic variable of each constraint row, mirroring the column
    /// layout produced by <see cref="LinearModel.GetMatrix"/>: the slack for &lt;= rows
    /// and the artificial for &gt;= and = rows.
    /// </summary>
    /// <param name="normalizedConstraints">The normalized constraint list used to build the tableau.</param>
    private void InitializeBasis(List<Constraint> normalizedConstraints)
    {
        _basisVariable = new int[MaxRows - 1];

        int slackIndex = NumberOfVariables;
        int artificialIndex = _artificialStartIndex;

        for (int i = 0; i < normalizedConstraints.Count; i++)
        {
            string op = normalizedConstraints[i].Operator.Trim();
            if (op == "<=" || op == "≤")
            {
                _basisVariable[i] = slackIndex;
                slackIndex++;
            }
            else if (op == ">=" || op == "≥")
            {
                slackIndex++; // surplus variable, starts non-basic
                _basisVariable[i] = artificialIndex;
                artificialIndex++;
            }
            else if (op == "=" || op == "==")
            {
                _basisVariable[i] = artificialIndex;
                artificialIndex++;
            }
        }
    }

    /// <summary>
    /// Main entry point. Runs the simplex algorithm if not already solved, then extracts
    /// and returns the solution (variable values + optimal Z). Handles infeasible cases
    /// (returns NaN) and unbounded cases (returns +/- Infinity).
    /// </summary>
    /// <returns>A <see cref="ModelResult"/> containing the optimal variable values and objective value.</returns>
    public ModelResult GetOptimalValues()
    {
        if (!IsSolved)
        {
            SolveSimplex();

            if (_isInfeasible || _isNotConverged)
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
    /// Checks if the solver exhausted its iteration budget without reaching optimality
    /// (e.g. due to cycling). The result is NaN in that case, never a partial solution.
    /// </summary>
    public bool IsNotConverged => _isNotConverged;

    /// <summary>
    /// For each constraint row, the column index of the variable basic in that row.
    /// Reflects the current tableau state (final basis after solving). Consumers such as
    /// cut generators must use this instead of inferring basicness from column shape.
    /// </summary>
    public IReadOnlyList<int> BasisVariables => _basisVariable;

    /// <summary>
    /// After solving, reads variable values from the final tableau. A variable is basic
    /// (in the solution) if its column is a unit vector (exactly one 1, rest 0s).
    /// The value is read from the RHS of the row containing the 1. Non-basic variables
    /// have value 0. The optimal objective value is read from the RHS of the objective row.
    /// </summary>
    private void ExtractSolution()
    {
        const double tolerance = 1e-9;

        // Map each decision variable to its value: RHS of the row where it is basic,
        // 0 if non-basic. The explicit basis avoids misreading columns that happen
        // to look like unit vectors in the final tableau.
        double[] values = new double[NumberOfVariables];

        for (int row = 0; row < MaxRows - 1; row++) // Exclude objective row
        {
            int basicVar = _basisVariable[row];

            if (basicVar < NumberOfVariables)
            {
                double value = SimplexMatrix[row, MaxCols - 1];

                // Handle floating point precision - round very small values to 0
                if (Math.Abs(value) < tolerance)
                {
                    value = 0;
                }

                values[basicVar] = value;
            }
        }

        for (int v = 0; v < NumberOfVariables; v++)
        {
            _result.Terms.Add(new Term(MyLinearModel.Variables[v].TermName, values[v]));
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

    /// <summary>
    /// The main simplex iteration loop. Repeats: find pivot column (entering variable),
    /// find pivot row (leaving variable), perform pivot. Stops when no negative reduced
    /// costs remain (optimal) or no valid pivot row exists (unbounded). After reaching
    /// optimality, checks feasibility to ensure no artificial variables remain in the basis.
    /// </summary>
    private void SolveSimplex()
    {
        // Scale the iteration budget with problem size; a fixed cap silently truncates
        // large models. Exhausting it means the solve failed and must be signaled.
        int maxIterations = 50 * (MaxRows + MaxCols);
        int iteration = 0;

        while (true)
        {
            if (iteration >= maxIterations)
            {
                _isNotConverged = true;
                break;
            }

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
    /// After optimization, verifies that no artificial variables remain in the basis
    /// with non-zero values, which would indicate the original problem is infeasible.
    /// </summary>
    /// <returns>True if the solution is feasible (no artificial variables in basis with non-zero values); false otherwise.</returns>
    private bool CheckFeasibility()
    {
        if (_artificialCount == 0) return true;

        const double tolerance = 1e-6;

        for (int row = 0; row < MaxRows - 1; row++)
        {
            if (_basisVariable[row] < _artificialStartIndex) continue;

            // An artificial variable is basic in this row - check its value
            double value = SimplexMatrix[row, MaxCols - 1];

            if (Math.Abs(value) > tolerance)
            {
                // Artificial variable has non-zero value - infeasible
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Selects the entering variable by finding the column with the most negative
    /// coefficient in the objective row. Returns -1 when all coefficients are
    /// non-negative, indicating the current solution is optimal.
    /// </summary>
    /// <returns>The column index of the entering variable, or -1 if optimal.</returns>
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
    /// Selects the leaving variable using the minimum ratio test (RHS / pivot column value).
    /// Only considers positive pivot column values to maintain feasibility.
    /// Returns -1 if no valid pivot row exists, indicating the problem is unbounded.
    /// </summary>
    /// <param name="pivotColumn">The column index of the entering variable.</param>
    /// <returns>The row index of the leaving variable, or -1 if unbounded.</returns>
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
    /// Performs the pivot operation on the simplex tableau:
    /// (1) divides the pivot row by the pivot element to make it 1,
    /// (2) eliminates the pivot column in all other rows (including the objective row)
    /// via row operations so that the pivot column becomes a unit vector.
    /// </summary>
    /// <param name="pivotRow">The row index of the leaving variable.</param>
    /// <param name="pivotColumn">The column index of the entering variable.</param>
    private void Pivot(int pivotRow, int pivotColumn)
    {
        double pivotElement = SimplexMatrix[pivotRow, pivotColumn];

        // The entering variable becomes the basic variable of the pivot row
        _basisVariable[pivotRow] = pivotColumn;

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
