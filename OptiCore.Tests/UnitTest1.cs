namespace OptiCore.Tests;

using System;
using Xunit;
using OptiCore;
using OptiCore.Solver;
using Xunit.Abstractions;
using OptiCore.Enums;
using OptiCore.Models;

// public class UnitTest1
// {

//     [Theory]
//     [InlineData(5, false)]   // menor que 10
//     [InlineData(10, false)]  // igual a 10
//     [InlineData(15, true)]   // mayor que 10
//     public void MyMethod_ReturnsExpectedResult(int input, bool expected)
//     {
//         // Arrange
//         var class1 = new Class1();

//         // Act
//         var result = class1.MyMethod(input);

//         // Assert
//         Assert.Equal(expected, result);
//     }
//}
public class SimplexTests
{
  private readonly ITestOutputHelper _output;

  public SimplexTests(ITestOutputHelper output)
  {
    _output = output;
  }
  [Fact]
  public void Simplex_ShouldDeserializeModel_FromJson()
  {
    // Arrange
    string jsonModel = @"
        {
          ""Type"": ""linearProgramming"",
          ""Objective"": {
            ""Goal"": ""max"",
            ""Coefficients"": [
                { ""TermName"": ""x1"", ""Coefficient"": 3.0 },
                { ""TermName"": ""x2"", ""Coefficient"": 5.0 }
              ]
          },
          ""ConstraintsList"": [
            {
              ""ConstraintName"": ""c1"",
              ""Coefficients"": [
                { ""TermName"": ""x1"", ""Coefficient"": 2.0 },
                { ""TermName"": ""x2"", ""Coefficient"": 3.0 }
              ],
              ""Operator"": ""<="",
              ""Rhs"": 12.0
            },
            {
              ""ConstraintName"": ""c2"",
              ""Coefficients"": [
                { ""TermName"": ""x1"", ""Coefficient"": -1.0 },
                { ""TermName"": ""x2"", ""Coefficient"": 1.0 }
              ],
              ""Operator"": ""<="",
              ""Rhs"": 3.0
            }
          ],
          ""Variables"": [
            { ""TermName"": ""x1"", ""Coefficient"": 0.0 },
            { ""TermName"": ""x2"", ""Coefficient"": 0.0 }
          ]
        }";


    // Act
    var simplex = new LoadModelFromJson(jsonModel);


    // Assert
    Assert.NotNull(simplex.ProcessedModel);
    Assert.Equal(ModelType.LinearProgramming, simplex.ProcessedModel.ModelKind);
    Assert.Equal(ObjectiveType.MAX, simplex.ProcessedModel.Objective.Goal);

    Assert.Equal(2, simplex.ProcessedModel.Objective.Coefficients.Count);
    //        Assert.Equal(3.0, simplex.ProcessedModel.Objective.Coefficients["x1"]);
    //        Assert.Equal(5.0, simplex.ProcessedModel.Objective.Coefficients["x2"]);

    Assert.Equal(2, simplex.ProcessedModel.ConstraintsList.Count);

    var c1 = simplex.ProcessedModel.ConstraintsList[0];
    Assert.Equal("c1", c1.ConstraintName);
    Assert.Equal("<=", c1.Operator);
    Assert.Equal(12.0, c1.Rhs);

    var c2 = simplex.ProcessedModel.ConstraintsList[1];
    Assert.Equal("c2", c2.ConstraintName);
    Assert.Equal("<=", c2.Operator);
    Assert.Equal(3.0, c2.Rhs);

    Assert.True(simplex.ProcessedModel.Variables.Exists(x => x.TermName == "x1"));
    Assert.True(simplex.ProcessedModel.Variables.Exists(x => x.TermName == "x2"));
    Assert.Equal(0.0, simplex.ProcessedModel.Variables.First(x => x.TermName == "x1").Coefficient);
    Assert.Equal(0.0, simplex.ProcessedModel.Variables.First(x => x.TermName == "x2").Coefficient);
    _output.WriteLine("============= matrix !!!====================");
    _output.WriteLine("============= matrix !!!====================");
    _output.WriteLine("============= matrix !!!====================");
    _output.WriteLine("============= matrix !!!====================");
    _output.WriteLine("============= matrix !!!====================");
    _output.WriteLine("============= matrix !!!====================");
    PrintMatrix(simplex.GetLinearModel().GetMatrix());
    _output.WriteLine("============= matrix !!!====================");
    _output.WriteLine("============= matrix !!!====================");
    _output.WriteLine("============= matrix !!!====================");
    _output.WriteLine("============= matrix !!!====================");
    _output.WriteLine("============= matrix !!!====================");
    _output.WriteLine("============= matrix !!!====================");
  }
  private void PrintMatrix(double[,] matrix)
  {
    int rows = matrix.GetLength(0);
    int cols = matrix.GetLength(1);

    for (int i = 0; i < rows; i++)
    {
      var rowValues = new List<string>();
      for (int j = 0; j < cols; j++)
      {
        rowValues.Add(matrix[i, j].ToString("0.##"));
      }
      _output.WriteLine(string.Join("\t", rowValues));
    }
  }
  string jsonModel = @"
        {
          ""Type"": ""linearProgramming"",
          ""Objective"": {
            ""Goal"": ""max"",
            ""Coefficients"": [
                { ""TermName"": ""x1"", ""Coefficient"": 3.0 },
                { ""TermName"": ""x2"", ""Coefficient"": 5.0 }
              ]
          },
          ""ConstraintsList"": [
            {
              ""ConstraintName"": ""c1"",
              ""Coefficients"": [
                { ""TermName"": ""x1"", ""Coefficient"": 2.0 },
                { ""TermName"": ""x2"", ""Coefficient"": 3.0 }
              ],
              ""Operator"": ""<="",
              ""Rhs"": 12.0
            },
            {
              ""ConstraintName"": ""c2"",
              ""Coefficients"": [
                { ""TermName"": ""x1"", ""Coefficient"": -1.0 },
                { ""TermName"": ""x2"", ""Coefficient"": 1.0 }
              ],
              ""Operator"": ""<="",
              ""Rhs"": 3.0
            }
          ],
          ""Variables"": [
            { ""TermName"": ""x1"", ""Coefficient"": 0.0 },
            { ""TermName"": ""x2"", ""Coefficient"": 0.0 }
          ]
        }";
  [Fact]
  public void LoadModel_And_GetMatrix()
  {
    var loader = new LoadModelFromJson(jsonModel);
    var matrix = loader.GetLinearModel().GetMatrix();

    PrintMatrix(matrix);

    Assert.NotNull(matrix);
    Assert.Equal(3, matrix.GetLength(0)); // 2 restricciones + 1 fila Z
    Assert.Equal(5, matrix.GetLength(1)); // 2 vars + 2 holguras + RHS
  }

  [Fact]
  public void Simplex_GetMinColumnValueFromObjective()
  {
    var loader = new LoadModelFromJson(jsonModel);
    var simplex = new OptiCoreSimplex(loader.GetLinearModel());

    PrintMatrix(simplex.SimplexMatrix);

    _output.WriteLine("============= Checking the matrix limits ===================");
    _output.WriteLine($" Maxcols: {simplex.MaxCols} MaxRows:{simplex.MaxRows}");
    _output.WriteLine("============= matrix !!!====================");

    int pivotCol = simplex.GetMinColumValueFromObjective(loader.ProcessedModel.GetNumberOfVariables());
    _output.WriteLine($"Pivot Column: {pivotCol}");
       _output.WriteLine("============= Value in Z ===================");
    _output.WriteLine($" PivotCol: {simplex.SimplexMatrix[simplex.MaxRows-1, pivotCol]} ");
    _output.WriteLine("============= matrix !!!====================");
    Assert.True(pivotCol >= -1);
  }

  [Fact]
  public void Simplex_GetPivotRow()
  {
    var loader = new LoadModelFromJson(jsonModel);
    var simplex = new OptiCoreSimplex(loader.GetLinearModel());
    _output.WriteLine("============= matrix !!!====================");
    _output.WriteLine("============= matrix !!!====================");
    PrintMatrix(simplex.SimplexMatrix);
    _output.WriteLine("============= matrix !!!====================");
    _output.WriteLine("============= matrix !!!====================");
    int pivotCol = simplex.GetMinColumValueFromObjective(loader.ProcessedModel.GetNumberOfVariables());
    _output.WriteLine("============= I'm going to call Get Pivot Row with the following value !!!====================");
    _output.WriteLine($"Pivot Column: {pivotCol}");
    int pivotRow = simplex.GetPivotRow(pivotCol);
    _output.WriteLine("============= Now I have the coordinates fo the pivot!!!!====================");
    _output.WriteLine($"Pivot Col: {pivotCol}, Pivot Row: {pivotRow}");
    _output.WriteLine($"============= yes this is the pivot {simplex.SimplexMatrix[pivotRow, pivotCol]}====================");
    Assert.True(pivotRow == 1);
  }

  [Fact]
  public void Simplex_DividePivotRow()
  {
    var loader = new LoadModelFromJson(jsonModel);
    var simplex = new OptiCoreSimplex(loader.GetLinearModel());
    _output.WriteLine($" Variables in the game: {loader.ProcessedModel.GetNumberOfVariables()}");
    int pivotCol = simplex.GetMinColumValueFromObjective(loader.ProcessedModel.GetNumberOfVariables());
    int pivotRow = simplex.GetPivotRow(pivotCol);
    _output.WriteLine("============= matrix before dividing pivot row !!!====================");
    PrintMatrix(simplex.SimplexMatrix);
    _output.WriteLine("============= matrix !!!====================");
    simplex.DividePivotRow(pivotRow, pivotCol);
    _output.WriteLine("============= matrix After dividing pivot row !!!====================");
    PrintMatrix(simplex.SimplexMatrix);

    Assert.Equal(1.0, simplex.SimplexMatrix[pivotRow, pivotCol]);
  }

  [Fact]
  public void Simplex_TransformTheRestOfTheMatrix()
  {
    var loader = new LoadModelFromJson(jsonModel);
    var simplex = new OptiCoreSimplex(loader.GetLinearModel());
    _output.WriteLine("============= matrix before dividing pivot row !!!====================");
    PrintMatrix(simplex.SimplexMatrix);
    _output.WriteLine("============= matrix !!!====================");
    int pivotCol = simplex.GetMinColumValueFromObjective(loader.ProcessedModel.GetNumberOfVariables());
    int pivotRow = simplex.GetPivotRow(pivotCol);
    _output.WriteLine("============= Now I have the coordinates fo the pivot!!!!====================");
    _output.WriteLine($"Pivot Col: {pivotCol}, Pivot Row: {pivotRow}");
    _output.WriteLine($"============= yes this is the pivot {simplex.SimplexMatrix[pivotRow, pivotCol]}====================");
    simplex.DividePivotRow(pivotRow, pivotCol);
    simplex.TransformTheRestOfTheMatrix(pivotRow, pivotCol);
    _output.WriteLine("============= This is the final matrix!!!!====================");
    PrintMatrix(simplex.SimplexMatrix);

    Assert.NotEqual(0.0, simplex.SimplexMatrix[0, 0]); // cambió algo
  }

  [Fact]
  public void Simplex_FullSolve()
  {
// First I load the model from a Json with LoadModelFromJson

    var loader = new LoadModelFromJson(jsonModel);
    _output.WriteLine($" Variables in the game: {loader.ProcessedModel.GetNumberOfVariables()}");
    _output.WriteLine($"variablesOfConstrains: {loader.ProcessedModel.GetNumberOfVariables}");

    // Now I create the solver object loading the matrix and pointing the number of variables
    var simplex = new OptiCoreSimplex(loader.GetLinearModel());

    //simplex.SolveSimplex(); ahora tengo que llamar a get

    var modelResult = simplex.GetOptimalValues();
    
    _output.WriteLine(modelResult.PrintString());
    
    PrintMatrix(simplex.SimplexMatrix);

    double result = simplex.SimplexMatrix[simplex.MaxRows - 1, simplex.MaxCols - 1];
    _output.WriteLine($"Optimal Value Z = {result}");

    Assert.True(result == 19.8);
  }
}
