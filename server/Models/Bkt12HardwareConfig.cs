using System.Text.Json.Serialization;

namespace Termometriya.Server.Models;

public class Bkt12HardwareConfig
{
    [JsonPropertyName("lines")]
    public List<Bkt12Line> Lines { get; set; } = [];
}

public class Bkt12Line
{
    [JsonPropertyName("lineNumber")]
    public int LineNumber { get; set; }
    [JsonPropertyName("blocks")]
    public List<Bkt12Block> Blocks { get; set; } = [];
}

public class ModbusConnectionConfig
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "simulation";
    [JsonPropertyName("host")]
    public string? Host { get; set; }
    [JsonPropertyName("port")]
    public int Port { get; set; } = 502;
    [JsonPropertyName("portName")]
    public string? PortName { get; set; }
    [JsonPropertyName("baudRate")]
    public int BaudRate { get; set; } = 9600;
    [JsonPropertyName("dataBits")]
    public int DataBits { get; set; } = 8;
    [JsonPropertyName("parity")]
    public string Parity { get; set; } = "None";
    [JsonPropertyName("stopBits")]
    public string StopBits { get; set; } = "One";
}

public class RegisterMapConfig
{
    [JsonPropertyName("baseRegister")]
    public ushort BaseRegister { get; set; } = 15;
    [JsonPropertyName("registersPerPendant")]
    public ushort RegistersPerPendant { get; set; } = 30;
    [JsonPropertyName("thermopendants")]
    public List<ThermopendantSlotConfig> Thermopendants { get; set; } = [];
}

public class ThermopendantSlotConfig
{
    [JsonPropertyName("positionIndex")]
    public int PositionIndex { get; set; }
    [JsonPropertyName("pointCount")]
    public int PointCount { get; set; } = 30;
}

public class Bkt12Block
{
    [JsonPropertyName("slaveId")]
    public int SlaveId { get; set; }
    [JsonPropertyName("simulationPort")]
    public int SimulationPort { get; set; }
    [JsonPropertyName("connection")]
    public ModbusConnectionConfig? Connection { get; set; }
    [JsonPropertyName("siloIds")]
    public List<int> SiloIds { get; set; } = [];
    [JsonPropertyName("registerMap")]
    public RegisterMapConfig? RegisterMap { get; set; }
    [JsonIgnore]
    public List<Bkt12PendantMapping> Pendants { get; set; } = [];

    public void BuildMapping()
    {
        Pendants.Clear();
        var map = RegisterMap;
        if (map == null) return;

        foreach (var tc in map.Thermopendants)
        {
            // positionIndex 0-5 → первый силос в списке, 6-11 → второй
            int siloIdx = tc.PositionIndex < 6 ? 0 : 1;
            int siloId = siloIdx < SiloIds.Count ? SiloIds[siloIdx] : 0;
            ushort startReg = (ushort)(map.BaseRegister + Pendants.Count * map.RegistersPerPendant);
            Pendants.Add(new Bkt12PendantMapping
            {
                PendantIndex = Pendants.Count,
                SiloId = siloId,
                PositionIndex = tc.PositionIndex,
                StartRegister = startReg,
                PointCount = tc.PointCount > 0 ? tc.PointCount : 30
            });
        }
    }
}

public class Bkt12PendantMapping
{
    public int PendantIndex { get; set; }
    public int SiloId { get; set; }
    public int PositionIndex { get; set; }
    public ushort StartRegister { get; set; }
    public int PointCount { get; set; }
}
