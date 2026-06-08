using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Termometriya.Server.Data;
using Termometriya.Server.Models;

namespace Termometriya.Server.Services.DqtService;

public class ModbusTcpSimulator : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ModbusTcpSimulator> _logger;
    private readonly string _configPath;
    private Bkt12HardwareConfig _config = new();
    private readonly ConcurrentDictionary<int, SimulatedBlockState> _blockStates = new();
    private readonly List<TcpListener> _listeners = [];
    private readonly ConcurrentDictionary<int, short[]> _simulationMemory = new();

    public ModbusTcpSimulator(IServiceScopeFactory scopeFactory, ILogger<ModbusTcpSimulator> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "bkt12-config.jsonc");
        if (!File.Exists(_configPath))
            _configPath = Path.Combine(Directory.GetCurrentDirectory(), "bkt12-config.jsonc");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ModbusTcpSimulator starting...");
        await LoadConfig();
        foreach (var line in _config.Lines)
        {
            foreach (var block in line.Blocks)
            {
                block.BuildMapping();
                var state = new SimulatedBlockState { Block = block };
                _blockStates[block.SlaveId] = state;

                if (block.Connection?.Type == "simulation")
                {
                    StartSimulationListener(block);
                }
            }
        }
        _logger.LogInformation("ModbusTcpSimulator started with {Count} blocks", _config.Lines.Sum(l => l.Blocks.Count));
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var listener in _listeners)
        {
            try { listener.Stop(); } catch { }
        }
        return Task.CompletedTask;
    }

    private async Task LoadConfig()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = await File.ReadAllTextAsync(_configPath);
                var options = new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip };
                _config = JsonSerializer.Deserialize<Bkt12HardwareConfig>(json, options) ?? new();
                _logger.LogInformation("Loaded config from {Path}", _configPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load config");
        }
    }

    private void StartSimulationListener(Bkt12Block block)
    {
        var port = block.SimulationPort > 0 ? block.SimulationPort : 1500 + block.SlaveId;
        try
        {
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            _listeners.Add(listener);
            _ = Task.Run(() => HandleSimulationClientAsync(listener, block));
            _logger.LogInformation("Simulation listener on port {Port} for slave {Slave}", port, block.SlaveId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Port {Port} in use, simulation will use internal memory", port);
        }
    }

    private async Task HandleSimulationClientAsync(TcpListener listener, Bkt12Block block)
    {
        while (true)
        {
            try
            {
                using var client = await listener.AcceptTcpClientAsync();
                client.NoDelay = true;
                _ = Task.Run(() => HandleModbusConnectionAsync(client, block));
            }
            catch { break; }
        }
    }

    private async Task HandleModbusConnectionAsync(TcpClient client, Bkt12Block block)
    {
        var stream = client.GetStream();
        var buffer = new byte[256];
        try
        {
            while (client.Connected)
            {
                var read = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (read < 2) break;

                int slaveId = buffer[0];
                int functionCode = buffer[1];

                if (slaveId != block.SlaveId) continue;

                if (functionCode == 3 || functionCode == 4)
                {
                    ushort startReg = (ushort)((buffer[2] << 8) | buffer[3]);
                    ushort regCount = (ushort)((buffer[4] << 8) | buffer[5]);
                    await SendReadResponseAsync(stream, slaveId, functionCode, block, startReg, regCount);
                }
            }
        }
        catch { }
    }

    private async Task SendReadResponseAsync(NetworkStream stream, int slaveId, int functionCode,
        Bkt12Block block, ushort startReg, ushort regCount)
    {
        var data = new byte[regCount * 2];
        var regs = _simulationMemory.GetOrAdd(slaveId, _ => GenerateNormalTemperatures(block));

        for (int i = 0; i < regCount && (startReg + i) < regs.Length; i++)
        {
            int idx = startReg + i;
            if (idx < regs.Length)
            {
                data[i * 2] = (byte)((regs[idx] >> 8) & 0xFF);
                data[i * 2 + 1] = (byte)(regs[idx] & 0xFF);
            }
        }

        var response = new byte[5 + data.Length + 2];
        response[0] = (byte)slaveId;
        response[1] = (byte)functionCode;
        response[2] = (byte)data.Length;
        Array.Copy(data, 0, response, 3, data.Length);
        ushort crc = CalculateCrc(response, 0, response.Length - 2);
        response[^2] = (byte)(crc & 0xFF);
        response[^1] = (byte)((crc >> 8) & 0xFF);

        try { await stream.WriteAsync(response); } catch { }
    }

    private short[] GenerateNormalTemperatures(Bkt12Block block)
    {
        var regs = new short[375];
        var rand = new Random(block.SlaveId * 100);
        foreach (var pendant in block.Pendants)
        {
            double baseTemp = 18 + rand.NextDouble() * 5;
            for (int p = 0; p < pendant.PointCount; p++)
            {
                int regIdx = pendant.StartRegister + p;
                double temp = baseTemp + (p * 0.3) + (rand.NextDouble() - 0.5) * 2;
                if (regIdx < regs.Length)
                    regs[regIdx] = (short)(temp * 16);
            }
        }
        return regs;
    }

    public async Task<List<SensorReading>> PollAsync()
    {
        var readings = new List<SensorReading>();
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pendants = await db.Thermopendants.Include(t => t.Silo).ToListAsync();

        var hour = DateTime.UtcNow.Hour;
        var dailyDelta = 5 * Math.Sin((hour - 6) * Math.PI / 12);

        foreach (var line in _config.Lines)
        {
            foreach (var block in line.Blocks)
            {
                var regs = _simulationMemory.GetOrAdd(block.SlaveId, _ => GenerateNormalTemperatures(block));
                var rand = new Random();

                foreach (var pendant in block.Pendants)
                {
                    var dbPendant = pendants.FirstOrDefault(p =>
                        p.SiloId == pendant.SiloId && p.PositionIndex == pendant.PositionIndex);
                    if (dbPendant == null || !dbPendant.IsActive) continue;

                    int pointCount = Math.Min(pendant.PointCount, dbPendant.PointCount);
                    for (int p = 0; p < pointCount; p++)
                    {
                        int regIdx = pendant.StartRegister + p;
                        double temp = 20 + (rand.NextDouble() - 0.5) * 4;

                        if (regIdx < regs.Length)
                        {
                            temp = regs[regIdx] / 16.0;
                            temp += (rand.NextDouble() - 0.5) * 0.5;
                        }

                        int grainLine = (pendant.PointCount * 2) / 3;
                        if (p >= grainLine)
                        {
                            temp += dailyDelta;
                        }

                        readings.Add(new SensorReading
                        {
                            SiloId = dbPendant.SiloId,
                            ThermopendantId = dbPendant.Id,
                            PointIndex = p,
                            Temperature = Math.Round(temp, 1),
                            Humidity = 45 + rand.NextDouble() * 20,
                            IsValid = true,
                            Timestamp = DateTime.UtcNow,
                            Silo = dbPendant.Silo,
                            Thermopendant = dbPendant
                        });
                    }
                }
            }
        }

        return readings;
    }

    public void SetScenario(int siloId, string scenario, Bkt12Block? block = null)
    {
        foreach (var b in _config.Lines.SelectMany(l => l.Blocks))
        {
            if (!b.SiloIds.Contains(siloId)) continue;
            var regs = _simulationMemory.GetOrAdd(b.SlaveId, _ => GenerateNormalTemperatures(b));
            var rand = new Random(siloId * 100);
            foreach (var pendant in b.Pendants.Where(p => p.SiloId == siloId))
            {
                double baseTemp = scenario switch
                {
                    "heating" => 30 + rand.NextDouble() * 3,
                    "selfheating" => 28 + rand.NextDouble() * 8,
                    "critical" => 38 + rand.NextDouble() * 12,
                    _ => 18 + rand.NextDouble() * 5
                };
                for (int p = 0; p < pendant.PointCount; p++)
                {
                    int regIdx = pendant.StartRegister + p;
                    if (regIdx < regs.Length)
                        regs[regIdx] = (short)((baseTemp + p * 0.2 + (rand.NextDouble() - 0.5) * 2) * 16);
                }
            }
        }
    }

    public void ResetAll()
    {
        foreach (var key in _simulationMemory.Keys.ToList())
        {
            var block = _config.Lines.SelectMany(l => l.Blocks).FirstOrDefault(b => b.SlaveId == key);
            if (block != null)
                _simulationMemory[key] = GenerateNormalTemperatures(block);
        }
    }

    public Bkt12HardwareConfig GetConfig() => _config;

    public async Task SaveConfigAsync(Bkt12HardwareConfig config)
    {
        _config = config;
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_configPath, json);
        foreach (var line in _config.Lines)
            foreach (var block in line.Blocks)
                block.BuildMapping();
    }

    public static ushort CalculateCrc(byte[] data, int offset, int length)
    {
        ushort crc = 0xFFFF;
        for (int i = offset; i < offset + length; i++)
        {
            crc ^= data[i];
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 1) != 0)
                    crc = (ushort)((crc >> 1) ^ 0xA001);
                else
                    crc >>= 1;
            }
        }
        return crc;
    }

    private class SimulatedBlockState
    {
        public Bkt12Block Block { get; set; } = null!;
    }
}
