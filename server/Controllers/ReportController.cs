using Microsoft.AspNetCore.Mvc;
using Termometriya.Server.Services;

namespace Termometriya.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportController : ControllerBase
{
    private readonly ReportService _reportService;

    public ReportController(ReportService reportService) => _reportService = reportService;

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string format = "excel")
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-7);
        var toDate = to ?? DateTime.UtcNow;

        if (format == "pdf")
        {
            var pdf = await _reportService.GenerateSummaryPdfAsync(fromDate, toDate);
            return File(pdf, "application/pdf", $"summary_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.pdf");
        }

        var excel = await _reportService.GenerateSummaryExcelAsync(fromDate, toDate);
        return File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"summary_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx");
    }

    [HttpGet("silo/{id}")]
    public async Task<IActionResult> GetSiloReport(int id, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-7);
        var toDate = to ?? DateTime.UtcNow;
        var pdf = await _reportService.GenerateSiloReportPdfAsync(id, fromDate, toDate);
        return File(pdf, "application/pdf", $"silo_{id}_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.pdf");
    }

    [HttpGet("temperature-log")]
    public async Task<IActionResult> GetTemperatureLog([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-7);
        var toDate = to ?? DateTime.UtcNow;
        var csv = await _reportService.GenerateTemperatureLogCsvAsync(fromDate, toDate);
        return File(csv, "text/csv", $"temperature_log_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.csv");
    }

    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlertReport([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-7);
        var toDate = to ?? DateTime.UtcNow;
        var csv = await _reportService.GenerateAlertReportCsvAsync(fromDate, toDate);
        return File(csv, "text/csv", $"alert_report_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.csv");
    }
}
