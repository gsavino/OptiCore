
using OptiCore.Models;


namespace OptiCore.Solver;

public class OptiCoreSimplex
{
    public Double[,] SimplexMatrix { get; set; }
    public int MaxRows { get; set; }
    public int MaxCols { get; set; }
    public int NumberOfVariables { get; set; }
    public LinearModel MyLinearModel { get; set; }

    public ModelResult Result { get; set; }

        public OptiCoreSimplex(LinearModel myModel)
    {
        MyLinearModel = myModel;
        SimplexMatrix = myModel.GetMatrix();
        MaxRows = SimplexMatrix.GetLength(0);
        MaxCols = SimplexMatrix.GetLength(1);
        NumberOfVariables = MaxCols - MaxRows;
    }

    public ModelResult GetOptimalValues()
    {
        SolveSimplex();
        var listOfVariables = new List<ControlTerm>();
        var result = new ModelResult();
        foreach (var term in MyLinearModel.Variables) listOfVariables.Add(new ControlTerm(term.TermName, term.Coefficient, false));
        var counter = 0;
        // chechListOfVariables chequea si hay variables sin despejar o lo que es lo mismo con el valor WasProcessed en false
        while (CheckListOfVariables(listOfVariables))
        {
            // me fijo si puedo despejar y si la variable no fue procesada y tambien me fijo que no sea la diagonal porque es la variable a procesar.
            if (CheckConstraint(listOfVariables, counter))
            {
                // proceso
                double coef = SimplexMatrix[counter, MaxCols -1];
                for (int col = 0; col < listOfVariables.Count; col++)
                {
                    if (col != counter)
                    {
                        double valueOfCoef = 0;
                        if (listOfVariables[col].WasProcessed) valueOfCoef = listOfVariables[col].Coefficient;
                        coef += valueOfCoef * -1.0 * SimplexMatrix[counter, col];
                    }
                }
                result.Terms.Add(new Term(listOfVariables[counter].TermName, coef));
                listOfVariables[counter].WasProcessed = true;
                listOfVariables[counter].Coefficient = coef;
            }
            counter++;
            if (listOfVariables.Count() == counter) counter = 0;
        }
        result.OptimalResult = SimplexMatrix[MaxRows - 1, MaxCols - 1];
        return result;
    }

    public bool CheckConstraint(List<ControlTerm> list, int row)
    {
        for (int col = 0; col < MyLinearModel.Variables.Count; col++)
        {
            if (col != row)
            {
                if (!list[col].WasProcessed && SimplexMatrix[row,col] != 0 ) return false;
            }
        }
        return true;
    }
    public bool CheckListOfVariables(List<ControlTerm> listOfVariables)
    {
        foreach (var term in listOfVariables)
        {
            if (!term.WasProcessed) return true;
        }
        return false;
    }
    public void SolveSimplex()
    {
        int maxIterations = 1000;
        int pivotColumn;
        int pivotRow;
        int iteration = 0;
        pivotColumn = GetMinColumValueFromObjective(NumberOfVariables);
        while (pivotColumn != -1 && SimplexMatrix[MaxRows - 1, pivotColumn] < 0)
        {
            pivotRow = GetPivotRow(pivotColumn);
            DividePivotRow(pivotRow, pivotColumn);
            TransformTheRestOfTheMatrix(pivotRow, pivotColumn);
            pivotColumn = GetMinColumValueFromObjective(NumberOfVariables);
            iteration++;
            if (iteration == maxIterations) pivotColumn = -1;
        }
    }

    public int GetMinColumValueFromObjective(int numberOfVariables)
    {
        int minCol = 0;
        for (int col = 0; col <= numberOfVariables; col++)
        {
            if (SimplexMatrix[MaxRows - 1, col] < SimplexMatrix[MaxRows - 1, minCol] && SimplexMatrix[MaxRows - 1, col] <= 0) minCol = col;
        }
        return minCol;
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
        Double oldZ = SimplexMatrix[MaxRows - 1, pivotColumn];
        // Here I transform all the constrains, including his RHS. I don't include the pivot row because it was trasnformed diferently with the dividePivotRow Method
        for (int row = 0; row < MaxRows - 2; row++)
        {
            var coefficientPivotOfRowX = SimplexMatrix[row, pivotColumn];
            if (row == pivotRow) continue;
            for (var col = 0; col < MaxCols; col++)
            {
                SimplexMatrix[row, col] = SimplexMatrix[row, col] - coefficientPivotOfRowX * SimplexMatrix[pivotRow, col];
            }
        }
        // Now I finish the transformation changing the last row, which is the Z objective function
        for (var col = 0; col < MaxCols; col++)
        {
            SimplexMatrix[MaxRows - 1, col] = SimplexMatrix[MaxRows - 1, col] - oldZ * SimplexMatrix[pivotRow, col];
        }  
    }
}







