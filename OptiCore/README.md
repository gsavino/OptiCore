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

## Requirements

- .NET 9.0 or later

## License

MIT License

## Author

Gaston Savino - [RoosLLC](https://github.com/gsavino/OptiCore)

## Repository

https://github.com/gsavino/OptiCore
