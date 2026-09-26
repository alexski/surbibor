using Microsoft.EntityFrameworkCore;
using Surbibor.Domain.Logic;
using Surbibor.Infrastructure;
using Surbibor.Infrastructure.Email;
using Surbibor.Infrastructure.Services;

namespace Surbibor.Api.Tests.Unit;

public static class TestSupport
{
    public static SurbiborDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SurbiborDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new SurbiborDbContext(options);
    }
}

public class CapturingEmailSender : IEmailSender
{
    private readonly List<EmailMessage> _sent = [];

    public IReadOnlyList<EmailMessage> Sent
    {
        get { lock (_sent) return _sent.ToList(); }
    }

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        lock (_sent) _sent.Add(message);
        return Task.CompletedTask;
    }

    /// <summary>Extracts the raw token from the link in the most recent email sent to the address.</summary>
    public string LastTokenFor(string email)
    {
        var message = Sent.Last(m => m.To == email);
        var match = System.Text.RegularExpressions.Regex.Match(message.TextBody, @"[?&]token=([^\s&]+)");
        Assert.True(match.Success, "No token link found in email body.");
        return Uri.UnescapeDataString(match.Groups[1].Value);
    }
}

public class NoOpGameNotifier : IGameNotifier
{
    public Task EventAdded(Guid gameId, Guid eventId, string text) => Task.CompletedTask;

    public Task EventProposed(Guid gameId, Guid eventId, Guid proposedByUserId) => Task.CompletedTask;

    public Task EventConfirmed(Guid gameId, Guid eventId, Guid confirmedByUserId) => Task.CompletedTask;

    public Task EventUnproposed(Guid gameId, Guid eventId) => Task.CompletedTask;

    public Task EventRejected(Guid gameId, Guid eventId) => Task.CompletedTask;

    public Task MarkCountUpdated(Guid gameId, Guid userId, int markedCount) => Task.CompletedTask;

    public Task GameWon(Guid gameId, Guid userId, WinLineType winType, int lineIndex) => Task.CompletedTask;

    public Task SideBetAdded(Guid gameId, Guid sideBetId, string text) => Task.CompletedTask;

    public Task SideBetWagerPlaced(Guid gameId, Guid sideBetId, Guid userId) => Task.CompletedTask;

    public Task SideBetProposed(Guid gameId, Guid sideBetId, Guid proposedByUserId, bool outcome) => Task.CompletedTask;

    public Task SideBetResolved(Guid gameId, Guid sideBetId, bool outcome) => Task.CompletedTask;

    public Task SideBetRejected(Guid gameId, Guid sideBetId) => Task.CompletedTask;
}
