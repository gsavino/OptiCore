namespace OptiCore.Models;


public class ControlTerm
{
    public string TermName { get; set; }
    public bool WasProcessed { get; set; }

    public double Coefficient { get; set; }

    // Constructor vacío
    public ControlTerm()
    {
        TermName = "";
        Coefficient = 0;
        WasProcessed = false;
    }

    // ✅ Constructor con parámetros
    public ControlTerm(string termName, double coefficient,bool wasProcessed = false)
    {
        TermName = termName;
        WasProcessed = wasProcessed;
        Coefficient = coefficient;
    }
}
