using System.Text.Json.Serialization;

namespace Termometriya.Server.Models;

/// <summary>
/// Единый конфиг системы термометрии.
/// Заменяет Bkt12HardwareConfig и ElevatorConfig.
/// Файл: termometriya-config.jsonc
/// </summary>
public class ThermometryConfig
{
    public List<CultureConfig> Cultures { get; set; } = [];
    public List<LineConfig> Lines { get; set; } = [];
}

public class CultureConfig
{
    public string Name { get; set; } = string.Empty;
    public double NormTemp { get; set; } = 25;
    public double WarnTemp { get; set; } = 30;
    public double CriticalTemp { get; set; } = 35;
    public double GradientWarn { get; set; } = 1.0;
    public double GradientCritical { get; set; } = 2.0;
    public double DeviationThreshold { get; set; } = 3.0;
    public double HighTempThreshold { get; set; } = 30;
    public double HighTempGradient { get; set; } = 5.0;
    public double CriticalHighTemp50 { get; set; } = 50;
}

public class LineConfig
{
    public string Name { get; set; } = "";
    public int DisplayOrder { get; set; }
    public bool Enabled { get; set; } = true;
    public string Protocol { get; set; } = "RTU";
    public string ComPort { get; set; } = "";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "Even";
    public string StopBits { get; set; } = "One";
    public string IpAddress { get; set; } = "";
    public int IpPort { get; set; } = 502;
    public List<BlockConfig> Blocks { get; set; } = [];
}

public class BlockConfig
{
    public int SlaveId { get; set; }
    public List<SiloConfig> Silos { get; set; } = [];
    [JsonIgnore]
    public List<PendantMapping> Pendants { get; set; } = [];

    public void BuildMapping()
    {
        Pendants.Clear();
        foreach (var silo in Silos)
            foreach (var sensor in silo.Sensors)
            {
                ushort startReg = (ushort)(15 + sensor.CableInput * 30);
                Pendants.Add(new PendantMapping
                {
                    SiloId = silo.Number,
                    PositionIndex = sensor.CableInput,
                    StartRegister = startReg,
                    PointCount = sensor.Points
                });
            }
    }
}

public class SiloConfig
{
    public int Number { get; set; }
    public string Culture { get; set; } = "";
    public double FillLevel { get; set; }
    public double Capacity { get; set; } = 1000;
    public List<SensorConfig> Sensors { get; set; } = [];
}

public class SensorConfig
{
    public int CableInput { get; set; }
    public int Points { get; set; } = 30;
    public bool IsCentral { get; set; }
}

/// <summary>Маппинг для сопоставления Modbus-регистров с термоподвесами в БД</summary>
public class PendantMapping
{
    public int SiloId { get; set; }
    public int PositionIndex { get; set; }
    public ushort StartRegister { get; set; }
    public int PointCount { get; set; }
}
