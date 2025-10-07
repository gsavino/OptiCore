using OptiCore.Enums;

namespace OptiCore.Models;

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

     public double[,] GetMatrix()
    {
        int rows = GetNumberOfConstrains() + 1;
        int cols = GetNumberOfConstrains() + GetNumberOfVariables() + 1;
        int numberOfVariables = GetNumberOfVariables();
        int numberOfConstrains = GetNumberOfConstrains();
        double[,] matrix = new double[rows, cols];

        // now I add all the constraints

        for (int i = 0; i < numberOfConstrains; i++)
        {
            for (int j = 0; j < numberOfVariables; j++)
            {
                matrix[i, j] = ConstraintsList[i].GetCoefficient(Variables[j].TermName);
            }
            for (int j = numberOfVariables; j < cols; j++)
            {
                if ((j - numberOfVariables) == i)
                {
                    matrix[i, j] = 1.0;
                }
                else
                {
                    matrix[i, j] = 0.0;
                }
            }
            matrix[i, cols - 1] = ConstraintsList[i].Rhs;
        }
        for (int j = 0; j < Variables.Count; j++)
        {
            matrix[rows - 1, j] = Objective.GetCoefficient(Variables[j].TermName) * -1;
        }
        matrix[rows - 1, cols - 1] = 0.0;
        return matrix;
    }
}