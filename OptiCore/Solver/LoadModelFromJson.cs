using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices.ObjectiveC;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.VisualBasic;
using OptiCore.Enums;
using OptiCore.Models;


namespace OptiCore.Solver;

public class LoadModelFromJson
{
    public Model ProcessedModel = new Model(ModelKind: ModelType.LinearProgramming, Objective: new ModelObjective(Goal: ObjectiveType.MAX, Coefficients: []), ConstraintsList: [], Variables: []);

    public LoadModelFromJson(string jsonModel)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        var data = JsonSerializer.Deserialize<Model>(jsonModel, options);
        if (data == null)
        {
            throw new ArgumentException("invalid Json");
        }
        ProcessedModel = data;
        double[,] TestMatrix = GetMatrix();
    }
    public double[,] GetMatrix()
    {
        int rows = ProcessedModel.GetNumberOfConstrains() + 1;
        int cols = ProcessedModel.GetNumberOfConstrains() + ProcessedModel.GetNumberOfVariables() + 1;
        int numberOfVariables = ProcessedModel.GetNumberOfVariables();
        int numberOfConstrains = ProcessedModel.GetNumberOfConstrains();
        double[,] matrix = new double[rows, cols];

        // now I add all the constrains

        for (int i = 0; i < numberOfConstrains; i++)
        {
            for (int j = 0; j < numberOfVariables; j++)
            {
                matrix[i, j] = ProcessedModel.ConstraintsList[i].GetCoefficient(ProcessedModel.Variables[j].TermName);
            }
            for (int j = numberOfVariables; j < cols; j++)
            {
                if ((j - numberOfVariables) == i)
                {
                    matrix[i, j] = 1.0;
                }
                else
                {
                    matrix[i, j] = 0.0;
                }
            }
            matrix[i, cols - 1] = ProcessedModel.ConstraintsList[i].Rhs;
        }
        for (int j = 0; j < ProcessedModel.Variables.Count; j++)
        {
            matrix[rows - 1, j] = ProcessedModel.Objective.GetCoefficient(ProcessedModel.Variables[j].TermName) * -1;
        }
        matrix[rows - 1, cols - 1] = 0.0;
        return matrix;
    }

}
