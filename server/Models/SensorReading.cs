namespace Termometriya.Server.Models;

public class SensorReading
{
    public long Id { get; set; }
    public int SiloId { get; set; }
    public int ThermopendantId { get; set; }
    public int PointIndex { get; set; }
    public double Temperature { get; set; }
    public double? Humidity { get; set; }
    public bool IsValid { get; set; } = true;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Silo Silo { get; set; } = null!;
    public Thermopendant Thermopendant { get; set; } = null!;
}
