using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Surbibor.Infrastructure.Email;

namespace Surbibor.Api.Tests.Unit;

public class BrevoEmailSenderTests
{
    private static readonly EmailOptions Config = new()
    {
        Provider = "Brevo",
        BrevoApiKey = "test-key",
        FromAddress = "me@gmail.com",
        FromName = "Surbibor",
    };

    private static readonly EmailMessage Message = new("alice@example.com", "Hello", "text body", "<p>html body</p>");

    [Fact]
    public async Task SendAsync_PostsExpectedRequest()
    {
        var handler = new RecordingHandler(HttpStatusCode.Created, """{"messageId":"<abc@brevo>"}""");
        var sender = new BrevoEmailSender(new HttpClient(handler) { BaseAddress = new Uri("https://api.brevo.com/") }, Options.Create(Config));

        await sender.SendAsync(Message);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://api.brevo.com/v3/smtp/email", handler.Request.RequestUri!.ToString());
        Assert.Equal("test-key", handler.Request.Headers.GetValues("api-key").Single());

        using var json = JsonDocument.Parse(handler.Body!);
        var root = json.RootElement;
        Assert.Equal("me@gmail.com", root.GetProperty("sender").GetProperty("email").GetString());
        Assert.Equal("alice@example.com", root.GetProperty("to")[0].GetProperty("email").GetString());
        Assert.Equal("Hello", root.GetProperty("subject").GetString());
        Assert.Equal("<p>html body</p>", root.GetProperty("htmlContent").GetString());
        Assert.Equal("text body", root.GetProperty("textContent").GetString());
    }

    [Fact]
    public async Task SendAsync_ThrowsWithBrevoErrorOnFailure()
    {
        var handler = new RecordingHandler(HttpStatusCode.BadRequest, """{"code":"invalid_parameter","message":"sender is not valid"}""");
        var sender = new BrevoEmailSender(new HttpClient(handler) { BaseAddress = new Uri("https://api.brevo.com/") }, Options.Create(Config));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => sender.SendAsync(Message));
        Assert.Contains("sender is not valid", ex.Message);
    }

    private class RecordingHandler(HttpStatusCode status, string responseBody) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            Body = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(responseBody) };
        }
    }
}
