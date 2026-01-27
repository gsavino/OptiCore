using OptiCore.Models;

namespace OptiCore.Cuts;

/// <summary>
/// Manages a pool of cutting planes with deduplication, aging, and activity tracking.
/// </summary>
public class CutPool
{
    private readonly Dictionary<int, Cut> _cuts = new();
    private readonly int _maxSize;
    private readonly int _maxAge;
    private int _currentRound;

    /// <summary>
    /// Creates a new cut pool.
    /// </summary>
    /// <param name="maxSize">Maximum number of cuts to store (default: 1000).</param>
    /// <param name="maxAge">Maximum age before a cut becomes stale (default: 100 rounds).</param>
    public CutPool(int maxSize = 1000, int maxAge = 100)
    {
        _maxSize = maxSize;
        _maxAge = maxAge;
        _currentRound = 0;
    }

    /// <summary>
    /// Gets the number of cuts in the pool.
    /// </summary>
    public int Count => _cuts.Count;

    /// <summary>
    /// Gets all cuts in the pool.
    /// </summary>
    public IReadOnlyCollection<Cut> AllCuts => _cuts.Values;

    /// <summary>
    /// Advances the round counter (call at start of each cut generation round).
    /// </summary>
    public void AdvanceRound()
    {
        _currentRound++;
    }

    /// <summary>
    /// Attempts to add a cut to the pool.
    /// </summary>
    /// <param name="cut">The cut to add.</param>
    /// <returns>True if the cut was added (not a duplicate).</returns>
    public bool TryAdd(Cut cut)
    {
        if (_cuts.ContainsKey(cut.CutHash))
            return false;

        // If pool is full, try to make room
        if (_cuts.Count >= _maxSize)
        {
            if (!PurgeStaleCuts(1))
            {
                // Remove lowest score cut if can't purge stale
                RemoveLowestScoreCut();
            }
        }

        _cuts[cut.CutHash] = cut;
        return true;
    }

    /// <summary>
    /// Tries to add multiple cuts to the pool.
    /// </summary>
    /// <param name="cuts">The cuts to add.</param>
    /// <returns>The number of cuts actually added.</returns>
    public int TryAddRange(IEnumerable<Cut> cuts)
    {
        int added = 0;
        foreach (var cut in cuts)
        {
            if (TryAdd(cut))
                added++;
        }
        return added;
    }

    /// <summary>
    /// Checks if a cut with the same hash already exists.
    /// </summary>
    public bool Contains(Cut cut)
    {
        return _cuts.ContainsKey(cut.CutHash);
    }

    /// <summary>
    /// Selects violated cuts from the pool for a given solution.
    /// </summary>
    /// <param name="solution">The current LP solution.</param>
    /// <param name="maxCuts">Maximum number of cuts to select.</param>
    /// <param name="minViolation">Minimum violation to be considered.</param>
    /// <returns>The selected violated cuts, ordered by violation.</returns>
    public IReadOnlyList<Cut> SelectViolatedCuts(
        IEnumerable<Term> solution,
        int maxCuts = 10,
        double minViolation = 1e-6)
    {
        var solutionDict = solution.ToDictionary(
            t => t.TermName,
            t => t.Coefficient,
            StringComparer.OrdinalIgnoreCase);

        var violatedCuts = new List<(Cut cut, double violation, double score)>();

        foreach (var cut in _cuts.Values)
        {
            double violation = cut.GetViolation(solutionDict);
            if (violation > minViolation)
            {
                double score = ComputeCutScore(cut, violation);
                violatedCuts.Add((cut, violation, score));
            }
        }

        // Select top cuts by score
        var selected = violatedCuts
            .OrderByDescending(x => x.score)
            .Take(maxCuts)
            .Select(x =>
            {
                x.cut.ActivityCount++;
                return x.cut;
            })
            .ToList();

        return selected;
    }

    /// <summary>
    /// Removes stale cuts (old with low activity).
    /// </summary>
    /// <param name="minToRemove">Minimum number of cuts to remove.</param>
    /// <returns>True if at least minToRemove cuts were removed.</returns>
    public bool PurgeStaleCuts(int minToRemove = 0)
    {
        var staleCuts = _cuts.Values
            .Where(c => (_currentRound - c.GenerationRound) > _maxAge)
            .Where(c => c.ActivityCount < (_currentRound - c.GenerationRound) / 10)
            .Select(c => c.CutHash)
            .ToList();

        foreach (var hash in staleCuts)
        {
            _cuts.Remove(hash);
        }

        return staleCuts.Count >= minToRemove;
    }

    /// <summary>
    /// Clears all cuts from the pool.
    /// </summary>
    public void Clear()
    {
        _cuts.Clear();
        _currentRound = 0;
    }

    /// <summary>
    /// Gets a cut by its hash.
    /// </summary>
    public Cut? GetByHash(int hash)
    {
        return _cuts.TryGetValue(hash, out var cut) ? cut : null;
    }

    /// <summary>
    /// Removes a specific cut from the pool.
    /// </summary>
    public bool Remove(Cut cut)
    {
        return _cuts.Remove(cut.CutHash);
    }

    private double ComputeCutScore(Cut cut, double violation)
    {
        int age = _currentRound - cut.GenerationRound;
        double activityRatio = cut.ActivityCount > 0 ? (double)cut.ActivityCount / (age + 1) : 0.5;

        // Score based on: violation (50%), efficacy (30%), activity (20%), age penalty
        double score = 0.5 * violation +
                      0.3 * cut.Efficacy +
                      0.2 * activityRatio;

        // Age penalty: reduce score for old cuts
        if (age > 10)
            score *= Math.Max(0.5, 1.0 - 0.01 * (age - 10));

        return score;
    }

    private void RemoveLowestScoreCut()
    {
        if (_cuts.Count == 0)
            return;

        int lowestHash = 0;
        double lowestScore = double.MaxValue;

        foreach (var cut in _cuts.Values)
        {
            double violation = cut.Efficacy; // Use original efficacy as proxy
            double score = ComputeCutScore(cut, violation);
            if (score < lowestScore)
            {
                lowestScore = score;
                lowestHash = cut.CutHash;
            }
        }

        _cuts.Remove(lowestHash);
    }
}
