using Microsoft.AspNetCore.SignalR;
using Surbibor.Api.Hubs;
using Surbibor.Domain.Logic;
using Surbibor.Infrastructure.Services;

namespace Surbibor.Api.Realtime;

public class SignalRGameNotifier(IHubContext<GameHub> hub) : IGameNotifier
{
    public Task EventAdded(Guid gameId, Guid eventId, string text) =>
        Group(gameId).SendAsync("EventAdded", new { eventId, text });

    public Task EventProposed(Guid gameId, Guid eventId, Guid proposedByUserId) =>
        Group(gameId).SendAsync("EventProposed", new { eventId, proposedByUserId });

    public Task EventConfirmed(Guid gameId, Guid eventId, Guid confirmedByUserId) =>
        Group(gameId).SendAsync("EventConfirmed", new { eventId, confirmedByUserId });

    public Task EventUnproposed(Guid gameId, Guid eventId) =>
        Group(gameId).SendAsync("EventUnproposed", new { eventId });

    public Task EventRejected(Guid gameId, Guid eventId) =>
        Group(gameId).SendAsync("EventRejected", new { eventId });

    public Task MarkCountUpdated(Guid gameId, Guid userId, int markedCount) =>
        Group(gameId).SendAsync("MarkCountUpdated", new { userId, markedCount });

    public Task GameWon(Guid gameId, Guid userId, WinLineType winType, int lineIndex) =>
        Group(gameId).SendAsync("GameWon", new { userId, winType = winType.ToString(), lineIndex });

    public Task SideBetAdded(Guid gameId, Guid sideBetId, string text) =>
        Group(gameId).SendAsync("SideBetAdded", new { sideBetId, text });

    public Task SideBetWagerPlaced(Guid gameId, Guid sideBetId, Guid userId) =>
        Group(gameId).SendAsync("SideBetWagerPlaced", new { sideBetId, userId });

    public Task SideBetProposed(Guid gameId, Guid sideBetId, Guid proposedByUserId, bool outcome) =>
        Group(gameId).SendAsync("SideBetProposed", new { sideBetId, proposedByUserId, outcome });

    public Task SideBetResolved(Guid gameId, Guid sideBetId, bool outcome) =>
        Group(gameId).SendAsync("SideBetResolved", new { sideBetId, outcome });

    public Task SideBetRejected(Guid gameId, Guid sideBetId) =>
        Group(gameId).SendAsync("SideBetRejected", new { sideBetId });

    private IClientProxy Group(Guid gameId) => hub.Clients.Group(GameHub.GroupName(gameId));
}
