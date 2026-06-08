// ============================================================
// Modbus TCP Server — симулятор блоков БКТ-12
// ============================================================
// Использование:
//   dotnet run [--ports 1501,1502,1503,1504,1505,1506]
//
// По умолчанию слушает порты 1501-1506 (по числу блоков БКТ-12).
// Реализует Modbus TCP slave:
//   - Функция 03 (Read Holding Registers)
//   - Big-endian, целое со знаком / 16 = °C
//   - CRC-16 (младший байт первым)
//   - Регистры 15-374 (360 регистров = 12 подвесок x 30)
//
// Генерирует реалистичные температуры + случайный шум.
// ============================================================

using System.Net;
using System.Net.Sockets;

var ports = ParsePorts(args);
Console.WriteLine($"Modbus TCP Server for BKT-12");
Console.WriteLine($"Порты: {string.Join(", ", ports)}");
Console.WriteLine($"Регистры: 15-374 (12 подвесок x 30 регистров)");
Console.WriteLine($"Формат: целое со знаком / 16 = °C, big-endian, CRC-16");
Console.WriteLine("Нажмите Ctrl+C для выхода.\n");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var tasks = ports.Select(port => RunServerAsync(port, cts.Token));
await Task.WhenAll(tasks);

static List<int> ParsePorts(string[] args)
{
    for (int i = 0; i < args.Length - 1; i++)
        if (args[i] == "--ports")
            return args[i + 1].Split(',').Select(int.Parse).ToList();
    return Enumerable.Range(1501, 6).ToList();
}

static async Task RunServerAsync(int port, CancellationToken ct)
{
    var listener = new TcpListener(IPAddress.Any, port);
    listener.Start();
    Console.WriteLine($"[Порт {port}] Ожидание подключений...");

    try
    {
        while (!ct.IsCancellationRequested)
        {
            var client = await listener.AcceptTcpClientAsync(ct);
            client.NoDelay = true;
            _ = HandleClientAsync(client, port, ct);
        }
    }
    catch (OperationCanceledException) { }
    finally { listener.Stop(); }
}

static async Task HandleClientAsync(TcpClient client, int port, CancellationToken ct)
{
    var ep = client.Client.RemoteEndPoint;
    Console.WriteLine($"[Порт {port}] Подключился {ep}");
    var buf = new byte[260];
    var regs = GenerateRegisters();

    try
    {
        var stream = client.GetStream();
        while (!ct.IsCancellationRequested && client.Connected)
        {
            var read = await stream.ReadAsync(buf, ct);
            if (read < 8) break;

            int len = (buf[4] << 8) | buf[5];
            int slaveId = buf[6];
            int func = buf[7];

            if (func == 3 || func == 4)
            {
                ushort start = (ushort)((buf[8] << 8) | buf[9]);
                ushort count = (ushort)((buf[10] << 8) | buf[11]);
                count = Math.Min((ushort)125, count);

                var data = new byte[count * 2];
                for (int i = 0; i < count; i++)
                {
                    int idx = start + i;
                    if (idx >= 0 && idx < regs.Length)
                    {
                        data[i * 2] = (byte)((regs[idx] >> 8) & 0xFF);
                        data[i * 2 + 1] = (byte)(regs[idx] & 0xFF);
                    }
                }

                var resp = new byte[9 + data.Length];
                resp[0] = 0; resp[1] = 0;           // Transaction ID
                resp[2] = 0; resp[3] = 0;           // Protocol ID
                resp[4] = (byte)((3 + data.Length) >> 8);
                resp[5] = (byte)(3 + data.Length);   // Length
                resp[6] = (byte)slaveId;
                resp[7] = (byte)func;
                resp[8] = (byte)data.Length;
                Array.Copy(data, 0, resp, 9, data.Length);

                Console.WriteLine($"[Порт {port}] Slave={slaveId} F={func} reg={start} cnt={count}");
                await stream.WriteAsync(resp, ct);
            }
        }
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        Console.WriteLine($"[Порт {port}] Ошибка: {ex.Message}");
    }
    finally
    {
        client.Close();
        Console.WriteLine($"[Порт {port}] Отключился {ep}");
    }
}

static short[] GenerateRegisters()
{
    var regs = new short[375];
    var rand = new Random();
    for (int pendant = 0; pendant < 12; pendant++)
    {
        double baseTemp = 18 + rand.NextDouble() * 5;
        int startReg = 15 + pendant * 30;
        int pointCount = pendant % 2 == 0 ? 18 : 16;
        for (int p = 0; p < pointCount; p++)
        {
            double temp = baseTemp + (p * 0.3) + (rand.NextDouble() - 0.5) * 2;
            regs[startReg + p] = (short)(temp * 16);
        }
    }
    return regs;
}
