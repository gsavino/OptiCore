using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices.ObjectiveC;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.VisualBasic;


namespace OptiCore.Solver;

public class OptiCoreSimplex
{
    public Double[,] SimplexMatrix { get; set; }
    public int MaxRows { get; set; } = 0;
    public int MaxCols { get; set; } = 0;
    public int NumberOfVariables { get; set; } = 0;
    public OptiCoreSimplex(Double[,] myMatrix, int numberOfVariables)
    {
        SimplexMatrix = myMatrix;
        MaxRows = myMatrix.GetLength(0);
        MaxCols = myMatrix.GetLength(1);
        NumberOfVariables = numberOfVariables;

    }

    public void SolveSimplex()
    {
        int MaxIterations = 1000;
        int pivotColumn = 0;
        int pivotRow = 0;
        int iteration = 0;
        pivotColumn = GetMinColumValueFromObjective(NumberOfVariables);
        while (pivotColumn != -1 && SimplexMatrix[MaxRows - 1, pivotColumn] < 0)
        {
            pivotRow = GetPivotRow(pivotColumn);
            DividePivotRow(pivotRow, pivotColumn);
            TransformTheRestOfTheMatrix(pivotRow, pivotColumn);
            pivotColumn = GetMinColumValueFromObjective(NumberOfVariables);
            iteration++;
            if (iteration == MaxIterations) pivotColumn = -1;
        }


    }

    public int GetMinColumValueFromObjective(int numberOfVariables)
    {
        int MinCol = 0;
        for (int col = 0; col <= numberOfVariables; col++)
        {
            if (SimplexMatrix[MaxRows - 1, col] < SimplexMatrix[MaxRows - 1, MinCol] && SimplexMatrix[MaxRows - 1, col] <= 0) MinCol = col;
        }
        return MinCol;
    }
    public int GetPivotRow(int pivotColumn)
    {

        int selectedRow = 0;
        Double selectedValue = SimplexMatrix[selectedRow, MaxCols-1] / SimplexMatrix[selectedRow, pivotColumn];
        for (int row = 1; row < MaxRows - 1; row++)
        {
            if (selectedValue > (SimplexMatrix[row, MaxCols - 1] / SimplexMatrix[row, pivotColumn]) && SimplexMatrix[row, pivotColumn] > 0)
            {
                selectedRow = row;
                selectedValue = SimplexMatrix[row, MaxCols - 1] / SimplexMatrix[row, pivotColumn];
            }
        }

        return selectedRow;
        // if (selectedValue > 0 && selectedValue != Double.MaxValue)
        // {
        //     return selectedRow;
        // }
        // else
        // {
        //     return -1;
        // }
    }

    public void DividePivotRow(int pivotRow, int pivotColumn)
    {
        double pivot = SimplexMatrix[pivotRow, pivotColumn];
        for (int col = 0; col < MaxCols; col++)
        {
            SimplexMatrix[pivotRow, col] = SimplexMatrix[pivotRow, col] / pivot;
        }
    }
    public void TransformTheRestOfTheMatrix(int pivotRow, int pivotColumn)
    {
        Double OldZ = SimplexMatrix[MaxRows - 1, pivotColumn];
        Double CoefficientPivoteOfRowX = 0.0;
        // Here I transform all the constrains, including his RHS. I don't include the pivot row because it was trasnformed diferently with the dividePivotRow Method
        for (int row = 0; row < MaxRows - 2; row++)
        {
            CoefficientPivoteOfRowX = SimplexMatrix[row, pivotColumn];
            if (row != pivotRow)
            {
                for (int col = 0; col < MaxCols; col++)
                {
                    SimplexMatrix[row, col] = SimplexMatrix[row, col] - CoefficientPivoteOfRowX * SimplexMatrix[pivotRow, col];
                }
            }
        }
        // Now I finish the transformation changing the last row, which is the Z objective function
        for (int col = 0; col < MaxCols; col++)
        {
            SimplexMatrix[MaxRows - 1, col] = SimplexMatrix[MaxRows - 1, col] - OldZ * SimplexMatrix[pivotRow, col];
        }  
    }
}







