using System.Diagnostics;

namespace Termometriya.Configurator.Services;

public class ServerProcessManager : IDisposable
{
    private Process? _process;
    private readonly ListBox _outputBox;

    public ServerProcessManager(ListBox outputBox)
    {
        _outputBox = outputBox;
    }

    public bool IsRunning => _process is { HasExited: false };

    public string ExePath { get; set; } = "";
    public string Args { get; set; } = "--urls http://0.0.0.0:5000";

    public event Action? StatusChanged;

    public void Start()
    {
        if (IsRunning) return;
        if (string.IsNullOrEmpty(ExePath) || !File.Exists(ExePath))
        {
            AppendOutput("Файл не найден: " + ExePath);
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = ExePath,
            Arguments = Args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(ExePath)
        };

        _process = new Process { StartInfo = psi };
        _process.OutputDataReceived += (_, e) => AppendOutput(e.Data);
        _process.ErrorDataReceived += (_, e) => AppendOutput(e.Data);
        _process.Exited += (_, _) =>
        {
            AppendOutput("Процесс завершён");
            StatusChanged?.Invoke();
        };

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        AppendOutput("Сервер запущен");
        StatusChanged?.Invoke();
    }

    public void Stop()
    {
        if (!IsRunning) return;
        try
        {
            _process!.Kill(entireProcessTree: true);
            _process.WaitForExit(5000);
            AppendOutput("Сервер остановлен");
        }
        catch (Exception ex)
        {
            AppendOutput($"Ошибка остановки: {ex.Message}");
        }
        StatusChanged?.Invoke();
    }

    public void Restart()
    {
        Stop();
        Thread.Sleep(1000);
        Start();
    }

    private void AppendOutput(string? text)
    {
        if (text == null) return;
        _outputBox.Dispatcher.Invoke(() =>
        {
            _outputBox.Items.Add($"{DateTime.Now:HH:mm:ss} {text}");
            if (_outputBox.Items.Count > 500)
                _outputBox.Items.RemoveAt(0);
            _outputBox.ScrollIntoView(_outputBox.Items[^1]);
        });
    }

    public void Dispose()
    {
        try { Stop(); } catch { }
        _process?.Dispose();
    }
}
