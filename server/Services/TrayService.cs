using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Termometriya.Server.Services;

public class TrayService : IHostedService
{
    private NotifyIcon? _trayIcon;
    private Form? _hiddenForm;
    private readonly string _url;

    public TrayService(IConfiguration config)
    {
        _url = config["Urls"] ?? "http://localhost:5000";
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var thread = new Thread(() =>
        {
            _hiddenForm = new Form { ShowInTaskbar = false, WindowState = FormWindowState.Minimized };

            using var bmp = new Bitmap(16, 16);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.Transparent);
            g.FillRectangle(Brushes.DodgerBlue, 2, 2, 12, 12);
            g.DrawRectangle(Pens.White, 2, 2, 12, 12);
            var hIcon = bmp.GetHicon();

            _trayIcon = new NotifyIcon
            {
                Icon = Icon.FromHandle(hIcon),
                Text = "Термометрия элеватора\nСервер запущен",
                Visible = true,
                ContextMenuStrip = new ContextMenuStrip()
            };

            _trayIcon.ContextMenuStrip.Items.Add("Открыть в браузере", null, (_, _) =>
            {
                try { Process.Start(new ProcessStartInfo(_url) { UseShellExecute = true }); } catch { }
            });
            _trayIcon.ContextMenuStrip.Items.Add("Перезапустить опрос", null, async (_, _) =>
            {
                try { using var http = new HttpClient(); await http.PostAsync($"{_url}/api/emulator/reset", null); } catch { }
            });
            _trayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _trayIcon.ContextMenuStrip.Items.Add("Выход", null, (_, _) =>
            {
                _trayIcon.Visible = false;
                Application.ExitThread();
                Environment.Exit(0);
            });

            _trayIcon.DoubleClick += (_, _) =>
            {
                try { Process.Start(new ProcessStartInfo(_url) { UseShellExecute = true }); } catch { }
            };

            Application.Run(_hiddenForm);
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        _hiddenForm?.Invoke(() => Application.ExitThread());
        return Task.CompletedTask;
    }
}
