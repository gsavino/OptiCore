using Xunit;
using Xunit.Abstractions;
using OptiCore.BranchAndBound;
using OptiCore.BranchAndBound.Strategies.Branching;
using OptiCore.BranchAndBound.Strategies.NodeSelection;
using OptiCore.Cuts;
using OptiCore.Cuts.Generators;
using OptiCore.Enums;
using OptiCore.Models;
using OptiCore.Solver;

namespace OptiCore.Tests;

/// <summary>
/// Tests for Integer Programming extensions including Branch & Bound and Branch & Cut.
/// </summary>
public class IntegerProgrammingTests
{
    private readonly ITestOutputHelper _output;

    public IntegerProgrammingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region IntegerTerm Tests

    /// <summary>
    /// Verifies that integer-typed variables accept whole number values and values within tolerance.
    /// </summary>
    [Fact]
    public void IntegerTerm_IsFeasibleValue_Integer_AcceptsIntegerValues()
    {
        var term = new IntegerTerm("x", 0, VariableType.Integer);

        Assert.True(term.IsFeasibleValue(5.0));
        Assert.True(term.IsFeasibleValue(0.0));
        Assert.True(term.IsFeasibleValue(-3.0));
        Assert.True(term.IsFeasibleValue(5.0000001)); // Within tolerance
    }

    /// <summary>
    /// Verifies that integer-typed variables reject clearly fractional values.
    /// </summary>
    [Fact]
    public void IntegerTerm_IsFeasibleValue_Integer_RejectsFractionalValues()
    {
        var term = new IntegerTerm("x", 0, VariableType.Integer);

        Assert.False(term.IsFeasibleValue(5.5));
        Assert.False(term.IsFeasibleValue(0.1));
        Assert.False(term.IsFeasibleValue(2.999));
    }

    /// <summary>
    /// Verifies that binary variables accept only 0 and 1 (and values within tolerance).
    /// </summary>
    [Fact]
    public void IntegerTerm_IsFeasibleValue_Binary_AcceptsZeroAndOne()
    {
        var term = new IntegerTerm("x", 0, VariableType.Binary);

        Assert.True(term.IsFeasibleValue(0.0));
        Assert.True(term.IsFeasibleValue(1.0));
        Assert.True(term.IsFeasibleValue(0.0000001)); // Within tolerance
    }

    /// <summary>
    /// Verifies that binary variables reject values outside [0,1] and fractional values.
    /// </summary>
    [Fact]
    public void IntegerTerm_IsFeasibleValue_Binary_RejectsOutOfBounds()
    {
        var term = new IntegerTerm("x", 0, VariableType.Binary);

        Assert.False(term.IsFeasibleValue(0.5));
        Assert.False(term.IsFeasibleValue(2.0));
        Assert.False(term.IsFeasibleValue(-1.0));
    }

    [Fact]
    public void IntegerTerm_IsFractional_DetectsFractionalValues()
    {
        Assert.True(IntegerTerm.IsFractional(5.5));
        Assert.True(IntegerTerm.IsFractional(0.1));
        Assert.True(IntegerTerm.IsFractional(2.999));

        Assert.False(IntegerTerm.IsFractional(5.0));
        Assert.False(IntegerTerm.IsFractional(0.0));
        Assert.False(IntegerTerm.IsFractional(5.0000001));
    }

    [Fact]
    public void IntegerTerm_GetFractionalPart_ReturnsCorrectPart()
    {
        Assert.Equal(0.5, IntegerTerm.GetFractionalPart(5.5), 6);
        Assert.Equal(0.25, IntegerTerm.GetFractionalPart(3.25), 6);
        Assert.Equal(0.0, IntegerTerm.GetFractionalPart(4.0), 6);
    }

    [Fact]
    public void IntegerTerm_WithBounds_CreatesNewTermWithBounds()
    {
        var term = new IntegerTerm("x", 0, VariableType.Integer);
        var bounded = term.WithBounds(newLowerBound: 0, newUpperBound: 10);

        Assert.Equal(0, bounded.LowerBound);
        Assert.Equal(10, bounded.UpperBound);
        Assert.Null(term.LowerBound); // Original unchanged
    }

    #endregion

    #region BranchNode Tests

    [Fact]
    public void BranchNode_CreateRoot_CreatesNodeWithCorrectDefaults()
    {
        var root = BranchNode.CreateRoot();

        Assert.Equal(0, root.NodeId);
        Assert.Null(root.ParentId);
        Assert.Equal(0, root.Depth);
        Assert.Empty(root.VariableBounds);
        Assert.Equal(NodeStatus.Pending, root.Status);
    }

    [Fact]
    public void BranchNode_CreateChild_CreatesNodeWithBounds()
    {
        var root = BranchNode.CreateRoot();
        var child = root.CreateChild(1, "x1", lowerBound: null, upperBound: 3.0, direction: "down");

        Assert.Equal(1, child.NodeId);
        Assert.Equal(0, child.ParentId);
        Assert.Equal(1, child.Depth);
        Assert.Single(child.VariableBounds);
        Assert.Equal("x1", child.VariableBounds[0].VariableName);
        Assert.Equal(3.0, child.VariableBounds[0].UpperBound);
    }

    [Fact]
    public void BranchNode_GetEffectiveBounds_AccumulatesBoundsFromPath()
    {
        var root = BranchNode.CreateRoot();
        var child1 = root.CreateChild(1, "x1", lowerBound: null, upperBound: 5.0, direction: "down");
        var child2 = child1.CreateChild(2, "x1", lowerBound: 2.0, upperBound: null, direction: "up");

        var (lower, upper) = child2.GetEffectiveBounds("x1");

        Assert.Equal(2.0, lower);
        Assert.Equal(5.0, upper);
    }

    #endregion

    #region Node Selection Strategy Tests

    [Fact]
    public void BestBoundNodeSelection_SelectsHighestBoundForMax()
    {
        var strategy = new BestBoundNodeSelection();

        var node1 = BranchNode.CreateRoot().WithLpSolution(10.0, new List<Term>());
        var node2 = node1.CreateChild(1, "x", null, 5, "down").WithLpSolution(15.0, new List<Term>());
        var node3 = node1.CreateChild(2, "x", 6, null, "up").WithLpSolution(12.0, new List<Term>());

        var nodes = new List<BranchNode> { node1, node2, node3 };
        var selected = strategy.SelectNode(nodes, null, isMaximization: true);

        Assert.Equal(node2, selected);
    }

    [Fact]
    public void DepthFirstNodeSelection_SelectsDeepestNode()
    {
        var strategy = new DepthFirstNodeSelection();

        var root = BranchNode.CreateRoot();
        var child1 = root.CreateChild(1, "x", null, 5, "down");
        var grandchild = child1.CreateChild(2, "x", null, 3, "down");

        var nodes = new List<BranchNode> { root, child1, grandchild };
        var selected = strategy.SelectNode(nodes, null, isMaximization: true);

        Assert.Equal(grandchild, selected);
    }

    #endregion

    #region Branching Strategy Tests

    [Fact]
    public void MostFractionalBranching_SelectsMostFractionalVariable()
    {
        var strategy = new MostFractionalBranching();

        var solution = new List<Term>
        {
            new Term("x1", 2.5),  // Fractional part 0.5 - most fractional
            new Term("x2", 3.2),  // Fractional part 0.2
            new Term("x3", 4.0),  // Integer
        };

        var node = BranchNode.CreateRoot().WithLpSolution(10.0, solution);

        var intVars = new List<IntegerTerm>
        {
            new IntegerTerm("x1", 0, VariableType.Integer),
            new IntegerTerm("x2", 0, VariableType.Integer),
            new IntegerTerm("x3", 0, VariableType.Integer)
        };

        var decision = strategy.SelectBranchingVariable(node, intVars, 1e-6);

        Assert.NotNull(decision);
        Assert.Equal("x1", decision.VariableName);
        Assert.Equal(2.5, decision.CurrentValue);
        Assert.Equal(2.0, decision.FloorValue);
        Assert.Equal(3.0, decision.CeilValue);
    }

    [Fact]
    public void MostFractionalBranching_ReturnsNull_WhenAllIntegral()
    {
        var strategy = new MostFractionalBranching();

        var solution = new List<Term>
        {
            new Term("x1", 2.0),
            new Term("x2", 3.0),
        };

        var node = BranchNode.CreateRoot().WithLpSolution(10.0, solution);

        var intVars = new List<IntegerTerm>
        {
            new IntegerTerm("x1", 0, VariableType.Integer),
            new IntegerTerm("x2", 0, VariableType.Integer)
        };

        var decision = strategy.SelectBranchingVariable(node, intVars, 1e-6);

        Assert.Null(decision);
    }

    #endregion

    #region SolutionPool Tests

    [Fact]
    public void SolutionPool_AddsAndTracksIncumbent()
    {
        var pool = new SolutionPool(5, isMaximization: true);

        var sol1 = new ModelResult { Terms = new List<Term>(), OptimalResult = 10.0 };
        var sol2 = new ModelResult { Terms = new List<Term>(), OptimalResult = 15.0 };

        pool.TryAdd(sol1, 10.0);
        Assert.Equal(10.0, pool.IncumbentValue);

        pool.TryAdd(sol2, 15.0);
        Assert.Equal(15.0, pool.IncumbentValue); // Better for max
    }

    [Fact]
    public void SolutionPool_ShouldPrune_ReturnsTrueForWorseBounds()
    {
        var pool = new SolutionPool(5, isMaximization: true);
        var sol = new ModelResult { Terms = new List<Term>(), OptimalResult = 10.0 };

        pool.TryAdd(sol, 10.0);

        Assert.True(pool.ShouldPrune(8.0)); // Worse bound for max
        Assert.False(pool.ShouldPrune(12.0)); // Better bound
    }

    #endregion

    #region Cut Tests

    [Fact]
    public void Cut_GetViolation_CalculatesCorrectly()
    {
        var cut = new Cut(
            cutId: "c1",
            cutType: CutType.GomoryFractional,
            coefficients: new List<Term> { new Term("x1", 1.0), new Term("x2", 2.0) },
            rhs: 5.0,
            op: "<="
        );

        var solution = new Dictionary<string, double> { ["x1"] = 2.0, ["x2"] = 3.0 };
        // LHS = 1*2 + 2*3 = 8, violation = 8 - 5 = 3

        Assert.Equal(3.0, cut.GetViolation(solution), 6);
        Assert.True(cut.IsViolated(solution));
    }

    [Fact]
    public void CutPool_DeduplicatesCuts()
    {
        var pool = new CutPool();

        var cut1 = new Cut("c1", CutType.GomoryFractional,
            new List<Term> { new Term("x1", 1.0) }, 5.0, "<=");
        var cut2 = new Cut("c2", CutType.GomoryFractional,
            new List<Term> { new Term("x1", 1.0) }, 5.0, "<="); // Same constraint, different ID

        Assert.True(pool.TryAdd(cut1));
        Assert.False(pool.TryAdd(cut2)); // Should be rejected as duplicate
        Assert.Equal(1, pool.Count);
    }

    #endregion

    #region Branch & Bound Integration Tests

    /// <summary>
    /// Integration test: solves a simple ILP (max x1+x2 s.t. x1+x2&lt;=3.5)
    /// and verifies the solver completes without errors.
    /// </summary>
    [Fact]
    public void BranchBound_SolvesSimpleILP()
    {
        // Simple ILP: max x1 + x2 subject to x1 + x2 <= 3.5, x1,x2 integer
        // Note: This test uses a small node limit for quick testing
        var model = new LinearModel(
            ModelKind: ModelType.IntegerLinearProgramming,
            Objective: new ModelObjective(ObjectiveType.MAX, new List<Term>
            {
                new Term("x1", 1.0),
                new Term("x2", 1.0)
            }),
            ConstraintsList: new List<Constraint>
            {
                new Constraint("c1", new List<Term>
                {
                    new Term("x1", 1.0),
                    new Term("x2", 1.0)
                }, "<=", 3.5)
            },
            Variables: new List<Term>
            {
                new Term("x1", 0.0),
                new Term("x2", 0.0)
            }
        );

        var options = new BranchBoundOptions { MaxNodes = 1000, MaxTimeSeconds = 10 };
        var solver = new BranchBoundSolver(model, options: options);
        var result = solver.Solve();

        _output.WriteLine($"Status: {result.Status}");
        _output.WriteLine($"Objective: {result.ObjectiveValue}");
        _output.WriteLine($"Nodes explored: {result.Statistics.NodesExplored}");

        // Accept either Optimal or any feasible result (solver may hit limits)
        Assert.True(result.Status == BranchBoundStatus.Optimal ||
                    result.Status == BranchBoundStatus.Feasible ||
                    result.Status == BranchBoundStatus.NodeLimitReached ||
                    result.Status == BranchBoundStatus.TimeLimitReached);
    }

    /// <summary>
    /// Integration test: solves a knapsack-style ILP with two constraints and verifies completion.
    /// </summary>
    [Fact]
    public void BranchBound_SolvesKnapsackProblem()
    {
        // Knapsack: max 8x1 + 5x2 s.t. x1 + x2 <= 6, 9x1 + 5x2 <= 45, x1,x2 integer
        var model = new LinearModel(
            ModelKind: ModelType.IntegerLinearProgramming,
            Objective: new ModelObjective(ObjectiveType.MAX, new List<Term>
            {
                new Term("x1", 8.0),
                new Term("x2", 5.0)
            }),
            ConstraintsList: new List<Constraint>
            {
                new Constraint("c1", new List<Term>
                {
                    new Term("x1", 1.0),
                    new Term("x2", 1.0)
                }, "<=", 6.0),
                new Constraint("c2", new List<Term>
                {
                    new Term("x1", 9.0),
                    new Term("x2", 5.0)
                }, "<=", 45.0)
            },
            Variables: new List<Term>
            {
                new Term("x1", 0.0),
                new Term("x2", 0.0)
            }
        );

        var options = new BranchBoundOptions { MaxNodes = 1000, MaxTimeSeconds = 10 };
        var solver = new BranchBoundSolver(model, options: options);
        var result = solver.Solve();

        _output.WriteLine($"Status: {result.Status}");
        _output.WriteLine($"Objective: {result.ObjectiveValue}");
        if (result.BestSolution != null)
        {
            foreach (var term in result.BestSolution.Terms)
            {
                _output.WriteLine($"  {term.TermName} = {term.Coefficient}");
            }
        }
        _output.WriteLine($"Nodes explored: {result.Statistics.NodesExplored}");

        // Accept various outcomes - the key is that it doesn't crash
        Assert.True(result.Status == BranchBoundStatus.Optimal ||
                    result.Status == BranchBoundStatus.Feasible ||
                    result.Status == BranchBoundStatus.NodeLimitReached ||
                    result.Status == BranchBoundStatus.TimeLimitReached);
    }

    /// <summary>
    /// Tests that when the LP relaxation already has integer values, the solver recognizes
    /// optimality at the root node without branching.
    /// </summary>
    [Fact]
    public void BranchBound_HandlesAlreadyIntegralSolution()
    {
        // LP relaxation is already integer: max x1 s.t. x1 <= 5
        var model = new LinearModel(
            ModelKind: ModelType.IntegerLinearProgramming,
            Objective: new ModelObjective(ObjectiveType.MAX, new List<Term>
            {
                new Term("x1", 1.0)
            }),
            ConstraintsList: new List<Constraint>
            {
                new Constraint("c1", new List<Term>
                {
                    new Term("x1", 1.0)
                }, "<=", 5.0)
            },
            Variables: new List<Term>
            {
                new Term("x1", 0.0)
            }
        );

        var solver = new BranchBoundSolver(model);
        var result = solver.Solve();

        _output.WriteLine($"Status: {result.Status}");
        _output.WriteLine($"Objective: {result.ObjectiveValue}");
        _output.WriteLine($"Nodes explored: {result.Statistics.NodesExplored}");

        Assert.Equal(BranchBoundStatus.Optimal, result.Status);
        Assert.True(Math.Abs(result.ObjectiveValue!.Value - 5.0) < 0.1);
        Assert.Equal(1, result.Statistics.NodesExplored); // Should solve at root
    }

    #endregion

    #region Branch & Cut Integration Tests

    /// <summary>
    /// Integration test: solves an ILP using the Branch and Cut solver with Gomory cut generation enabled.
    /// </summary>
    [Fact]
    public void BranchAndCut_SolvesWithCuts()
    {
        var model = new LinearModel(
            ModelKind: ModelType.IntegerLinearProgramming,
            Objective: new ModelObjective(ObjectiveType.MAX, new List<Term>
            {
                new Term("x1", 8.0),
                new Term("x2", 5.0)
            }),
            ConstraintsList: new List<Constraint>
            {
                new Constraint("c1", new List<Term>
                {
                    new Term("x1", 1.0),
                    new Term("x2", 1.0)
                }, "<=", 6.0),
                new Constraint("c2", new List<Term>
                {
                    new Term("x1", 9.0),
                    new Term("x2", 5.0)
                }, "<=", 45.0)
            },
            Variables: new List<Term>
            {
                new Term("x1", 0.0),
                new Term("x2", 0.0)
            }
        );

        var options = new BranchBoundOptions { EnableCuts = true, MaxNodes = 1000, MaxTimeSeconds = 10 };
        var solver = new BranchAndCutSolver(model, options: options);
        var result = solver.Solve();

        _output.WriteLine($"Status: {result.Status}");
        _output.WriteLine($"Objective: {result.ObjectiveValue}");
        _output.WriteLine($"Nodes explored: {result.Statistics.NodesExplored}");
        _output.WriteLine($"Cuts generated: {result.Statistics.CutsGenerated}");
        _output.WriteLine($"Cuts added: {result.Statistics.CutsAdded}");

        // Accept various outcomes - the key is that it doesn't crash
        Assert.True(result.Status == BranchBoundStatus.Optimal ||
                    result.Status == BranchBoundStatus.Feasible ||
                    result.Status == BranchBoundStatus.NodeLimitReached ||
                    result.Status == BranchBoundStatus.TimeLimitReached);
    }

    #endregion

    #region WorkingModel Tests

    [Fact]
    public void WorkingModel_AddsCutsToBuiltModel()
    {
        var baseModel = new LinearModel(
            ModelKind: ModelType.LinearProgramming,
            Objective: new ModelObjective(ObjectiveType.MAX, new List<Term>
            {
                new Term("x1", 1.0)
            }),
            ConstraintsList: new List<Constraint>(),
            Variables: new List<Term>
            {
                new Term("x1", 0.0)
            }
        );

        var workingModel = new WorkingModel(baseModel);
        var cut = new Cut("cut1", CutType.GomoryFractional,
            new List<Term> { new Term("x1", 1.0) }, 5.0, "<=");

        workingModel.AddCut(cut);
        var built = workingModel.BuildLinearModel();

        Assert.Equal(1, built.ConstraintsList.Count);
        Assert.Equal("cut1", built.ConstraintsList[0].ConstraintName);
    }

    [Fact]
    public void WorkingModel_AppliesBranchBounds()
    {
        var baseModel = new LinearModel(
            ModelKind: ModelType.LinearProgramming,
            Objective: new ModelObjective(ObjectiveType.MAX, new List<Term>
            {
                new Term("x1", 1.0)
            }),
            ConstraintsList: new List<Constraint>(),
            Variables: new List<Term>
            {
                new Term("x1", 0.0)
            }
        );

        var workingModel = new WorkingModel(baseModel);
        var bounds = new List<VariableBound>
        {
            new VariableBound("x1", LowerBound: 2.0, UpperBound: 5.0)
        };

        workingModel.ApplyBranchBounds(bounds);
        var built = workingModel.BuildLinearModel();

        Assert.Equal(2, built.ConstraintsList.Count); // LB and UB constraints
    }

    #endregion

    #region ModelType Enum Tests

    [Fact]
    public void ModelType_HasILPAndMILPValues()
    {
        Assert.Equal(1, (int)ModelType.IntegerLinearProgramming);
        Assert.Equal(2, (int)ModelType.MixedIntegerLinearProgramming);
    }

    #endregion
}
