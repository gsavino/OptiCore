// OptiCore v1.2.5 - Linear model with support for <=, >=, and = constraints
using OptiCore.Enums;

namespace OptiCore.Models;

/// <summary>
/// Central model representation for a Linear Programming problem. Combines the model type (LP/ILP/MILP),
/// objective function, constraints, and variable declarations. The <see cref="GetMatrix"/> method converts
/// this declarative model into a simplex tableau matrix used by the solver. The matrix includes slack
/// variables for &lt;= constraints, surplus + artificial variables for &gt;= constraints, artificial
/// variables for = constraints, and uses the Big-M method to handle artificial variables.
/// </summary>
/// <param name="ModelKind">The type of model: LP, ILP, or MILP.</param>
/// <param name="Objective">The objective function defining the optimization goal and variable coefficients.</param>
/// <param name="ConstraintsList">The list of linear constraints in the model.</param>
/// <param name="Variables">The list of decision variables declared in the model.</param>
public record LinearModel(
    ModelType ModelKind,
    ModelObjective Objective,
    List<Constraint> ConstraintsList,
    List<Term> Variables
)
{
    /// <summary>
    /// Returns the count of decision variables in the model.
    /// </summary>
    /// <returns>The number of decision variables.</returns>
    public int GetNumberOfVariables() => Variables.Count;

    /// <summary>
    /// Returns the count of constraints in the model.
    /// </summary>
    /// <returns>The number of constraints.</returns>
    public int GetNumberOfConstrains() => ConstraintsList.Count;

    /// <summary>
    /// Checks whether a variable name exists in the model's variable list using case-insensitive comparison.
    /// </summary>
    /// <param name="variableName">The variable name to search for.</param>
    /// <returns><c>true</c> if the variable exists in the model; otherwise, <c>false</c>.</returns>
    public bool ValidateVariable(string variableName) =>
        Variables.Any(x => x.TermName.Equals(variableName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Verifies that every term in the objective and in each constraint references a declared
    /// model variable. Without this check, <see cref="ConstraintBase.GetCoefficient"/> silently
    /// returns 0 for unknown names, so a typo turns a real constraint into a vacuous one.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when one or more terms reference unknown variables.</exception>
    public void ValidateVariableReferences()
    {
        var known = new HashSet<string>(
            Variables.Select(v => v.TermName), StringComparer.OrdinalIgnoreCase);

        var unknown = new List<string>();

        foreach (var term in Objective.Coefficients)
        {
            if (!known.Contains(term.TermName))
            {
                unknown.Add($"'{term.TermName}' (objective)");
            }
        }

        foreach (var constraint in ConstraintsList)
        {
            foreach (var term in constraint.Coefficients)
            {
                if (!known.Contains(term.TermName))
                {
                    unknown.Add($"'{term.TermName}' (constraint '{constraint.ConstraintName}')");
                }
            }
        }

        if (unknown.Count > 0)
        {
            throw new ArgumentException(
                $"Unknown variable reference(s): {string.Join(", ", unknown.Distinct())}. " +
                "Every term must reference a variable declared in the model's Variables list.");
        }
    }

    /// <summary>
    /// Returns the constraint list normalized for the simplex tableau: any constraint with a
    /// negative RHS is multiplied by -1 and its operator flipped (&lt;= becomes &gt;= and vice
    /// versa; = stays =). The simplex assumes a feasible starting basis with non-negative RHS,
    /// so both <see cref="GetMatrix"/> and the solver's basis bookkeeping must work from this
    /// list, never from the raw <see cref="ConstraintsList"/>.
    /// </summary>
    public List<Constraint> GetNormalizedConstraints()
    {
        var normalized = new List<Constraint>(ConstraintsList.Count);

        foreach (var constraint in ConstraintsList)
        {
            if (constraint.Rhs >= 0)
            {
                normalized.Add(constraint);
                continue;
            }

            string op = constraint.Operator.Trim();
            string flippedOp = op switch
            {
                "<=" or "≤" => ">=",
                ">=" or "≥" => "<=",
                _ => op
            };

            var negatedCoefficients = constraint.Coefficients
                .Select(t => new Term(t.TermName, -t.Coefficient))
                .ToList();

            normalized.Add(new Constraint(
                constraint.ConstraintName, negatedCoefficients, flippedOp, -constraint.Rhs));
        }

        return normalized;
    }

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
        ValidateVariableReferences();

        int numberOfVariables = GetNumberOfVariables();
        int numberOfConstraints = GetNumberOfConstrains();

        var normalizedConstraints = GetNormalizedConstraints();

        // Count slack and artificial variables needed
        int slackCount = 0;
        int artificialCount = 0;

        foreach (var constraint in normalizedConstraints)
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

        // Big-M value for artificial variable penalties. M must dominate every objective
        // coefficient by a wide margin or artificials cannot be driven out of the basis
        // and feasible models are misreported as infeasible - so scale it with the data
        // instead of hardcoding it.
        double maxAbsObjectiveCoeff = 1.0;
        foreach (var term in Objective.Coefficients)
        {
            maxAbsObjectiveCoeff = Math.Max(maxAbsObjectiveCoeff, Math.Abs(term.Coefficient));
        }
        double BigM = Math.Max(1e6, 1e4 * maxAbsObjectiveCoeff);

        int slackIndex = numberOfVariables;
        int artificialIndex = numberOfVariables + slackCount;

        // Build constraint rows
        for (int i = 0; i < numberOfConstraints; i++)
        {
            var constraint = normalizedConstraints[i];
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
            string op = normalizedConstraints[i].Operator.Trim();

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
