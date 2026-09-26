namespace Surbibor.Infrastructure.Email;

public class EmailOptions
{
    public const string SectionName = "Email";

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
