using Surbibor.Domain.Logic;

namespace Surbibor.Infrastructure.Services;

/// <summary>
/// Abstraction over realtime push notifications so the service layer stays free of any
/// SignalR/transport concerns. Implemented in the API layer using a SignalR hub context.
/// </summary>
public interface IGameNotifier
{
    Task EventAdded(Guid gameId, Guid eventId, string text);

    Task EventProposed(Guid gameId, Guid eventId, Guid proposedByUserId);

    Task EventConfirmed(Guid gameId, Guid eventId, Guid confirmedByUserId);

    Task EventUnproposed(Guid gameId, Guid eventId);

    Task EventRejected(Guid gameId, Guid eventId);

    Task MarkCountUpdated(Guid gameId, Guid userId, int markedCount);

    Task GameWon(Guid gameId, Guid userId, WinLineType winType, int lineIndex);

    Task SideBetAdded(Guid gameId, Guid sideBetId, string text);

    Task SideBetWagerPlaced(Guid gameId, Guid sideBetId, Guid userId);

    Task SideBetProposed(Guid gameId, Guid sideBetId, Guid proposedByUserId, bool outcome);

    Task SideBetResolved(Guid gameId, Guid sideBetId, bool outcome);

    Task SideBetRejected(Guid gameId, Guid sideBetId);
}
