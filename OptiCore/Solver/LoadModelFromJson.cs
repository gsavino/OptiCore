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

/// <summary>
/// Utility class that deserializes a JSON string into a <see cref="LinearModel"/>.
/// Provides a convenient way to define optimization problems in JSON format and load
/// them into the solver. Uses System.Text.Json with case-insensitive property matching
/// and camelCase enum conversion.
/// </summary>
public class LoadModelFromJson
{
    /// <summary>
    /// The deserialized <see cref="LinearModel"/>, ready to be passed to the simplex solver.
    /// </summary>
    public LinearModel ProcessedModel = new LinearModel(ModelKind: ModelType.LinearProgramming, Objective: new ModelObjective(Goal: ObjectiveType.MAX, Coefficients: []), ConstraintsList: [], Variables: []);

    /// <summary>
    /// Takes a JSON string and deserializes it into a <see cref="LinearModel"/>.
    /// Throws <see cref="ArgumentException"/> if the JSON is invalid or cannot be parsed.
    /// </summary>
    /// <param name="jsonModel">A JSON string representing the linear programming model.</param>
    /// <exception cref="ArgumentException">Thrown when the JSON string is null, empty, or cannot be deserialized.</exception>
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

    /// <summary>
    /// Returns the deserialized <see cref="LinearModel"/> for use with the solver.
    /// </summary>
    /// <returns>The linear model parsed from the JSON input.</returns>
    public LinearModel GetLinearModel()
    {
        return ProcessedModel;
    }
   
}
