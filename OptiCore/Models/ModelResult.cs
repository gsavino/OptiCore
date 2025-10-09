namespace OptiCore.Models;

public class ModelResult
{
    public List<Term> Terms  { get; set; }
    public double OptimalResult{ get; set; }

    public ModelResult()
    {
        Terms = [];
        OptimalResult = 0;
    }

    public bool VariableExists(int fila)
    {
        return fila < Terms.Count();
    }
    public void PrintResult()
    {
        Console.WriteLine("---------------------------------------------");
        Console.WriteLine("The optimal result is: " + OptimalResult);
        Console.WriteLine("---------------------------------------------");
        Console.WriteLine("The value of the variables are: ");
        foreach (var term in Terms)
        {
            Console.WriteLine(term.TermName + " = " + term.Coefficient);
        }
        Console.WriteLine("---------------------------------------------");
    }

    public override string ToString()
    {
        string toPrint = $"Z = {OptimalResult} \n";
        foreach (var term in Terms)
        {
            toPrint += $"Variable {term.TermName}  = {term.Coefficient}  \n";
        }
        return toPrint;
    }
}
    
