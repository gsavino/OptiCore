🧠 OptiCore

  A lightweight C#9 library for solving Linear Programming (LP) problems using the Simplex algorithm.

    OptiCore provides a simple, modern API to define and solve linear optimization problems.
    You can define models using plain C# objects or load them directly from JSON — then solve them using a clean, object-oriented Simplex engine.

🚀 Features

    Define Linear Programming (LP) models with minimal code.

    Load models from JSON or build them programmatically.

    Solve LP problems via a built-in Simplex implementation.

    Retrieve both the optimal result and variable assignments.

    Fully compatible with .NET 5+ / C# 9.

📦 Installation
dotnet add package OptiCore

🧩 1. Loading and Creating Linear Models

    You can load or define a linear model in two ways.

1.1 Load from JSON

    Use LoadModelFromJson to transform a JSON string or file into a LinearModel object.

var loader = new LoadModelFromJson();
LinearModel model = loader.FromString(jsonModel);

Example JSON
{
  "Type": "linearProgramming",
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

1.2 Create the Model Directly in C#

  Alternatively, you can create a model directly using C# records.

var model = new LinearModel(
    ModelType.Maximization,
    new ModelObjective(new List<Term> {
        new Term("x1", 3.0),
        new Term("x2", 5.0)
    }),
    new List<Constraint> {
        new Constraint(
            "c1",
            new List<Term> {
                new Term("x1", 2.0),
                new Term("x2", 3.0)
            },
            "<=",
            12.0
        ),
        new Constraint(
            "c2",
            new List<Term> {
                new Term("x1", -1.0),
                new Term("x2", 1.0)
            },
            "<=",
            3.0
        )
    },
    new List<Term> {
        new Term("x1", 0.0),
        new Term("x2", 0.0)
    }
);

⚙️ 2. Solving the Model

  Once your model is ready, use the OptiCoreSimplex solver to compute the optimal solution.

    2.1 Load the Model into the Solver

      Pass your LinearModel instance to the solver’s constructor:

      var solver = new OptiCoreSimplex(model);

    2.2 Get the Optimal Solution

      Call GetOptimalValues() to solve and retrieve the results as a ModelResult object.

      ModelResult result = solver.GetOptimalValues();
      Console.WriteLine(result);

      🧮 Example Output
      Z = 27
      Variable x1 = 3
      Variable x2 = 3

      🧱 Core Classes
      Term

      Represents a single variable and its coefficient.

      public record Term(
          string TermName,
          double Coefficient
      );

  Constraint

    Represents a linear constraint of the model.

      public record Constraint(
      string ConstraintName,
      List<Term> Coefficients,
      string Operator,
      double Rhs
    ) : ConstraintBase
    {
        public override List<Term> Coefficients { get; init; } = Coefficients;
    }

  ModelResult

    Contains the solution result of a solved linear model.

public class ModelResult
{
    public List<Term> Terms { get; set; }
    public double OptimalResult { get; set; }

    public ModelResult()
    {
        Terms = [];
        OptimalResult = 0;
    }

    public override string ToString()
    {
        string toPrint = $"Z = {OptimalResult}\n";
        foreach (var term in Terms)
        {
            toPrint += $"Variable {term.TermName} = {term.Coefficient}\n";
        }
        return toPrint;
    }
}

🧠 Quick Example (End-to-End)
using OptiCore;

// Load model from JSON
{
  "Type": "linearProgramming",
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


var loader = new LoadModelFromJson();
LinearModel model = loader.FromString(jsonModel);

// Solve
var solver = new OptiCoreSimplex(model);
var result = solver.GetOptimalValues();

Console.WriteLine(result);

    🔮 Future Enhancements

          Mixed-Integer Linear Programming (MILP) support

          Dual Simplex method

          External solver integration

          Sensitivity analysis
