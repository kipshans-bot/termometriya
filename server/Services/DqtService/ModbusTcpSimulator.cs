using System.Collections.Concurrent;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
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
    private ThermometryConfig _config = new();
    private readonly ConcurrentDictionary<int, SimulatedBlockState> _blockStates = new();
    private readonly List<TcpListener> _listeners = [];
    private readonly ConcurrentDictionary<int, short[]> _simulationMemory = new();

    public ModbusTcpSimulator(IServiceScopeFactory scopeFactory, ILogger<ModbusTcpSimulator> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "termometriya-config.jsonc");
        if (!File.Exists(_configPath))
            _configPath = Path.Combine(Directory.GetCurrentDirectory(), "termometriya-config.jsonc");
        if (!File.Exists(_configPath))
            _configPath = Path.Combine(AppContext.BaseDirectory, "termometriya-config.jsonc");
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
        if (File.Exists(_configPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_configPath);
                var options = new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip, PropertyNameCaseInsensitive = true };
                _config = JsonSerializer.Deserialize<ThermometryConfig>(json, options) ?? new();
                _logger.LogInformation("Loaded config from {Path}", _configPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load config");
            }
        }
        else
        {
            _logger.LogWarning("Config file not found at {Path}", _configPath);
        }
    }

    private void StartSimulationListener(BlockConfig block)
    {
        var port = 1500 + block.SlaveId;
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

    private async Task HandleSimulationClientAsync(TcpListener listener, BlockConfig block)
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

    private async Task HandleModbusConnectionAsync(TcpClient client, BlockConfig block)
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
        BlockConfig block, ushort startReg, ushort regCount)
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

    private short[] GenerateNormalTemperatures(BlockConfig block)
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
        var siloNumbers = pendants.Select(p => $"(SiloId={p.SiloId}, Silo.Number={p.Silo?.Number}, Pos={p.PositionIndex})").Distinct().ToList();
        _logger.LogInformation("PollAsync: {Count} thermopendants, samples: {Samples}",
            pendants.Count, string.Join("; ", siloNumbers.Take(30)));

        var hour = DateTime.UtcNow.Hour;
        var dailyDelta = 5 * Math.Sin((hour - 6) * Math.PI / 12);

        int enabledLines = _config.Lines.Count(l => l.Enabled);
        var cfgSilos = _config.Lines.Where(l => l.Enabled).SelectMany(l => l.Blocks).SelectMany(b => b.Pendants)
            .Select(p => $"(siloId={p.SiloId}, pos={p.PositionIndex})").Distinct().ToList();
        _logger.LogInformation("PollAsync: {LineCount} lines, {TotalBlocks} blocks, config pendants: {Cfg}",
            enabledLines, _config.Lines.Where(l => l.Enabled).Sum(l => l.Blocks.Count),
            string.Join("; ", cfgSilos.Take(40)));

        foreach (var line in _config.Lines.Where(l => l.Enabled))
        {
            foreach (var block in line.Blocks)
            {
                short[]? regs = null;
                var rand = new Random();

                if (string.Equals(line.Protocol, "TCP", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(line.IpAddress))
                {
                    _logger.LogInformation("TCP reading slave {Slave} at {Host}:{Port}...",
                        block.SlaveId, line.IpAddress, line.IpPort);
                    try
                    {
                        regs = await ReadTcpBlockRegistersAsync(block, line.IpAddress, line.IpPort, TimeSpan.FromSeconds(5));
                        _logger.LogInformation("TCP slave {Slave} OK ({PendantCount} pendants)",
                            block.SlaveId, block.Pendants.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "TCP read failed for slave {Slave} at {Host}:{Port}",
                            block.SlaveId, line.IpAddress, line.IpPort);
                    }
                }
                else if (!string.IsNullOrEmpty(line.ComPort))
                {
                    _logger.LogInformation("RTU reading slave {Slave} on {PortName}...",
                        block.SlaveId, line.ComPort);
                    try
                    {
                        regs = await ReadRtuBlockRegistersAsync(block, line, TimeSpan.FromSeconds(3));
                        _logger.LogInformation("RTU slave {Slave} OK ({PendantCount} pendants)",
                            block.SlaveId, block.Pendants.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "RTU read failed for slave {Slave} on {PortName}",
                            block.SlaveId, line.ComPort);
                    }
                }

                if (regs == null)
                {
                    _logger.LogInformation("Block slave {Slave}: no register data, skipping", block.SlaveId);
                    continue;
                }

                int matched = 0, skipped = 0;
                foreach (var pendant in block.Pendants)
                {
                    var dbPendant = pendants.FirstOrDefault(p =>
                        p.Silo.Number == pendant.SiloId && p.PositionIndex == pendant.PositionIndex);
                    if (dbPendant == null || !dbPendant.IsActive) { skipped++; continue; }
                    matched++;

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

                        bool pointValid = regIdx < regs.Length
                            ? temp <= 80 && regs[regIdx] != unchecked((short)0xAAAA)
                            : temp <= 80;

                        readings.Add(new SensorReading
                        {
                            SiloId = dbPendant.SiloId,
                            ThermopendantId = dbPendant.Id,
                            PointIndex = p,
                            Temperature = Math.Round(temp, 1),
                            Humidity = 45 + rand.NextDouble() * 20,
                            IsValid = pointValid,
                            Timestamp = DateTime.UtcNow,
                            Silo = dbPendant.Silo,
                            Thermopendant = dbPendant
                        });
                    }
                }
                _logger.LogInformation("Block slave {Slave}: {Matched} matched, {Skipped} skipped from {Total} pendants",
                    block.SlaveId, matched, skipped, block.Pendants.Count);
            }
        }

        _logger.LogDebug("PollAsync: {Count} lines, {Readings} readings", _config.Lines.Count(l => l.Enabled), readings.Count);
        return readings;
    }

    private async Task<short[]> ReadTcpBlockRegistersAsync(BlockConfig block, string host, int port, TimeSpan timeout)
    {
        using var tcpClient = new TcpClient();
        tcpClient.ReceiveTimeout = (int)timeout.TotalMilliseconds;
        tcpClient.SendTimeout = (int)timeout.TotalMilliseconds;

        var connectTask = tcpClient.ConnectAsync(host, port);
        if (await Task.WhenAny(connectTask, Task.Delay(timeout)) != connectTask)
            throw new TimeoutException($"TCP connect to {host}:{port} timed out");
        await connectTask;

        var regs = new short[375];
        var stream = tcpClient.GetStream();
        byte unitId = (byte)block.SlaveId;
        const byte functionCode = 3;
        ushort transactionId = 1;

        foreach (var pendant in block.Pendants.Where(p => p.PointCount > 0))
        {
            ushort regCount = (ushort)pendant.PointCount;
            ushort startReg = pendant.StartRegister;
            bool success = false;

            for (int attempt = 0; attempt < 5 && !success; attempt++)
            {
                try
                {
                    var request = new byte[12];
                    request[0] = (byte)(transactionId >> 8);
                    request[1] = (byte)(transactionId++);
                    request[2] = 0;
                    request[3] = 0;
                    request[4] = 0;
                    request[5] = 6;
                    request[6] = unitId;
                    request[7] = functionCode;
                    request[8] = (byte)(startReg >> 8);
                    request[9] = (byte)(startReg);
                    request[10] = (byte)(regCount >> 8);
                    request[11] = (byte)(regCount);

                    var writeTask = stream.WriteAsync(request).AsTask();
                    if (await Task.WhenAny(writeTask, Task.Delay(timeout)) != writeTask)
                        throw new TimeoutException("TCP write timed out");
                    await writeTask;

                    var mbap = new byte[7];
                    var readTask = ReadExactAsync(stream, mbap, 0, 7);
                    if (await Task.WhenAny(readTask, Task.Delay(timeout)) != readTask)
                        throw new TimeoutException("TCP read MBAP timed out");
                    await readTask;

                    int responseLen = (mbap[4] << 8) | mbap[5];
                    int pduLen = responseLen - 1;

                    if (pduLen <= 0)
                        throw new InvalidOperationException($"Invalid PDU length {pduLen}");

                    var pdu = new byte[pduLen];
                    readTask = ReadExactAsync(stream, pdu, 0, pduLen);
                    if (await Task.WhenAny(readTask, Task.Delay(timeout)) != readTask)
                        throw new TimeoutException("TCP read PDU timed out");
                    await readTask;

                    byte respFunc = pdu[0];
                    if (respFunc == (functionCode | 0x80))
                        throw new InvalidOperationException($"Modbus exception: slave {unitId}, code {pdu[1]}");
                    if (respFunc != functionCode)
                        throw new InvalidOperationException($"Unexpected function code {respFunc}");

                    int byteCount = pdu[1];
                    if (byteCount > pduLen - 2)
                        throw new InvalidOperationException($"Byte count {byteCount} exceeds PDU data");

                    bool hasInvalid = false;
                    for (int i = 0; i < byteCount / 2; i++)
                    {
                        int regAddr = startReg + i;
                        if (regAddr < regs.Length)
                        {
                            short val = (short)((pdu[2 + i * 2] << 8) | pdu[2 + i * 2 + 1]);
                            if (val == unchecked((short)0xAAAA))
                            {
                                hasInvalid = true;
                                break;
                            }
                            regs[regAddr] = val;
                        }
                    }

                    if (!hasInvalid)
                        success = true;
                }
                catch (Exception ex) when (attempt < 4)
                {
                    _logger.LogWarning(ex, "TCP read attempt {Attempt}/5 failed for slave {Slave} reg {Reg}",
                        attempt + 1, block.SlaveId, startReg);
                }
            }

            if (!success)
                _logger.LogWarning("TCP read failed for slave {Slave} reg {Reg} after 5 attempts", block.SlaveId, startReg);
        }

        return regs;
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, int offset, int count)
    {
        int read = 0;
        while (read < count)
        {
            int n = await stream.ReadAsync(buffer, offset + read, count - read);
            if (n == 0) throw new EndOfStreamException("Connection closed by remote host");
            read += n;
        }
    }

    private async Task<short[]> ReadRtuBlockRegistersAsync(BlockConfig block, LineConfig line, TimeSpan timeout)
    {
        var parity = line.Parity switch
        {
            "None" => Parity.None,
            "Odd" => Parity.Odd,
            "Even" => Parity.Even,
            "Mark" => Parity.Mark,
            "Space" => Parity.Space,
            _ => Parity.None
        };
        var stopBits = line.StopBits switch
        {
            "None" => StopBits.None,
            "One" => StopBits.One,
            "Two" => StopBits.Two,
            "OnePointFive" => StopBits.OnePointFive,
            _ => StopBits.One
        };

        using var serialPort = new SerialPort(line.ComPort, line.BaudRate, parity, line.DataBits, stopBits);
        serialPort.ReadTimeout = (int)timeout.TotalMilliseconds;
        serialPort.WriteTimeout = (int)timeout.TotalMilliseconds;
        serialPort.Open();

        var regs = new short[375];
        byte slaveId = (byte)block.SlaveId;
        const byte functionCode = 3;

        foreach (var pendant in block.Pendants.Where(p => p.PointCount > 0))
        {
            ushort regCount = (ushort)pendant.PointCount;
            ushort startReg = pendant.StartRegister;
            bool success = false;

            for (int attempt = 0; attempt < 5 && !success; attempt++)
            {
                try
                {
                    var request = new byte[8];
                    request[0] = slaveId;
                    request[1] = functionCode;
                    request[2] = (byte)(startReg >> 8);
                    request[3] = (byte)(startReg);
                    request[4] = (byte)(regCount >> 8);
                    request[5] = (byte)(regCount);
                    ushort crc = CalculateCrc(request, 0, 6);
                    request[6] = (byte)(crc & 0xFF);
                    request[7] = (byte)((crc >> 8) & 0xFF);

                    await serialPort.BaseStream.WriteAsync(request);
                    await serialPort.BaseStream.FlushAsync();

                    await Task.Delay(20);

                    var header = new byte[3];
                    int headerRead = 0;
                    while (headerRead < 3)
                    {
                        int n = await serialPort.BaseStream.ReadAsync(header, headerRead, 3 - headerRead);
                        if (n == 0) throw new EndOfStreamException("No response from device");
                        headerRead += n;
                    }

                    if (header[0] != slaveId)
                        throw new InvalidOperationException($"Response slave ID mismatch: expected {slaveId}, got {header[0]}");

                    byte respFunc = header[1];
                    if (respFunc == (functionCode | 0x80))
                    {
                        var excData = new byte[2];
                        int excRead = 0;
                        while (excRead < 2)
                        {
                            int n = await serialPort.BaseStream.ReadAsync(excData, excRead, 2 - excRead);
                            if (n == 0) throw new EndOfStreamException("Connection closed");
                            excRead += n;
                        }
                        throw new InvalidOperationException($"Modbus exception: slave {slaveId}, code {excData[0]}");
                    }

                    if (respFunc != functionCode)
                        throw new InvalidOperationException($"Unexpected function code {respFunc}");

                    int dataLen = header[2];
                    var data = new byte[dataLen + 2];
                    int dataRead = 0;
                    while (dataRead < dataLen + 2)
                    {
                        int n = await serialPort.BaseStream.ReadAsync(data, dataRead, dataLen + 2 - dataRead);
                        if (n == 0) throw new EndOfStreamException("Connection closed");
                        dataRead += n;
                    }

                    ushort expectedCrc = CalculateCrc(new[] { header[0], header[1], header[2] }.Concat(data.Take(dataLen)).ToArray(), 0, dataLen + 3);
                    ushort receivedCrc = (ushort)((data[dataLen + 1] << 8) | data[dataLen]);
                    if (expectedCrc != receivedCrc)
                        throw new InvalidOperationException($"CRC mismatch");

                    bool hasInvalid = false;
                    for (int i = 0; i < dataLen / 2; i++)
                    {
                        int regAddr = startReg + i;
                        if (regAddr < regs.Length)
                        {
                            short val = (short)((data[i * 2] << 8) | data[i * 2 + 1]);
                            if (val == unchecked((short)0xAAAA))
                            {
                                hasInvalid = true;
                                break;
                            }
                            regs[regAddr] = val;
                        }
                    }

                    if (!hasInvalid)
                        success = true;
                }
                catch (Exception ex) when (attempt < 4)
                {
                    _logger.LogWarning(ex, "RTU read attempt {Attempt}/5 failed for slave {Slave} reg {Reg}",
                        attempt + 1, block.SlaveId, startReg);
                }
            }

            if (!success)
                _logger.LogWarning("RTU read failed for slave {Slave} reg {Reg} after 5 attempts", block.SlaveId, startReg);

            await Task.Delay(50);
        }

        return regs;
    }

    public void SetScenario(int siloId, string scenario)
    {
        foreach (var b in _config.Lines.SelectMany(l => l.Blocks))
        {
            if (!b.Silos.Any(s => s.Number == siloId)) continue;
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

    public ThermometryConfig GetConfig() => _config;

    public async Task SaveConfigAsync(ThermometryConfig config)
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
        public BlockConfig Block { get; set; } = null!;
    }
}
