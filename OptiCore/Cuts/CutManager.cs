using OptiCore.Models;

namespace OptiCore.Cuts;

/// <summary>
/// Orchestrates multiple cut generators and manages the global cut pool.
/// </summary>
public class CutManager
{
    private readonly List<ICutGenerator> _generators = new();
    private readonly CutPool _globalPool;
    private int _cutIdCounter = 0;
    private int _totalCutsGenerated = 0;
    private int _totalCutsAdded = 0;

    /// <summary>
    /// Creates a new cut manager.
    /// </summary>
    /// <param name="poolSize">Maximum size of the global cut pool.</param>
    public CutManager(int poolSize = 1000)
    {
        _globalPool = new CutPool(poolSize);
    }

    /// <summary>
    /// Gets the global cut pool.
    /// </summary>
    public CutPool GlobalPool => _globalPool;

    /// <summary>
    /// Gets the total number of cuts generated.
    /// </summary>
    public int TotalCutsGenerated => _totalCutsGenerated;

    /// <summary>
    /// Gets the total number of cuts added to the pool (after deduplication).
    /// </summary>
    public int TotalCutsAdded => _totalCutsAdded;

    /// <summary>
    /// Registers a cut generator.
    /// </summary>
    public void RegisterGenerator(ICutGenerator generator)
    {
        _generators.Add(generator);
        // Sort by priority (highest first)
        _generators.Sort((a, b) => b.Priority.CompareTo(a.Priority));
    }

    /// <summary>
    /// Removes a cut generator.
    /// </summary>
    public bool RemoveGenerator(ICutGenerator generator)
    {
        return _generators.Remove(generator);
    }

    /// <summary>
    /// Gets all registered generators.
    /// </summary>
    public IReadOnlyList<ICutGenerator> Generators => _generators;

    /// <summary>
    /// Generates cuts using all applicable generators for the given context.
    /// </summary>
    /// <param name="context">The cut generation context.</param>
    /// <param name="nodeDepth">The current node depth.</param>
    /// <param name="nodeCount">The current node count.</param>
    /// <param name="maxCutsTotal">Maximum total cuts to generate.</param>
    /// <returns>The generated cuts.</returns>
    public IReadOnlyList<Cut> GenerateCuts(
        CutGenerationContext context,
        int nodeDepth,
        int nodeCount,
        int maxCutsTotal = 50)
    {
        _globalPool.AdvanceRound();
        var allCuts = new List<Cut>();
        int cutsPerGenerator = Math.Max(5, maxCutsTotal / Math.Max(1, _generators.Count));

        foreach (var generator in _generators)
        {
            if (!generator.ShouldGenerateAtNode(nodeDepth, nodeCount))
                continue;

            var cuts = generator.GenerateCuts(context, cutsPerGenerator);
            foreach (var cut in cuts)
            {
                _totalCutsGenerated++;
                allCuts.Add(cut);
            }

            if (allCuts.Count >= maxCutsTotal)
                break;
        }

        // Add to global pool and filter duplicates
        int added = _globalPool.TryAddRange(allCuts);
        _totalCutsAdded += added;

        return allCuts;
    }

    /// <summary>
    /// Selects the best cuts to add at a node.
    /// </summary>
    /// <param name="solution">The current LP solution.</param>
    /// <param name="maxCuts">Maximum number of cuts to select.</param>
    /// <param name="minViolation">Minimum violation threshold.</param>
    /// <returns>The selected cuts.</returns>
    public IReadOnlyList<Cut> SelectCutsToAdd(
        IEnumerable<Term> solution,
        int maxCuts = 10,
        double minViolation = 1e-6)
    {
        return _globalPool.SelectViolatedCuts(solution, maxCuts, minViolation);
    }

    /// <summary>
    /// Generates a unique cut ID.
    /// </summary>
    public string GenerateCutId(string prefix = "cut")
    {
        return $"{prefix}_{++_cutIdCounter}";
    }

    /// <summary>
    /// Purges stale cuts from the global pool.
    /// </summary>
    public void PurgeStaleCuts()
    {
        _globalPool.PurgeStaleCuts();
    }

    /// <summary>
    /// Clears all cuts and resets statistics.
    /// </summary>
    public void Clear()
    {
        _globalPool.Clear();
        _cutIdCounter = 0;
        _totalCutsGenerated = 0;
        _totalCutsAdded = 0;
    }

    /// <summary>
    /// Creates default cut manager with Gomory generators.
    /// </summary>
    public static CutManager CreateDefault()
    {
        var manager = new CutManager();
        // Generators will be added in Phase 4
        return manager;
    }
}
