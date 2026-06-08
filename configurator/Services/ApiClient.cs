using System.Net.Http.Json;
using System.Text.Json;
using Termometriya.Configurator.Models;

namespace Termometriya.Configurator.Services;

public class ApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public ApiClient()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public string BaseUrl { get; set; } = "http://localhost:5000";

    private string Url(string path) => $"{BaseUrl.TrimEnd('/')}/api/{path}";

    public async Task<ElevatorConfig?> GetElevatorConfigAsync()
    {
        try
        {
            var resp = await _http.GetAsync(Url("config/elevator"));
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<ElevatorConfig>(JsonOpts);
        }
        catch (Exception ex)
        {
            throw new Exception($"Ошибка загрузки конфигурации элеватора: {ex.Message}", ex);
        }
    }

    public async Task SaveElevatorConfigAsync(ElevatorConfig config)
    {
        try
        {
            var resp = await _http.PutAsJsonAsync(Url("config/elevator"), config, JsonOpts);
            resp.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            throw new Exception($"Ошибка сохранения конфигурации элеватора: {ex.Message}", ex);
        }
    }

    public async Task<Bkt12HardwareConfig?> GetHardwareConfigAsync()
    {
        try
        {
            var resp = await _http.GetAsync(Url("config/hardware"));
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<Bkt12HardwareConfig>(JsonOpts);
        }
        catch (Exception ex)
        {
            throw new Exception($"Ошибка загрузки конфигурации БКТ-12: {ex.Message}", ex);
        }
    }

    public async Task SaveHardwareConfigAsync(Bkt12HardwareConfig config)
    {
        try
        {
            var resp = await _http.PutAsJsonAsync(Url("config/hardware"), config, JsonOpts);
            resp.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            throw new Exception($"Ошибка сохранения конфигурации БКТ-12: {ex.Message}", ex);
        }
    }

    public void Dispose() => _http.Dispose();
}
