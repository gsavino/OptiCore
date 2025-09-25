using OptiCore.Enums;

namespace OptiCore.Models;

public record Model(
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
}