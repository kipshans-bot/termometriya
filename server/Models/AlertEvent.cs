namespace Termometriya.Server.Models;

public enum AlertType
{
    Normal = 0,
    Warning = 1,
    Critical = 2,
    GradientWarning = 3,
    GradientCritical = 4,
    DeviationWarning = 5,
    HumidityWarning = 6,
    SensorFault = 7
}

public class AlertEvent
{
    public long Id { get; set; }
    public int SiloId { get; set; }
    public int? ThermopendantId { get; set; }
    public AlertType AlertType { get; set; }
    public int PointIndex { get; set; } = -1;
    public double Value { get; set; }
    public double Threshold { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Silo Silo { get; set; } = null!;
}
