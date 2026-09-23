using Microsoft.EntityFrameworkCore;
using Surbibor.Domain.Entities;
using Surbibor.Domain.Exceptions;

namespace Surbibor.Infrastructure.Services;

public class EventService(SurbiborDbContext db, IGameNotifier notifier)
{
    public async Task<List<Event>> ListEventsAsync(Guid gameId, Guid userId, CancellationToken ct = default)
    {
        await EnsureMemberAsync(gameId, userId, ct);

        return await db.Events
            .Where(e => e.GameId == gameId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);
    }
    
    public async Task<List<Event>> ListOpenEventsAsync(Guid gameId, Guid userId, CancellationToken ct = default)
    {
        await EnsureMemberAsync(gameId, userId, ct);

        return await db.Events
            .Where(e => e.GameId == gameId)
            .Where(q => q.Status == EventStatus.Open)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);
    }
    
    public async Task<List<Event>> ListProposedEventsAsync(Guid gameId, Guid userId, CancellationToken ct = default)
    {
        await EnsureMemberAsync(gameId, userId, ct);

        return await db.Events
            .Where(e => e.GameId == gameId)
            .Where(q => q.Status == EventStatus.PendingConfirmation)
            .OrderByDescending(e => e.ProposedAt)
            .ToListAsync(ct);
    }
    
    public async Task<List<Event>> ListConfirmedEventsAsync(Guid gameId, Guid userId, CancellationToken ct = default)
    {
        await EnsureMemberAsync(gameId, userId, ct);

        return await db.Events
            .Where(e => e.GameId == gameId)
            .Where(q => q.Status == EventStatus.Confirmed)
            .OrderByDescending(e => e.ConfirmedAt)
            .ToListAsync(ct);
    }


    public async Task<Event> AddEventAsync(Guid gameId, Guid userId, string text, CancellationToken ct = default)
    {
        var game = await EnsureMemberAsync(gameId, userId, ct);

        text = text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ValidationException("Event text is required.");
        }

        if (game.Status != GameStatus.Active)
        {
            throw new ConflictException("This game has already been completed.");
        }

        var evt = new Event
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            Text = text,
            CreatedByUserId = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = EventStatus.Open,
        };

        db.Events.Add(evt);
        await db.SaveChangesAsync(ct);

        await notifier.EventAdded(gameId, evt.Id, evt.Text);

        return evt;
    }

    public async Task<Event> ProposeAsync(Guid gameId, Guid eventId, Guid userId, CancellationToken ct = default)
    {
        var game = await EnsureMemberAsync(gameId, userId, ct);
        EnsureGameActive(game);

        var evt = await GetEventAsync(gameId, eventId, ct);

        if (evt.Status != EventStatus.Open)
        {
            throw new ConflictException("This event has already been proposed or confirmed.");
        }

        evt.Status = EventStatus.PendingConfirmation;
        evt.ProposedByUserId = userId;
        evt.ProposedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        await notifier.EventProposed(gameId, evt.Id, userId);

        return evt;
    }

    public async Task<Event> ConfirmAsync(Guid gameId, Guid eventId, Guid userId, CancellationToken ct = default)
    {
        var game = await EnsureMemberAsync(gameId, userId, ct);
        EnsureGameActive(game);

        var evt = await GetEventAsync(gameId, eventId, ct);

        if (evt.Status != EventStatus.PendingConfirmation)
        {
            throw new ConflictException("This event is not awaiting confirmation.");
        }

        if (evt.ProposedByUserId == userId)
        {
            throw new ForbiddenException("A different player must confirm this event.");
        }

        evt.Status = EventStatus.Confirmed;
        evt.ConfirmedByUserId = userId;
        evt.ConfirmedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        await notifier.EventConfirmed(gameId, evt.Id, userId);

        return evt;
    }

    public async Task<Event> UnproposeAsync(Guid gameId, Guid eventId, Guid userId, CancellationToken ct = default)
    {
        var game = await EnsureMemberAsync(gameId, userId, ct);
        EnsureGameActive(game);

        var evt = await GetEventAsync(gameId, eventId, ct);

        if (evt.Status != EventStatus.PendingConfirmation)
        {
            throw new ConflictException("This event is not awaiting confirmation.");
        }

        if (evt.ProposedByUserId != userId)
        {
            throw new ForbiddenException("Only the player who reported this event can undo it.");
        }

        evt.Status = EventStatus.Open;
        evt.ProposedByUserId = null;
        evt.ProposedAt = null;

        await db.SaveChangesAsync(ct);

        await notifier.EventUnproposed(gameId, evt.Id);

        return evt;
    }

    public async Task<Event> RejectAsync(Guid gameId, Guid eventId, Guid userId, CancellationToken ct = default)
    {
        var game = await EnsureMemberAsync(gameId, userId, ct);
        EnsureGameActive(game);

        var evt = await GetEventAsync(gameId, eventId, ct);

        if (evt.Status != EventStatus.PendingConfirmation)
        {
            throw new ConflictException("This event is not awaiting confirmation.");
        }

        if (evt.ProposedByUserId == userId)
        {
            throw new ForbiddenException("A different player must reject this event.");
        }

        evt.Status = EventStatus.Open;
        evt.ProposedByUserId = null;
        evt.ProposedAt = null;

        await db.SaveChangesAsync(ct);

        await notifier.EventRejected(gameId, evt.Id);

        return evt;
    }

    private async Task<Event> GetEventAsync(Guid gameId, Guid eventId, CancellationToken ct)
    {
        return await db.Events.FirstOrDefaultAsync(e => e.Id == eventId && e.GameId == gameId, ct)
            ?? throw new NotFoundException("Event not found.");
    }

    private async Task<Game> EnsureMemberAsync(Guid gameId, Guid userId, CancellationToken ct)
    {
        var game = await db.Games
            .Include(g => g.Memberships)
            .FirstOrDefaultAsync(g => g.Id == gameId, ct)
            ?? throw new NotFoundException("Game not found.");

        GameService.EnsureMember(game, userId);

        return game;
    }

    private static void EnsureGameActive(Game game)
    {
        if (game.Status != GameStatus.Active)
        {
            throw new ConflictException("This game has already been completed.");
        }
    }
}
