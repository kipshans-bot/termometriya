using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Termometriya.Server.Data;
using Termometriya.Server.Models;

namespace Termometriya.Server.Services;

public class ReportService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ReportService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> GenerateSummaryExcelAsync(DateTime from, DateTime to)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var silos = await db.Silos.Include(s => s.Line).Include(s => s.Culture).ToListAsync();
        var readings = await db.SensorReadings
            .Where(r => r.Timestamp >= from && r.Timestamp <= to)
            .GroupBy(r => r.SiloId)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Сводка");
        ws.Cell("A1").Value = "Отчёт по температурному режиму";
        ws.Cell("A2").Value = $"Период: {from:dd.MM.yyyy HH:mm} - {to:dd.MM.yyyy HH:mm}";
        ws.Range("A1:F1").Merge().Style.Font.Bold = true;

        ws.Cell("A4").Value = "Линия";
        ws.Cell("B4").Value = "Силос";
        ws.Cell("C4").Value = "Культура";
        ws.Cell("D4").Value = "Средняя T°C";
        ws.Cell("E4").Value = "Макс T°C";
        ws.Cell("F4").Value = "Мин T°C";

        int row = 5;
        foreach (var silo in silos)
        {
            var group = readings.FirstOrDefault(r => r.Key == silo.Id);
            var temps = group?.Where(x => x.IsValid).Select(x => x.Temperature).ToList() ?? [];
            ws.Cell(row, 1).Value = silo.Line?.Name ?? "";
            ws.Cell(row, 2).Value = silo.Number;
            ws.Cell(row, 3).Value = silo.Culture?.Name ?? "";
            ws.Cell(row, 4).Value = temps.Count > 0 ? Math.Round(temps.Average(), 1).ToString("F1") : "-";
            ws.Cell(row, 5).Value = temps.Count > 0 ? Math.Round(temps.Max(), 1).ToString("F1") : "-";
            ws.Cell(row, 6).Value = temps.Count > 0 ? Math.Round(temps.Min(), 1).ToString("F1") : "-";
            row++;
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<byte[]> GenerateSiloReportPdfAsync(int siloId, DateTime from, DateTime to)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var silo = await db.Silos.Include(s => s.Line).Include(s => s.Culture).FirstOrDefaultAsync(s => s.Id == siloId);
        if (silo == null) return [];

        var readings = await db.SensorReadings
            .Where(r => r.SiloId == siloId && r.Timestamp >= from && r.Timestamp <= to)
            .OrderBy(r => r.Timestamp)
            .Take(5000)
            .ToListAsync();

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.Header().Text($"Отчёт по силосу {silo.Number} — {silo.Line?.Name}").Bold().FontSize(16);
                page.Content().Column(col =>
                {
                    col.Item().Text($"Культура: {silo.Culture?.Name ?? "-"}");
                    col.Item().Text($"Уровень загрузки: {silo.FillLevel}%");
                    col.Item().Text($"Период: {from:dd.MM.yyyy} - {to:dd.MM.yyyy}");
                    col.Item().PaddingVertical(10).LineHorizontal(1);

                    if (readings.Count > 0)
                    {
                        var avg = readings.Where(r => r.IsValid).Average(r => r.Temperature);
                        var max = readings.Where(r => r.IsValid).Max(r => r.Temperature);
                        var min = readings.Where(r => r.IsValid).Min(r => r.Temperature);
                        col.Item().Text($"Средняя температура: {avg:F1}°C");
                        col.Item().Text($"Максимальная температура: {max:F1}°C");
                        col.Item().Text($"Минимальная температура: {min:F1}°C");
                        col.Item().PaddingVertical(10).LineHorizontal(1);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn();
                                c.RelativeColumn();
                                c.RelativeColumn();
                                c.RelativeColumn();
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("Подвеска").Bold();
                                header.Cell().Text("Точка").Bold();
                                header.Cell().Text("T°C").Bold();
                                header.Cell().Text("Время").Bold();
                            });

                            foreach (var r in readings.Take(100))
                            {
                                table.Cell().Text($"#{r.ThermopendantId}");
                                table.Cell().Text($"{r.PointIndex}");
                                table.Cell().Text($"{r.Temperature:F1}");
                                table.Cell().Text($"{r.Timestamp:dd.MM HH:mm}");
                            }
                        });
                    }
                });
            });
        });

        return doc.GeneratePdf();
    }

    public async Task<byte[]> GenerateTemperatureLogCsvAsync(DateTime from, DateTime to)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var readings = await db.SensorReadings
            .Include(r => r.Silo).ThenInclude(s => s.Line)
            .Where(r => r.Timestamp >= from && r.Timestamp <= to)
            .OrderBy(r => r.Timestamp)
            .Take(100000)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Время,Линия,Силос,Подвеска,Точка,Температура,Влажность");
        foreach (var r in readings)
        {
            sb.AppendLine($"{r.Timestamp:yyyy-MM-dd HH:mm:ss},{r.Silo.Line?.Name ?? ""},{r.Silo.Number},{r.ThermopendantId},{r.PointIndex},{r.Temperature:F1},{r.Humidity?.ToString("F1") ?? ""}");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public async Task<byte[]> GenerateAlertReportCsvAsync(DateTime from, DateTime to)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var alerts = await db.AlertEvents
            .Include(a => a.Silo).ThenInclude(s => s.Line)
            .Where(a => a.Timestamp >= from && a.Timestamp <= to)
            .OrderByDescending(a => a.Timestamp)
            .Take(5000)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Время,Линия,Силос,Тип,Значение,Порог,Сообщение,Статус");
        foreach (var a in alerts)
        {
            sb.AppendLine($"{a.Timestamp:yyyy-MM-dd HH:mm:ss},{a.Silo.Line?.Name ?? ""},{a.Silo.Number},{a.AlertType},{a.Value:F1},{a.Threshold:F1},\"{a.Message}\",{(a.IsActive ? "Активно" : "Завершено")}");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public async Task<byte[]> GenerateSummaryPdfAsync(DateTime from, DateTime to)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var silos = await db.Silos.Include(s => s.Line).Include(s => s.Culture).ToListAsync();
        var allReadings = await db.SensorReadings
            .Where(r => r.Timestamp >= from && r.Timestamp <= to)
            .GroupBy(r => r.SiloId)
            .ToListAsync();

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(15);
                page.Header().Text("Сводный отчёт по термометрии элеватора").Bold().FontSize(16);
                page.Content().Column(col =>
                {
                    col.Item().Text($"Период: {from:dd.MM.yyyy} - {to:dd.MM.yyyy}");
                    col.Item().PaddingVertical(5).LineHorizontal(1);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn();
                            c.RelativeColumn(2);
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                        });

                        table.Header(h =>
                        {
                            h.Cell().Text("Силос").Bold();
                            h.Cell().Text("Культура").Bold();
                            h.Cell().Text("Средняя").Bold();
                            h.Cell().Text("Макс").Bold();
                            h.Cell().Text("Мин").Bold();
                        });

                        foreach (var silo in silos)
                        {
                            var group = allReadings.FirstOrDefault(r => r.Key == silo.Id);
                            var temps = group?.Where(x => x.IsValid).Select(x => x.Temperature).ToList() ?? [];
                            table.Cell().Text($"{silo.Line?.Name ?? ""} / {silo.Number}");
                            table.Cell().Text(silo.Culture?.Name ?? "-");
                            table.Cell().Text(temps.Count > 0 ? $"{temps.Average():F1}°C" : "-");
                            table.Cell().Text(temps.Count > 0 ? $"{temps.Max():F1}°C" : "-");
                            table.Cell().Text(temps.Count > 0 ? $"{temps.Min():F1}°C" : "-");
                        }
                    });
                });
            });
        });

        return doc.GeneratePdf();
    }
}
