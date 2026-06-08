namespace Termometriya.Server.Models;

public class AlertConfig
{
    public int Id { get; set; }
    public bool SoundEnabled { get; set; } = true;
    public bool EmailEnabled { get; set; }
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 25;
    public string SmtpUser { get; set; } = string.Empty;
    public string SmtpPass { get; set; } = string.Empty;
    public string EmailRecipients { get; set; } = string.Empty;
}
