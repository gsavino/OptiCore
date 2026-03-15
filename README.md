# OptiCore - nuget.org
# See CLaude Code Skills down in this file!

A lightweight, open-source optimization engine for solving **Linear Programming (LP)**, **Integer Linear Programming (ILP)**, and **Mixed-Integer Linear Programming (MILP)** problems in .NET.

## Overview

OptiCore provides a native .NET solution for mathematical optimization without external dependencies on commercial solvers. It supports **continuous (real)**, **integer**, and **binary (boolean)** decision variables, making it suitable for a wide range of optimization problems — from simple resource allocation to complex scheduling and combinatorial optimization.

### Supported Problem Types

| Problem Type | Variable Types | Solver |
|---|---|---|
| **Linear Programming (LP)** | Continuous (real) variables | Simplex |
| **Integer Linear Programming (ILP)** | Integer and/or binary variables | Branch & Bound |
| **Mixed-Integer Linear Programming (MILP)** | Mix of continuous, integer, and binary variables | Branch & Bound / Branch & Cut |

## Features

- **Linear Programming (LP)** — Continuous optimization using the Simplex method with Big-M support
- **Integer Linear Programming (ILP)** — Pure integer problems solved with Branch & Bound
- **Mixed-Integer Linear Programming (MILP)** — Combined continuous, integer, and binary variables
- **Binary (Boolean) Variables** — Model yes/no decisions, assignments, and logical constraints
- **Branch & Cut** — Enhanced solving with Gomory cutting planes for tighter bounds
- **Pluggable Strategies** — Customizable branching and node selection strategies
- **Solution Pool** — Track multiple best solutions during optimization
- **JSON Model Loading** — Define models in JSON and load them directly
- **Maximization and Minimization** — Support for both objective directions

## Variable Types

OptiCore supports three types of decision variables:

| Type | Description | Example Use Cases |
|---|---|---|
| **Continuous** | Real-valued (any decimal value) | Production quantities, blending ratios |
| **Integer** | Whole numbers only | Number of trucks, staff shifts |
| **Binary** | 0 or 1 (boolean) | Yes/no decisions, facility open/close, task assignments |

## Installation

```bash
dotnet add package OptiCore
```

## Quick Start

### 1. Solving a Linear Program (Continuous Variables)

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

### 2. Solving an Integer/Mixed-Integer Program

Use `IntegerTerm` to define integer and binary variables, then solve with the Branch & Bound solver:

```csharp
using OptiCore.BranchAndBound;
using OptiCore.Models;
using OptiCore.Enums;

// Define integer and binary variables
var integerVars = new List<IntegerTerm>
{
    new IntegerTerm("x1", 0, VariableType.Integer),  // Integer variable
    new IntegerTerm("x2", 0, VariableType.Binary)     // Binary (boolean) variable: 0 or 1
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

### 3. Using Branch & Cut (for Tighter Bounds)

Branch & Cut combines Branch & Bound with Gomory cutting planes for improved performance on difficult problems:

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

### 4. Loading a Model from JSON

Define your optimization model in JSON — supports LP, ILP, and MILP problem types:

```csharp
var loader = new LoadModelFromJson(jsonModel);
var model = loader.GetLinearModel();
```

**Example JSON:**

```json
{
  "Type": "mixedIntegerLinearProgramming",
  "Objective": {
    "Goal": "max",
    "Coefficients": [
      { "TermName": "x1", "Coefficient": 3.0 },
      { "TermName": "x2", "Coefficient": 5.0 }
    ]
  },
  "ConstraintsList": [
    {
      "ConstraintName": "c1",
      "Coefficients": [
        { "TermName": "x1", "Coefficient": 2.0 },
        { "TermName": "x2", "Coefficient": 3.0 }
      ],
      "Operator": "<=",
      "Rhs": 12.0
    },
    {
      "ConstraintName": "c2",
      "Coefficients": [
        { "TermName": "x1", "Coefficient": -1.0 },
        { "TermName": "x2", "Coefficient": 1.0 }
      ],
      "Operator": "<=",
      "Rhs": 3.0
    }
  ],
  "Variables": [
    { "TermName": "x1", "Coefficient": 0.0 },
    { "TermName": "x2", "Coefficient": 0.0 }
  ]
}
```

Supported `Type` values: `"linearProgramming"`, `"integerLinearProgramming"`, `"mixedIntegerLinearProgramming"`

Supported constraint operators: `"<="`, `">="`, `"="`

## Configuration Options

| Option | Default | Description |
|---|---|---|
| `MaxNodes` | 100,000 | Maximum nodes to explore |
| `MaxTimeSeconds` | 3600 | Time limit in seconds |
| `GapTolerance` | 1e-4 | Relative optimality gap |
| `AbsoluteGapTolerance` | 1e-6 | Absolute optimality gap |
| `IntegralityTolerance` | 1e-6 | Tolerance for integer feasibility |
| `SolutionPoolSize` | 5 | Number of best solutions to keep |
| `EnableCuts` | false | Enable cutting plane generation |
| `MaxCutRoundsPerNode` | 10 | Cut generation rounds per node |
| `MinCutImprovement` | 1e-4 | Minimum LP improvement to continue cuts |

### Pre-configured Presets

```csharp
BranchBoundOptions.Default  // Balanced settings
BranchBoundOptions.Quick    // 10K nodes, 60s, 1% gap — faster solving
BranchBoundOptions.Optimal  // 1M nodes, 2h, 1e-6 gap, cuts enabled — prove optimality
```

## Solving Strategies

### Node Selection Strategies

- **Best Bound** (default) — Selects node with best LP relaxation bound
- **Best Estimate** — Hybrid strategy balancing bound quality and search depth
- **Depth First** — Explores deeply before backtracking; finds feasible solutions quickly

### Branching Strategies

- **Most Fractional** — Branches on variable closest to 0.5
- **Pseudo-Cost Branching** — Learns from historical branching effectiveness

## Project Structure

```
OptiCore/
├── Models/           # Core model classes (LinearModel, Term, IntegerTerm, Constraint)
├── Solver/           # Simplex and Branch & Cut solvers
├── BranchAndBound/   # Branch & Bound framework, strategies, and solution pool
├── Cuts/             # Cutting plane generators (Gomory fractional & mixed-integer)
├── Enums/            # Type definitions (VariableType, ModelType, ObjectiveType)
└── Abstract/         # Base classes
```

## Requirements

- .NET 9.0 or later

## License

MIT License

## Author

Gaston Savino — [RoosLLC](https://github.com/gsavino/OptiCore)

## Repository

https://github.com/gsavino/OptiCore
