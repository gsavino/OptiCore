namespace OptiCore.Tests;

using Xunit;
using OptiCore.Solver;
using OptiCore.Models;
using OptiCore.Enums;

/// <summary>
/// Regression tests for two reported simplex bugs:
/// 1. SolveSimplex exhausting its iteration limit silently and returning a half-solved
///    tableau as if it were optimal (no non-convergence signal).
/// 2. False infeasibility when a variable that appears in a single >= row becomes basic:
///    the Big-M artificial variable for that row keeps an identical unit-vector column,
///    so column-shape basicness detection attributes the row's RHS to the artificial.
/// </summary>
public class SimplexBugFixTests
{
  /// <summary>
  /// Repro from the bug report: min 320x + 10000u s.t. x + u >= 3, x <= 1.
  /// Optimum is x=1, u=2, z=20320. The u column and the artificial column of the
  /// >= row are both unit vectors at the same row, which fooled CheckFeasibility
  /// into reporting Infeasible.
  /// </summary>
  [Fact]
  public void Simplex_SingleRowVariableBasicInGeConstraint_ShouldNotReportInfeasible()
  {
    var variables = new List<Term> { new Term("x", 0), new Term("u", 0) };
    var objective = new ModelObjective(ObjectiveType.MIN,
        new List<Term> { new Term("x", 320), new Term("u", 10000) });
    var constraints = new List<Constraint>
    {
      new Constraint("coverage", new List<Term> { new Term("x", 1), new Term("u", 1) }, ">=", 3),
      new Constraint("capacity", new List<Term> { new Term("x", 1) }, "<=", 1)
    };
    var model = new LinearModel(ModelType.LinearProgramming, objective, constraints, variables);

    var simplex = new OptiCoreSimplex(model);
    var result = simplex.GetOptimalValues();

    Assert.False(simplex.IsInfeasible);
    Assert.Equal(20320.0, result.OptimalResult, 6);
    Assert.Equal(1.0, result.Terms.First(t => t.TermName == "x").Coefficient, 6);
    Assert.Equal(2.0, result.Terms.First(t => t.TermName == "u").Coefficient, 6);
  }

  /// <summary>
  /// Same shape with an equality constraint instead of >=: min 5x + 7u s.t. x + u = 4, x <= 1.
  /// Optimum is x=1, u=3, z=26. Guards the basis bookkeeping for = rows too.
  /// </summary>
  [Fact]
  public void Simplex_SingleRowVariableBasicInEqConstraint_ShouldNotReportInfeasible()
  {
    var variables = new List<Term> { new Term("x", 0), new Term("u", 0) };
    var objective = new ModelObjective(ObjectiveType.MIN,
        new List<Term> { new Term("x", 5), new Term("u", 7) });
    var constraints = new List<Constraint>
    {
      new Constraint("balance", new List<Term> { new Term("x", 1), new Term("u", 1) }, "=", 4),
      new Constraint("capacity", new List<Term> { new Term("x", 1) }, "<=", 1)
    };
    var model = new LinearModel(ModelType.LinearProgramming, objective, constraints, variables);

    var simplex = new OptiCoreSimplex(model);
    var result = simplex.GetOptimalValues();

    Assert.False(simplex.IsInfeasible);
    Assert.Equal(26.0, result.OptimalResult, 6);
    Assert.Equal(1.0, result.Terms.First(t => t.TermName == "x").Coefficient, 6);
    Assert.Equal(3.0, result.Terms.First(t => t.TermName == "u").Coefficient, 6);
  }

  /// <summary>
  /// A genuinely infeasible model must still be reported as such after the basis fix:
  /// x >= 5 and x <= 1 has no solution.
  /// </summary>
  [Fact]
  public void Simplex_TrulyInfeasibleModel_ShouldStillReportInfeasible()
  {
    var variables = new List<Term> { new Term("x", 0) };
    var objective = new ModelObjective(ObjectiveType.MIN, new List<Term> { new Term("x", 1) });
    var constraints = new List<Constraint>
    {
      new Constraint("lower", new List<Term> { new Term("x", 1) }, ">=", 5),
      new Constraint("upper", new List<Term> { new Term("x", 1) }, "<=", 1)
    };
    var model = new LinearModel(ModelType.LinearProgramming, objective, constraints, variables);

    var simplex = new OptiCoreSimplex(model);
    var result = simplex.GetOptimalValues();

    Assert.True(simplex.IsInfeasible);
    Assert.True(double.IsNaN(result.OptimalResult));
  }

  /// <summary>
  /// Beale's classic cycling example: with Dantzig's most-negative-column rule and
  /// lowest-index tie-breaking the simplex cycles forever, so any iteration limit is
  /// exhausted. The solver must signal non-convergence (NaN result) instead of
  /// returning the half-solved tableau as if it were optimal.
  /// Model: min -3/4 x1 + 150 x2 - 1/50 x3 + 6 x4
  ///        s.t. 1/4 x1 - 60 x2 - 1/25 x3 + 9 x4 <= 0
  ///             1/2 x1 - 90 x2 - 1/50 x3 + 3 x4 <= 0
  ///             x3 <= 1
  /// </summary>
  [Fact]
  public void Simplex_WhenIterationLimitExhausted_ShouldSignalNonConvergence()
  {
    var variables = new List<Term>
    {
      new Term("x1", 0), new Term("x2", 0), new Term("x3", 0), new Term("x4", 0)
    };
    var objective = new ModelObjective(ObjectiveType.MIN, new List<Term>
    {
      new Term("x1", -0.75), new Term("x2", 150), new Term("x3", -0.02), new Term("x4", 6)
    });
    var constraints = new List<Constraint>
    {
      new Constraint("c1", new List<Term>
      {
        new Term("x1", 0.25), new Term("x2", -60), new Term("x3", -0.04), new Term("x4", 9)
      }, "<=", 0),
      new Constraint("c2", new List<Term>
      {
        new Term("x1", 0.5), new Term("x2", -90), new Term("x3", -0.02), new Term("x4", 3)
      }, "<=", 0),
      new Constraint("c3", new List<Term> { new Term("x3", 1) }, "<=", 1)
    };
    var model = new LinearModel(ModelType.LinearProgramming, objective, constraints, variables);

    var simplex = new OptiCoreSimplex(model);
    var result = simplex.GetOptimalValues();

    // If the solver did not converge it must say so, never fabricate an optimum.
    // (If a future pivoting rule breaks the cycle and truly converges, the correct
    // optimum of Beale's problem is z = -0.05.)
    if (double.IsNaN(result.OptimalResult))
    {
      Assert.True(simplex.IsNotConverged);
    }
    else
    {
      Assert.False(simplex.IsNotConverged);
      Assert.Equal(-0.05, result.OptimalResult, 6);
    }
  }
}
