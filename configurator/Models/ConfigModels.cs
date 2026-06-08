using System.Text.Json.Serialization;

namespace Termometriya.Configurator.Models;

// === Elevator Config ===

public class ElevatorConfig
{
    [JsonPropertyName("cultures")] public List<CultureConfig> Cultures { get; set; } = [];
    [JsonPropertyName("lines")] public List<LineConfig> Lines { get; set; } = [];
}

public class CultureConfig
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("normTemp")] public double NormTemp { get; set; } = 25;
    [JsonPropertyName("warnTemp")] public double WarnTemp { get; set; } = 30;
    [JsonPropertyName("criticalTemp")] public double CriticalTemp { get; set; } = 35;
    [JsonPropertyName("gradientWarn")] public double GradientWarn { get; set; } = 1.0;
    [JsonPropertyName("gradientCritical")] public double GradientCritical { get; set; } = 2.0;
    [JsonPropertyName("deviationThreshold")] public double DeviationThreshold { get; set; } = 3.0;
    [JsonPropertyName("highTempThreshold")] public double HighTempThreshold { get; set; } = 30;
    [JsonPropertyName("highTempGradient")] public double HighTempGradient { get; set; } = 5.0;
    [JsonPropertyName("criticalHighTemp50")] public double CriticalHighTemp50 { get; set; } = 50;
}

public class LineConfig
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("displayOrder")] public int DisplayOrder { get; set; }
    [JsonPropertyName("silos")] public List<SiloConfig> Silos { get; set; } = [];
}

public class SiloConfig
{
    [JsonPropertyName("number")] public int Number { get; set; }
    [JsonPropertyName("fillLevel")] public double FillLevel { get; set; }
    [JsonPropertyName("capacity")] public double Capacity { get; set; } = 1000;
    [JsonPropertyName("cultureName")] public string CultureName { get; set; } = "";
    [JsonPropertyName("pendants")] public List<PendantConfig> Pendants { get; set; } = [];
}

public class PendantConfig
{
    [JsonPropertyName("positionIndex")] public int PositionIndex { get; set; }
    [JsonPropertyName("pointCount")] public int PointCount { get; set; } = 30;
    [JsonPropertyName("isCentral")] public bool IsCentral { get; set; }
}

// === BKT-12 Hardware Config ===

public class Bkt12HardwareConfig
{
    [JsonPropertyName("lines")] public List<Bkt12Line> Lines { get; set; } = [];
}

public class Bkt12Line
{
    [JsonPropertyName("lineNumber")] public int LineNumber { get; set; }
    [JsonPropertyName("blocks")] public List<Bkt12Block> Blocks { get; set; } = [];
}

public class Bkt12Block
{
    [JsonPropertyName("slaveId")] public int SlaveId { get; set; }
    [JsonPropertyName("simulationPort")] public int SimulationPort { get; set; }
    [JsonPropertyName("connection")] public ModbusConnectionConfig? Connection { get; set; }
    [JsonPropertyName("siloIds")] public List<int> SiloIds { get; set; } = [];
    [JsonPropertyName("registerMap")] public RegisterMapConfig? RegisterMap { get; set; }
}

public class ModbusConnectionConfig
{
    [JsonPropertyName("type")] public string Type { get; set; } = "simulation";
    [JsonPropertyName("host")] public string? Host { get; set; }
    [JsonPropertyName("port")] public int Port { get; set; } = 502;
    [JsonPropertyName("portName")] public string? PortName { get; set; }
    [JsonPropertyName("baudRate")] public int BaudRate { get; set; } = 9600;
    [JsonPropertyName("dataBits")] public int DataBits { get; set; } = 8;
    [JsonPropertyName("parity")] public string Parity { get; set; } = "None";
    [JsonPropertyName("stopBits")] public string StopBits { get; set; } = "One";
}

public class RegisterMapConfig
{
    [JsonPropertyName("baseRegister")] public ushort BaseRegister { get; set; } = 15;
    [JsonPropertyName("registersPerPendant")] public ushort RegistersPerPendant { get; set; } = 30;
    [JsonPropertyName("thermopendants")] public List<ThermopendantSlotConfig> Thermopendants { get; set; } = [];
}

public class ThermopendantSlotConfig
{
    [JsonPropertyName("positionIndex")] public int PositionIndex { get; set; }
    [JsonPropertyName("pointCount")] public int PointCount { get; set; } = 30;
}
