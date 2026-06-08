namespace Termometriya.Server.Models;

public class Silo
{
    public int Id { get; set; }
    public int LineId { get; set; }
    public int Number { get; set; }
    public int CultureId { get; set; }
    public double FillLevel { get; set; }
    public double Capacity { get; set; }
    public int? GrainLevelPointIndex { get; set; }
    public DateTime? GrainLevelLastChecked { get; set; }
    public ElevatorLine Line { get; set; } = null!;
    public Culture Culture { get; set; } = null!;
    public ICollection<Thermopendant> Thermopendants { get; set; } = new List<Thermopendant>();
    public ICollection<SensorReading> Readings { get; set; } = new List<SensorReading>();
    public ICollection<AlertEvent> Alerts { get; set; } = new List<AlertEvent>();
}
