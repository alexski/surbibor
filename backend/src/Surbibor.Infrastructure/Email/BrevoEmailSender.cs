using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Surbibor.Infrastructure.Email;

/// <summary>
/// Sends through Brevo's transactional email API over HTTPS.
/// See https://developers.brevo.com/reference/sendtransacemail.
/// </summary>
public class BrevoEmailSender(HttpClient http, IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.BrevoApiKey) || string.IsNullOrWhiteSpace(config.FromAddress))
        {
            throw new InvalidOperationException("Email:BrevoApiKey and Email:FromAddress must be configured to send email via Brevo.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "v3/smtp/email")
        {
            Content = JsonContent.Create(new
            {
                sender = new { name = config.FromName, email = config.FromAddress },
                to = new[] { new { email = message.To } },
                subject = message.Subject,
                htmlContent = message.HtmlBody,
                textContent = message.TextBody,
            }),
        };
        request.Headers.Add("api-key", config.BrevoApiKey);
        request.Headers.Accept.ParseAdd("application/json");

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Brevo rejected the email ({(int)response.StatusCode}): {body}");
        }
    }
}
