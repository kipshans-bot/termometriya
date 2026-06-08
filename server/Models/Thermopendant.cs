namespace Termometriya.Server.Models;

public class Thermopendant
{
    public int Id { get; set; }
    public int SiloId { get; set; }
    public int PositionIndex { get; set; }
    public int PointCount { get; set; } = 30;
    public bool IsActive { get; set; } = true;
    public bool IsCentral { get; set; }
    public int DisplayOrder { get; set; }
    public Silo Silo { get; set; } = null!;
    public ICollection<SensorReading> Readings { get; set; } = new List<SensorReading>();
}
