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
    
