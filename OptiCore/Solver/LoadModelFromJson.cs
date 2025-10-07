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
    public LinearModel ProcessedModel = new LinearModel(ModelKind: ModelType.LinearProgramming, Objective: new ModelObjective(Goal: ObjectiveType.MAX, Coefficients: []), ConstraintsList: [], Variables: []);
    public LoadModelFromJson(string jsonModel)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        var data = JsonSerializer.Deserialize<LinearModel>(jsonModel, options);
        if (data == null)
        {
            throw new ArgumentException("invalid Json");
        }
        ProcessedModel = data;
    }

    public LinearModel GetLinearModel()
    {
        return ProcessedModel;
    }
   
}
