namespace OptiCore.Tests;

using Xunit;
using OptiCore.Solver;
using OptiCore.Models;
using OptiCore.Enums;
using OptiCore.BranchAndBound;

/// <summary>
/// Regression tests for flaws found in the post-bugfix audit:
/// 1. Constraints with negative RHS were not normalized, so the simplex started from an
///    infeasible basis and silently returned invalid "optimal" solutions.
/// 2. Branch &amp; Bound fathomed non-converged LP relaxations as infeasible, reporting
///    Infeasible for feasible ILPs.
/// 3. A fixed Big-M of 1e6 was smaller than large objective coefficients, so artificials
///    could not be driven out and feasible models were reported infeasible.
/// 4. Constraints referencing unknown variable names were silently treated as 0 coefficients.
/// 5. Cut generation read the initial (unsolved) tableau and inferred basicness from column
///    shape instead of using the solved tableau and the explicit basis.
/// </summary>
public class SimplexAuditFixTests
{
  private static LinearModel Lp(ObjectiveType goal, List<Term> obj, List<Constraint> cons, List<Term> vars)
    => new LinearModel(ModelType.LinearProgramming, new ModelObjective(goal, obj), cons, vars);

  // ---- Flaw 1: negative RHS ----

  [Fact]
  public void Simplex_NegativeRhsLeConstraint_SolvesCorrectly()
  {
    // min x1+x2 s.t. x1 - x2 <= -2  =>  optimum (0,2), z=2
    var vars = new List<Term> { new("x1", 0), new("x2", 0) };
    var model = Lp(ObjectiveType.MIN,
      new List<Term> { new("x1", 1), new("x2", 1) },
      new List<Constraint> {
        new("c1", new List<Term> { new("x1", 1), new("x2", -1) }, "<=", -2)
      }, vars);

    var simplex = new OptiCoreSimplex(model);
    var r = simplex.GetOptimalValues();

    Assert.False(simplex.IsInfeasible);
    Assert.Equal(2.0, r.OptimalResult, 6);
    Assert.Equal(0.0, r.Terms.First(t => t.TermName == "x1").Coefficient, 6);
    Assert.Equal(2.0, r.Terms.First(t => t.TermName == "x2").Coefficient, 6);
  }

  [Fact]
  public void Simplex_NegativeRhsInfeasibleModel_ReportsInfeasible()
  {
    // x >= 0 implicit, so x <= -1 has no solution
    var vars = new List<Term> { new("x", 0) };
    var model = Lp(ObjectiveType.MIN,
      new List<Term> { new("x", 1) },
      new List<Constraint> {
        new("c1", new List<Term> { new("x", 1) }, "<=", -1)
      }, vars);

    var simplex = new OptiCoreSimplex(model);
    var r = simplex.GetOptimalValues();

    Assert.True(simplex.IsInfeasible);
    Assert.True(double.IsNaN(r.OptimalResult));
  }

  [Fact]
  public void Simplex_NegativeRhsGeConstraint_SolvesCorrectly()
  {
    // max x1 s.t. x1 - x2 >= -2, x1 <= 3, x2 <= 1  =>  optimum x1=3, z=3
    var vars = new List<Term> { new("x1", 0), new("x2", 0) };
    var model = Lp(ObjectiveType.MAX,
      new List<Term> { new("x1", 1) },
      new List<Constraint> {
        new("c1", new List<Term> { new("x1", 1), new("x2", -1) }, ">=", -2),
        new("c2", new List<Term> { new("x1", 1) }, "<=", 3),
        new("c3", new List<Term> { new("x2", 1) }, "<=", 1)
      }, vars);

    var simplex = new OptiCoreSimplex(model);
    var r = simplex.GetOptimalValues();

    Assert.False(simplex.IsInfeasible);
    Assert.Equal(3.0, r.OptimalResult, 6);
  }

  // ---- Flaw 2: non-convergence fathomed as infeasible in B&B ----

  [Fact]
  public void BranchBound_RootLpNonConvergence_DoesNotReportInfeasible()
  {
    // Beale's cycling LP as relaxation, x3 integer. Feasible ILP (x3=1, z=-0.05),
    // but the root LP cycles, so the solver must not claim Infeasible.
    var vars = new List<Term> { new("x1", 0), new("x2", 0), new("x3", 0), new("x4", 0) };
    var objective = new ModelObjective(ObjectiveType.MIN, new List<Term>
    {
      new("x1", -0.75), new("x2", 150), new("x3", -0.02), new("x4", 6)
    });
    var constraints = new List<Constraint>
    {
      new("c1", new List<Term> { new("x1", 0.25), new("x2", -60), new("x3", -0.04), new("x4", 9) }, "<=", 0),
      new("c2", new List<Term> { new("x1", 0.5), new("x2", -90), new("x3", -0.02), new("x4", 3) }, "<=", 0),
      new("c3", new List<Term> { new("x3", 1) }, "<=", 1)
    };
    var model = new LinearModel(ModelType.MixedIntegerLinearProgramming, objective, constraints, vars);
    var integers = new List<IntegerTerm> { new("x3", 0, VariableType.Integer) };

    var result = new BranchBoundSolver(model, integers, BranchBoundOptions.Quick).Solve();

    // With no incumbent and abandoned subtrees the honest answer is Error, not Infeasible.
    Assert.Equal(BranchBoundStatus.Error, result.Status);
    Assert.NotNull(result.ErrorMessage);
  }

  // ---- Flaw 3: fixed Big-M smaller than objective coefficients ----

  [Fact]
  public void Simplex_ObjectiveCoefficientLargerThanOldBigM_SolvesCorrectly()
  {
    // min x + 1e7*u s.t. x + u >= 3, x <= 1  =>  optimum x=1, u=2, z = 1 + 2e7
    var vars = new List<Term> { new("x", 0), new("u", 0) };
    var model = Lp(ObjectiveType.MIN,
      new List<Term> { new("x", 1), new("u", 1e7) },
      new List<Constraint> {
        new("cov", new List<Term> { new("x", 1), new("u", 1) }, ">=", 3),
        new("cap", new List<Term> { new("x", 1) }, "<=", 1)
      }, vars);

    var simplex = new OptiCoreSimplex(model);
    var r = simplex.GetOptimalValues();

    Assert.False(simplex.IsInfeasible);
    Assert.Equal(1 + 2e7, r.OptimalResult, 3);
    Assert.Equal(1.0, r.Terms.First(t => t.TermName == "x").Coefficient, 6);
    Assert.Equal(2.0, r.Terms.First(t => t.TermName == "u").Coefficient, 6);
  }

  // ---- Flaw 5: cut generation must use the solved tableau and the explicit basis ----

  [Fact]
  public void CutContext_WithExplicitBasis_ReturnsTrueBasicVariable_NotFirstUnitColumn()
  {
    // Row 0: column 0 coincidentally looks like a unit vector, but the true basic
    // variable of row 0 is column 2. Shape inference would wrongly return column 0.
    var matrix = new double[,]
    {
      { 1.0, 0.0, 1.0, 0.0, 2.5 },
      { 0.0, 1.0, 0.0, 0.0, 1.0 },
      { 0.0, 0.0, 0.0, 0.0, 9.9 } // objective row
    };
    var context = new OptiCore.Cuts.CutGenerationContext(
      simplexMatrix: matrix,
      variableNames: new List<string> { "a", "b", "c", "d" },
      integerVariableIndices: new HashSet<int> { 0, 1, 2 },
      currentSolution: new List<Term> { new("a", 0), new("b", 1), new("c", 2.5), new("d", 0) },
      numberOfOriginalVariables: 4,
      objectiveValue: 9.9,
      basisVariables: new[] { 2, 1 });

    Assert.Equal(2, context.FindBasicVariable(0));
    Assert.Equal(1, context.FindBasicVariable(1));
  }

  [Fact]
  public void BranchAndCut_WithCutsEnabled_FindsCorrectIntegerOptimum()
  {
    // max x + y s.t. 2x + 2y <= 5, x,y integer  =>  LP opt 2.5, ILP opt 2
    var vars = new List<Term> { new("x", 0), new("y", 0) };
    var model = new LinearModel(ModelType.IntegerLinearProgramming,
      new ModelObjective(ObjectiveType.MAX, new List<Term> { new("x", 1), new("y", 1) }),
      new List<Constraint> {
        new("c1", new List<Term> { new("x", 2), new("y", 2) }, "<=", 5)
      }, vars);
    var integers = new List<IntegerTerm> {
      new("x", 0, VariableType.Integer), new("y", 0, VariableType.Integer)
    };
    var options = new BranchBoundOptions { EnableCuts = true, MaxCutRoundsPerNode = 5 };

    var result = new BranchAndCutSolver(model, integers, options).Solve();

    Assert.Equal(BranchBoundStatus.Optimal, result.Status);
    Assert.NotNull(result.ObjectiveValue);
    Assert.Equal(2.0, result.ObjectiveValue!.Value, 6);
  }

  // ---- Flaw 4: unknown variable names silently treated as 0 coefficients ----

  [Fact]
  public void Simplex_UnknownVariableInConstraint_Throws()
  {
    var vars = new List<Term> { new("x", 0) };
    var model = Lp(ObjectiveType.MAX,
      new List<Term> { new("x", 1) },
      new List<Constraint> {
        // "y" is not declared: without validation this becomes 0 <= 5 (always true)
        new("c1", new List<Term> { new("y", 1) }, "<=", 5),
        new("c2", new List<Term> { new("x", 1) }, "<=", 10)
      }, vars);

    var ex = Assert.Throws<ArgumentException>(() => new OptiCoreSimplex(model).GetOptimalValues());
    Assert.Contains("y", ex.Message);
  }

  [Fact]
  public void Simplex_UnknownVariableInObjective_Throws()
  {
    var vars = new List<Term> { new("x", 0) };
    var model = Lp(ObjectiveType.MAX,
      new List<Term> { new("x", 1), new("ghost", 5) },
      new List<Constraint> {
        new("c1", new List<Term> { new("x", 1) }, "<=", 10)
      }, vars);

    var ex = Assert.Throws<ArgumentException>(() => new OptiCoreSimplex(model).GetOptimalValues());
    Assert.Contains("ghost", ex.Message);
  }

  [Fact]
  public void BranchBound_UnknownVariable_ThrowsAtConstruction_NotSilentInfeasible()
  {
    // The solver ctor must surface the modeling error directly; inside Solve() the
    // per-node catch would swallow it and misreport the model as infeasible.
    var vars = new List<Term> { new("x", 0) };
    var model = new LinearModel(ModelType.IntegerLinearProgramming,
      new ModelObjective(ObjectiveType.MAX, new List<Term> { new("x", 1) }),
      new List<Constraint> {
        new("c1", new List<Term> { new("typo", 1) }, "<=", 5)
      }, vars);

    Assert.Throws<ArgumentException>(() =>
      new BranchBoundSolver(model, new List<IntegerTerm> { new("x", 0, VariableType.Integer) }));
  }
}
