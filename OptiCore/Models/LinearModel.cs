using OptiCore.Enums;

namespace OptiCore.Models;

/// <summary>
/// Represents a Linear Programming model with objective function and constraints.
/// </summary>
public record LinearModel(
    ModelType ModelKind,
    ModelObjective Objective,
    List<Constraint> ConstraintsList,
    List<Term> Variables
)
{
    public int GetNumberOfVariables() => Variables.Count;
    public int GetNumberOfConstrains() => ConstraintsList.Count;

    public bool ValidateVariable(string variableName) =>
        Variables.Any(x => x.TermName.Equals(variableName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Builds the simplex tableau matrix.
    ///
    /// Matrix structure:
    /// [constraint coefficients | slack/surplus variables | artificial variables | RHS]
    /// [objective coefficients  | 0s for slack           | Big-M penalties      | 0  ]
    ///
    /// Constraint handling:
    /// - <= : adds slack variable (+1)
    /// - >= : adds surplus variable (-1) and artificial variable (+1)
    /// - =  : adds artificial variable (+1)
    /// </summary>
    public double[,] GetMatrix()
    {
        int numberOfVariables = GetNumberOfVariables();
        int numberOfConstraints = GetNumberOfConstrains();

        // Count slack and artificial variables needed
        int slackCount = 0;
        int artificialCount = 0;

        foreach (var constraint in ConstraintsList)
        {
            string op = constraint.Operator.Trim();
            if (op == "<=" || op == "≤")
            {
                slackCount++;
            }
            else if (op == ">=" || op == "≥")
            {
                slackCount++;      // surplus variable
                artificialCount++; // artificial variable
            }
            else if (op == "=" || op == "==")
            {
                artificialCount++; // artificial variable only
            }
        }

        int totalSlackAndArtificial = slackCount + artificialCount;
        int rows = numberOfConstraints + 1; // constraints + objective
        int cols = numberOfVariables + totalSlackAndArtificial + 1; // variables + slack/artificial + RHS

        double[,] matrix = new double[rows, cols];

        // Big-M value for artificial variable penalties
        const double BigM = 1e6;

        int slackIndex = numberOfVariables;
        int artificialIndex = numberOfVariables + slackCount;

        // Build constraint rows
        for (int i = 0; i < numberOfConstraints; i++)
        {
            var constraint = ConstraintsList[i];
            string op = constraint.Operator.Trim();

            // Add decision variable coefficients
            for (int j = 0; j < numberOfVariables; j++)
            {
                matrix[i, j] = constraint.GetCoefficient(Variables[j].TermName);
            }

            // Handle different constraint types
            if (op == "<=" || op == "≤")
            {
                // Add slack variable with coefficient +1
                matrix[i, slackIndex] = 1.0;
                slackIndex++;
                matrix[i, cols - 1] = constraint.Rhs;
            }
            else if (op == ">=" || op == "≥")
            {
                // Add surplus variable with coefficient -1
                matrix[i, slackIndex] = -1.0;
                slackIndex++;

                // Add artificial variable with coefficient +1
                matrix[i, artificialIndex] = 1.0;
                artificialIndex++;

                matrix[i, cols - 1] = constraint.Rhs;
            }
            else if (op == "=" || op == "==")
            {
                // Add artificial variable with coefficient +1
                matrix[i, artificialIndex] = 1.0;
                artificialIndex++;

                matrix[i, cols - 1] = constraint.Rhs;
            }
        }

        // Build objective row
        int objRow = rows - 1;

        // Add decision variable coefficients (negated for maximization)
        for (int j = 0; j < numberOfVariables; j++)
        {
            double coeff = Objective.GetCoefficient(Variables[j].TermName);

            if (Objective.Goal == ObjectiveType.MAX)
            {
                matrix[objRow, j] = -coeff;
            }
            else // MIN
            {
                matrix[objRow, j] = coeff;
            }
        }

        // Add Big-M penalties for artificial variables
        // For MAX: we want to minimize artificial variables, so add +M in the objective row
        // But since we negate for MAX, the setup is: we want artificial = 0
        // In the transformed objective row, artificial variables should have large positive coefficients
        int artIdx = numberOfVariables + slackCount;
        for (int a = 0; a < artificialCount; a++)
        {
            if (Objective.Goal == ObjectiveType.MAX)
            {
                // For max, artificial variables should have +M (to be driven out)
                matrix[objRow, artIdx + a] = BigM;
            }
            else
            {
                // For min, artificial variables should have +M
                matrix[objRow, artIdx + a] = BigM;
            }
        }

        // Initialize RHS of objective row to 0
        matrix[objRow, cols - 1] = 0.0;

        // For Big-M method: we need to eliminate artificial variables from the objective row
        // by subtracting M * (artificial variable's row) from the objective row
        // This ensures the initial tableau is in proper form
        artIdx = numberOfVariables + slackCount;
        int artCounter = 0;

        for (int i = 0; i < numberOfConstraints; i++)
        {
            string op = ConstraintsList[i].Operator.Trim();

            if (op == ">=" || op == "≥" || op == "=" || op == "==")
            {
                // This constraint has an artificial variable - eliminate it from objective row
                double factor = matrix[objRow, artIdx + artCounter];

                for (int col = 0; col < cols; col++)
                {
                    matrix[objRow, col] -= factor * matrix[i, col];
                }

                artCounter++;
            }
        }

        return matrix;
    }
}
