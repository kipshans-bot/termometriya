namespace Termometriya.Server.Models;

public class Culture
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double NormTemp { get; set; } = 25;
    public double WarnTemp { get; set; } = 30;
    public double CriticalTemp { get; set; } = 35;
    public double GradientWarn { get; set; } = 1.0;
    public double GradientCritical { get; set; } = 2.0;
    public double HighTempGradient { get; set; } = 5.0;
    public double DeviationThreshold { get; set; } = 3.0;
    public double CriticalHighTemp50 { get; set; } = 50;
    public double HighTempThreshold { get; set; } = 30;
    public bool SoundEnabled { get; set; } = true;
    public bool EmailEnabled { get; set; }
    public string EmailRecipients { get; set; } = string.Empty;
    public ICollection<Silo> Silos { get; set; } = new List<Silo>();
}
