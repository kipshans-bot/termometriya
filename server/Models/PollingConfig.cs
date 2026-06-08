namespace Termometriya.Server.Models;

public enum PollingMode
{
    Normal = 0,
    Elevated = 1
}

public class PollingConfig
{
    public int Id { get; set; }
    public int NormalIntervalSec { get; set; } = 10;
    public int ElevatedIntervalSec { get; set; } = 5;
    public PollingMode CurrentMode { get; set; } = PollingMode.Normal;
    public int GetCurrentInterval() =>
        CurrentMode == PollingMode.Elevated ? ElevatedIntervalSec : NormalIntervalSec;
}
