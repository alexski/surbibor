using Microsoft.EntityFrameworkCore;
using Surbibor.Domain.Logic;
using Surbibor.Infrastructure;
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
