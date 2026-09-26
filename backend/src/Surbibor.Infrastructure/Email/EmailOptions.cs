namespace Surbibor.Infrastructure.Email;

public class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>
    /// "Smtp" (default; local Mailpit or any SMTP server) or "Brevo" (HTTPS API, for hosts
    /// like Railway that block outbound SMTP).
    /// </summary>
    public string Provider { get; set; } = "Smtp";

    public string? BrevoApiKey { get; set; }

    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }

    /// <summary>
    /// "StartTls" (port 587), "SslOnConnect" (port 465), or "None" (local catchers like Mailpit).
    /// </summary>
    public string Security { get; set; } = "StartTls";

    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "Surbibor";
}
