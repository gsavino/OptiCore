using OptiCore.BranchAndBound;
using OptiCore.Cuts;
using OptiCore.Enums;

namespace OptiCore.Models;

/// <summary>
/// Mutable wrapper around LinearModel for efficient cut addition/removal during Branch & Cut.
/// Provides a working copy that can be modified without affecting the original model.
/// </summary>
public class WorkingModel
{
    private readonly LinearModel _baseModel;
    private readonly List<Cut> _activeCuts = new();
    private readonly Dictionary<string, (double? Lower, double? Upper)> _variableBounds = new();

    /// <summary>
    /// Creates a new working model from a base linear model.
    /// </summary>
    /// <param name="baseModel">The base model to wrap.</param>
    public WorkingModel(LinearModel baseModel)
    {
        _baseModel = baseModel ?? throw new ArgumentNullException(nameof(baseModel));
    }

    /// <summary>
    /// Gets the base linear model.
    /// </summary>
    public LinearModel BaseModel => _baseModel;

    /// <summary>
    /// Gets the active cuts.
    /// </summary>
    public IReadOnlyList<Cut> ActiveCuts => _activeCuts;

    /// <summary>
    /// Gets the number of active cuts.
    /// </summary>
    public int CutCount => _activeCuts.Count;

    /// <summary>
    /// Gets the variable bounds applied to this working model.
    /// </summary>
    public IReadOnlyDictionary<string, (double? Lower, double? Upper)> VariableBounds =>
        _variableBounds;

    /// <summary>
    /// Adds a cut to the working model.
    /// </summary>
    /// <param name="cut">The cut to add.</param>
    /// <returns>True if the cut was added (not a duplicate).</returns>
    public bool AddCut(Cut cut)
    {
        if (_activeCuts.Any(c => c.CutHash == cut.CutHash))
            return false;

        _activeCuts.Add(cut);
        return true;
    }

    /// <summary>
    /// Adds multiple cuts to the working model.
    /// </summary>
    /// <param name="cuts">The cuts to add.</param>
    /// <returns>The number of cuts actually added.</returns>
    public int AddCuts(IEnumerable<Cut> cuts)
    {
        int added = 0;
        foreach (var cut in cuts)
        {
            if (AddCut(cut))
                added++;
        }
        return added;
    }

    /// <summary>
    /// Removes a cut from the working model.
    /// </summary>
    /// <param name="cut">The cut to remove.</param>
    /// <returns>True if the cut was found and removed.</returns>
    public bool RemoveCut(Cut cut)
    {
        return _activeCuts.RemoveAll(c => c.CutHash == cut.CutHash) > 0;
    }

    /// <summary>
    /// Removes all cuts from the working model.
    /// </summary>
    public void ClearCuts()
    {
        _activeCuts.Clear();
    }

    /// <summary>
    /// Sets a variable bound.
    /// </summary>
    /// <param name="variableName">The variable name.</param>
    /// <param name="lowerBound">The lower bound (null to not change).</param>
    /// <param name="upperBound">The upper bound (null to not change).</param>
    public void SetVariableBound(string variableName, double? lowerBound, double? upperBound)
    {
        if (_variableBounds.TryGetValue(variableName, out var existing))
        {
            _variableBounds[variableName] = (
                lowerBound ?? existing.Lower,
                upperBound ?? existing.Upper
            );
        }
        else
        {
            _variableBounds[variableName] = (lowerBound, upperBound);
        }
    }

    /// <summary>
    /// Applies variable bounds from a branch node.
    /// </summary>
    /// <param name="bounds">The variable bounds from the branch node.</param>
    public void ApplyBranchBounds(IEnumerable<VariableBound> bounds)
    {
        foreach (var bound in bounds)
        {
            SetVariableBound(bound.VariableName, bound.LowerBound, bound.UpperBound);
        }
    }

    /// <summary>
    /// Clears all variable bounds.
    /// </summary>
    public void ClearVariableBounds()
    {
        _variableBounds.Clear();
    }

    /// <summary>
    /// Creates an immutable LinearModel snapshot for solving.
    /// </summary>
    /// <returns>A new LinearModel with all cuts and bounds applied.</returns>
    public LinearModel BuildLinearModel()
    {
        var constraints = new List<Constraint>(_baseModel.ConstraintsList);

        // Add cut constraints
        foreach (var cut in _activeCuts)
        {
            constraints.Add(cut.ToConstraint());
        }

        // Add bound constraints
        foreach (var (varName, bounds) in _variableBounds)
        {
            if (bounds.Upper.HasValue)
            {
                constraints.Add(new Constraint(
                    ConstraintName: $"_bound_{varName}_ub",
                    Coefficients: new List<Term> { new Term(varName, 1.0) },
                    Operator: "<=",
                    Rhs: bounds.Upper.Value
                ));
            }

            if (bounds.Lower.HasValue)
            {
                // x >= lb  =>  -x <= -lb
                constraints.Add(new Constraint(
                    ConstraintName: $"_bound_{varName}_lb",
                    Coefficients: new List<Term> { new Term(varName, -1.0) },
                    Operator: "<=",
                    Rhs: -bounds.Lower.Value
                ));
            }
        }

        return new LinearModel(
            ModelKind: _baseModel.ModelKind,
            Objective: _baseModel.Objective,
            ConstraintsList: constraints,
            Variables: _baseModel.Variables
        );
    }

    /// <summary>
    /// Creates a copy of this working model with the same state.
    /// </summary>
    /// <returns>A new WorkingModel with copied state.</returns>
    public WorkingModel Clone()
    {
        var clone = new WorkingModel(_baseModel);
        clone._activeCuts.AddRange(_activeCuts);
        foreach (var (key, value) in _variableBounds)
        {
            clone._variableBounds[key] = value;
        }
        return clone;
    }

    /// <summary>
    /// Creates a child working model for branching (inherits cuts, not bounds).
    /// </summary>
    /// <returns>A new WorkingModel with inherited cuts.</returns>
    public WorkingModel CreateChildForBranching()
    {
        var child = new WorkingModel(_baseModel);
        child._activeCuts.AddRange(_activeCuts);
        return child;
    }
}
