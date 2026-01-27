using OptiCore.Models;

namespace OptiCore.BranchAndBound;

/// <summary>
/// Manages a pool of the best integer solutions found during Branch & Bound.
/// </summary>
public class SolutionPool
{
    private readonly List<(ModelResult Solution, double ObjectiveValue)> _solutions = new();
    private readonly int _maxSize;
    private readonly bool _isMaximization;

    /// <summary>
    /// Creates a new solution pool.
    /// </summary>
    /// <param name="maxSize">Maximum number of solutions to keep.</param>
    /// <param name="isMaximization">True if maximizing, false if minimizing.</param>
    public SolutionPool(int maxSize, bool isMaximization)
    {
        _maxSize = maxSize;
        _isMaximization = isMaximization;
    }

    /// <summary>
    /// Gets the current incumbent (best solution found).
    /// </summary>
    public ModelResult? Incumbent => _solutions.Count > 0 ? _solutions[0].Solution : null;

    /// <summary>
    /// Gets the objective value of the incumbent.
    /// </summary>
    public double? IncumbentValue => _solutions.Count > 0 ? _solutions[0].ObjectiveValue : null;

    /// <summary>
    /// Gets all solutions in the pool, ordered from best to worst.
    /// </summary>
    public IReadOnlyList<ModelResult> AllSolutions =>
        _solutions.Select(s => s.Solution).ToList();

    /// <summary>
    /// Gets the number of solutions in the pool.
    /// </summary>
    public int Count => _solutions.Count;

    /// <summary>
    /// Attempts to add a new solution to the pool.
    /// </summary>
    /// <param name="solution">The solution to add.</param>
    /// <param name="objectiveValue">The objective value of the solution.</param>
    /// <returns>True if the solution was added (improved incumbent or fits in pool).</returns>
    public bool TryAdd(ModelResult solution, double objectiveValue)
    {
        // Check if this solution improves the incumbent or can fit in the pool
        if (_solutions.Count == 0)
        {
            _solutions.Add((solution, objectiveValue));
            return true;
        }

        // Find the position where this solution should be inserted
        int insertPosition = FindInsertPosition(objectiveValue);

        // If it would go beyond the max size and doesn't improve any existing solution, reject it
        if (insertPosition >= _maxSize)
            return false;

        // Insert the solution
        _solutions.Insert(insertPosition, (solution, objectiveValue));

        // Remove excess solutions if over capacity
        while (_solutions.Count > _maxSize)
        {
            _solutions.RemoveAt(_solutions.Count - 1);
        }

        return true;
    }

    /// <summary>
    /// Checks if a bound can potentially improve the incumbent.
    /// </summary>
    /// <param name="bound">The LP bound to check.</param>
    /// <returns>True if the bound could lead to a better solution.</returns>
    public bool CanImprove(double bound)
    {
        if (!IncumbentValue.HasValue)
            return true;

        if (_isMaximization)
            return bound > IncumbentValue.Value;
        else
            return bound < IncumbentValue.Value;
    }

    /// <summary>
    /// Checks if a bound should be pruned (cannot improve incumbent).
    /// </summary>
    /// <param name="bound">The LP bound to check.</param>
    /// <param name="tolerance">The tolerance for comparison.</param>
    /// <returns>True if the bound should be pruned.</returns>
    public bool ShouldPrune(double bound, double tolerance = 1e-6)
    {
        if (!IncumbentValue.HasValue)
            return false;

        if (_isMaximization)
            return bound <= IncumbentValue.Value + tolerance;
        else
            return bound >= IncumbentValue.Value - tolerance;
    }

    private int FindInsertPosition(double objectiveValue)
    {
        for (int i = 0; i < _solutions.Count; i++)
        {
            bool isBetter = _isMaximization
                ? objectiveValue > _solutions[i].ObjectiveValue
                : objectiveValue < _solutions[i].ObjectiveValue;

            if (isBetter)
                return i;
        }

        return _solutions.Count;
    }

    /// <summary>
    /// Clears all solutions from the pool.
    /// </summary>
    public void Clear()
    {
        _solutions.Clear();
    }
}
