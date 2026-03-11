# OptiCore

A lightweight, open-source optimization engine for solving Linear Programming (LP) and Mixed-Integer Linear Programming (MILP) problems in .NET.

## Overview

OptiCore provides a native .NET solution for mathematical optimization without external dependencies on commercial solvers. It implements the Simplex algorithm for continuous optimization and Branch & Bound / Branch & Cut algorithms for integer programming problems.

## Features

- **Linear Programming (LP)** - Continuous optimization using the Simplex method
- **Integer Linear Programming (ILP)** - Pure integer problems with Branch & Bound
- **Mixed-Integer Linear Programming (MILP)** - Combined continuous and integer variables
- **Branch & Cut** - Enhanced solving with Gomory cutting planes
- **Pluggable Strategies** - Customizable branching and node selection strategies
- **Solution Pool** - Track multiple best solutions during optimization

## Installation

```bash
dotnet add package OptiCore
```

## Quick Start 

### Solving a Linear Program

```csharp
using OptiCore.Models;
using OptiCore.Solver;
using OptiCore.Enums;

// Define variables
var variables = new List<Term>
{
    new Term("x1", 0),
    new Term("x2", 0)
};

// Define objective: maximize 3*x1 + 2*x2
var objective = new ModelObjective(
    Goal: ObjectiveType.MAX,
    Coefficients: new List<Term>
    {
        new Term("x1", 3),
        new Term("x2", 2)
    }
);

// Define constraints
var constraints = new List<Constraint>
{
    new Constraint("c1", new List<Term> { new Term("x1", 1), new Term("x2", 1) }, "<=", 4),
    new Constraint("c2", new List<Term> { new Term("x1", 2), new Term("x2", 1) }, "<=", 5)
};

// Create and solve the model
var model = new LinearModel(
    ModelKind: ModelType.LinearProgramming,
    Objective: objective,
    ConstraintsList: constraints,
    Variables: variables
);

var simplex = new OptiCoreSimplex(model);
var result = simplex.GetOptimalValues();
Console.WriteLine(result);
```

### Solving an Integer Program

```csharp
using OptiCore.BranchAndBound;

// Define integer variables
var integerVars = new List<IntegerTerm>
{
    new IntegerTerm("x1", 0, VariableType.Integer),
    new IntegerTerm("x2", 0, VariableType.Binary)
};

var model = new LinearModel(
    ModelKind: ModelType.MixedIntegerLinearProgramming,
    Objective: objective,
    ConstraintsList: constraints,
    Variables: variables
);

// Configure and solve
var options = new BranchBoundOptions
{
    MaxNodes = 100_000,
    MaxTimeSeconds = 3600,
    GapTolerance = 1e-4
};

var solver = new BranchBoundSolver(model, integerVars, options);
var result = solver.Solve();

Console.WriteLine($"Status: {result.Status}");
Console.WriteLine($"Objective: {result.ObjectiveValue}");
Console.WriteLine($"Gap: {result.GapPercent}%");
```

### Using Branch & Cut

```csharp
var options = new BranchBoundOptions
{
    EnableCuts = true,
    MaxCutRoundsPerNode = 10,
    MinCutImprovement = 1e-4
};

var solver = new BranchAndCutSolver(model, integerVars, options);
var result = solver.Solve();
```

## Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `MaxNodes` | 100,000 | Maximum nodes to explore |
| `MaxTimeSeconds` | 3600 | Time limit in seconds |
| `GapTolerance` | 1e-4 | Relative optimality gap |
| `AbsoluteGapTolerance` | 1e-6 | Absolute optimality gap |
| `IntegralityTolerance` | 1e-6 | Tolerance for integer feasibility |
| `SolutionPoolSize` | 5 | Number of best solutions to keep |
| `EnableCuts` | false | Enable cutting plane generation |
| `MaxCutRoundsPerNode` | 10 | Cut generation rounds per node |

### Pre-configured Options

```csharp
BranchBoundOptions.Default  // Standard settings
BranchBoundOptions.Quick    // Faster solving, less optimal
BranchBoundOptions.Optimal  // Prove optimality
```

## Solving Strategies

### Node Selection Strategies

- **Best Bound** (default) - Selects node with best LP relaxation bound
- **Best Estimate** - Estimates node potential based on pseudo-costs
- **Depth First** - Explores deeply before backtracking

### Branching Strategies

- **Most Fractional** - Branches on variable closest to 0.5
- **Pseudo-Cost Branching** - Uses historical branching effectiveness

## Project Structure

```
OptiCore/
├── Models/           # Core model classes (LinearModel, Constraint, Term)
├── Solver/           # Simplex and Branch & Cut solvers
├── BranchAndBound/   # Branch & Bound framework and strategies
├── Cuts/             # Cutting plane generators (Gomory cuts)
├── Enums/            # Type definitions
└── Abstract/         # Base classes
```

## Creating a Claude Code Skill for OptiCore

You can create a [Claude Code](https://docs.anthropic.com/en/docs/claude-code) skill that teaches Claude how to use OptiCore when building optimization solutions. This lets Claude automatically apply OptiCore's API whenever an optimization problem comes up in conversation.

### What is a Claude Code Skill?

A skill is a markdown file (SKILL.md) with YAML frontmatter that Claude Code loads into context when it detects a matching task. Skills live in `~/.claude/skills/<skill-name>/SKILL.md`.

### Step 1: Create the Skill Directory

```bash
mkdir -p ~/.claude/skills/opticore-optimization
```

### Step 2: Create the SKILL.md File

Create `~/.claude/skills/opticore-optimization/SKILL.md` with the following content:

````markdown
---
name: opticore-optimization
description: Use when building .NET optimization solutions, solving linear programming (LP), integer linear programming (ILP), or mixed-integer linear programming (MILP) problems, or when the user mentions OptiCore, mathematical optimization, simplex, branch and bound, or decision variables (continuous, integer, binary)
---

# OptiCore Optimization Library

## Overview

OptiCore is a .NET optimization engine for LP, ILP, and MILP problems. No external solver dependencies required.

## Variable Types

| Type | Class | Description |
|------|-------|-------------|
| Continuous (real) | `Term` | Any decimal value |
| Integer | `IntegerTerm` with `VariableType.Integer` | Whole numbers only |
| Binary (boolean) | `IntegerTerm` with `VariableType.Binary` | 0 or 1 |

## Model Types

- `ModelType.LinearProgramming` — all continuous variables, use `OptiCoreSimplex`
- `ModelType.IntegerLinearProgramming` — all integer/binary variables, use `BranchBoundSolver`
- `ModelType.MixedIntegerLinearProgramming` — mix of continuous + integer/binary, use `BranchBoundSolver` or `BranchAndCutSolver`

## Objective Types

- `ObjectiveType.MAX` — maximization
- `ObjectiveType.MIN` — minimization

## Constraint Operators

`"<="`, `">="`, `"="`

## Quick Reference: Solving an LP

```csharp
using OptiCore.Models;
using OptiCore.Solver;
using OptiCore.Enums;

var variables = new List<Term> { new Term("x1", 0), new Term("x2", 0) };
var objective = new ModelObjective(Goal: ObjectiveType.MAX,
    Coefficients: new List<Term> { new Term("x1", 3), new Term("x2", 2) });
var constraints = new List<Constraint> {
    new Constraint("c1", new List<Term> { new Term("x1", 1), new Term("x2", 1) }, "<=", 4),
    new Constraint("c2", new List<Term> { new Term("x1", 2), new Term("x2", 1) }, "<=", 5)
};
var model = new LinearModel(ModelKind: ModelType.LinearProgramming,
    Objective: objective, ConstraintsList: constraints, Variables: variables);

var result = new OptiCoreSimplex(model).GetOptimalValues();
// result.OptimalResult = objective value, result.Terms = variable values
```

## Quick Reference: Solving MILP with Integer and Binary Variables

```csharp
using OptiCore.BranchAndBound;

var integerVars = new List<IntegerTerm> {
    new IntegerTerm("x1", 0, VariableType.Integer),
    new IntegerTerm("x2", 0, VariableType.Binary)
};
var model = new LinearModel(ModelKind: ModelType.MixedIntegerLinearProgramming,
    Objective: objective, ConstraintsList: constraints, Variables: variables);

var options = new BranchBoundOptions { MaxNodes = 100_000, MaxTimeSeconds = 3600, GapTolerance = 1e-4 };
var result = new BranchBoundSolver(model, integerVars, options).Solve();
// result.Status, result.ObjectiveValue, result.BestSolution, result.GapPercent
```

## Quick Reference: Branch & Cut (Tighter Bounds)

```csharp
var options = new BranchBoundOptions { EnableCuts = true, MaxCutRoundsPerNode = 10 };
var result = new BranchAndCutSolver(model, integerVars, options).Solve();
```

## Quick Reference: Load Model from JSON

```csharp
var loader = new LoadModelFromJson(jsonString);
var model = loader.GetLinearModel();
```

JSON `Type` values: `"linearProgramming"`, `"integerLinearProgramming"`, `"mixedIntegerLinearProgramming"`

## Pre-configured Options

- `BranchBoundOptions.Default` — balanced (100K nodes, 1h, 0.01% gap)
- `BranchBoundOptions.Quick` — fast (10K nodes, 60s, 1% gap)
- `BranchBoundOptions.Optimal` — prove optimality (1M nodes, 2h, cuts enabled)

## Strategies

Node selection: `BestBoundNodeSelection` (default), `BestEstimateNodeSelection`, `DepthFirstNodeSelection`
Branching: `MostFractionalBranching`, `PseudoCostBranching`

## Common Mistakes

- Using `OptiCoreSimplex` for integer problems — use `BranchBoundSolver` instead
- Forgetting to define `IntegerTerm` list separately from model `Variables`
- Using `ModelType.LinearProgramming` when variables are integer/binary — use `MixedIntegerLinearProgramming` or `IntegerLinearProgramming`

## Key Namespaces

- `OptiCore.Models` — Term, IntegerTerm, LinearModel, Constraint, ModelObjective, ModelResult
- `OptiCore.Solver` — OptiCoreSimplex, BranchAndCutSolver, LoadModelFromJson
- `OptiCore.BranchAndBound` — BranchBoundSolver, BranchBoundOptions, BranchBoundResult
- `OptiCore.Enums` — ModelType, ObjectiveType, VariableType
````

### Step 3: Verify the Skill

Restart Claude Code or start a new conversation. Claude will automatically detect the skill and use it when you ask about optimization problems. You can verify by asking:

```
"Create a .NET program that solves a knapsack problem using OptiCore"
```

Claude should use `IntegerTerm` with `VariableType.Binary` and `BranchBoundSolver` — not just the Simplex solver.

### Skill Customization Tips

- **Description field** — Controls when Claude loads the skill. Add domain-specific keywords relevant to your projects (e.g., "scheduling", "routing", "assignment problems").
- **Keep it concise** — Skills load into Claude's context window. Focus on API patterns and common mistakes, not explanations.
- **Update as OptiCore evolves** — When new solvers or features are added, update the skill to keep Claude current.

## Requirements

- .NET 9.0 or later

## License

MIT License

## Author

Gaston Savino - [RoosLLC](https://github.com/gsavino/OptiCore)

## Repository

https://github.com/gsavino/OptiCore
