using Microsoft.EntityFrameworkCore;
using Surbibor.Domain.Entities;
using Surbibor.Domain.Exceptions;

namespace Surbibor.Infrastructure.Services;

public class SideBetService(SurbiborDbContext db, IGameNotifier notifier)
{
    private const int MinStake = 5;

    public async Task<List<SideBet>> ListSideBetsAsync(Guid gameId, Guid userId, CancellationToken ct = default)
    {
        await EnsureMemberAsync(gameId, userId, ct);

        return await db.SideBets
            .Where(sb => sb.GameId == gameId)
            .Include(sb => sb.Wagers)
            .OrderByDescending(sb => sb.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<SideBet> CreateSideBetAsync(
        Guid gameId,
        Guid userId,
        string text,
        DateTimeOffset placingClosesAt,
        DateTimeOffset expectedResolutionAt,
        CancellationToken ct = default)
    {
        var game = await EnsureMemberAsync(gameId, userId, ct);
        EnsureGameActive(game);

        text = text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ValidationException("Side bet text is required.");
        }

        var now = DateTimeOffset.UtcNow;
        if (placingClosesAt <= now)
        {
            throw new ValidationException("The betting deadline must be in the future.");
        }

        if (expectedResolutionAt < placingClosesAt)
        {
            throw new ValidationException("The expected resolution time must be at or after the betting deadline.");
        }

        var sideBet = new SideBet
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            Text = text,
            CreatedByUserId = userId,
            CreatedAt = now,
            PlacingClosesAt = placingClosesAt,
            ExpectedResolutionAt = expectedResolutionAt,
            Status = SideBetStatus.Open,
        };

        db.SideBets.Add(sideBet);
        await db.SaveChangesAsync(ct);

        await notifier.SideBetAdded(gameId, sideBet.Id, sideBet.Text);

        return sideBet;
    }

    public async Task<SideBet> PlaceWagerAsync(
        Guid gameId, Guid sideBetId, Guid userId, bool prediction, int stake, CancellationToken ct = default)
    {
        var game = await EnsureMemberAsync(gameId, userId, ct);
        EnsureGameActive(game);

        var sideBet = await GetSideBetAsync(gameId, sideBetId, ct);

        if (sideBet.Status != SideBetStatus.Open)
        {
            throw new ConflictException("This side bet is no longer accepting wagers.");
        }

        if (DateTimeOffset.UtcNow >= sideBet.PlacingClosesAt)
        {
            throw new ConflictException("Betting has closed for this side bet.");
        }

        if (sideBet.Wagers.Any(w => w.UserId == userId))
        {
            throw new ConflictException("You have already placed a wager on this side bet.");
        }

        var membership = game.Memberships.First(m => m.UserId == userId);

        if (membership.Points <= 0)
        {
            throw new ValidationException("You have no points left to wager.");
        }

        var minStake = Math.Min(MinStake, membership.Points);
        if (stake < minStake || stake > membership.Points)
        {
            throw new ValidationException($"Stake must be between {minStake} and {membership.Points} points.");
        }

        membership.Points -= stake;

        var wager = new SideBetWager
        {
            Id = Guid.NewGuid(),
            SideBetId = sideBetId,
            UserId = userId,
            Prediction = prediction,
            Stake = stake,
            PlacedAt = DateTimeOffset.UtcNow,
        };

        db.SideBetWagers.Add(wager);
        await db.SaveChangesAsync(ct);

        await notifier.SideBetWagerPlaced(gameId, sideBetId, userId);

        return sideBet;
    }

    public async Task<SideBet> ProposeOutcomeAsync(
        Guid gameId, Guid sideBetId, Guid userId, bool outcome, CancellationToken ct = default)
    {
        var game = await EnsureMemberAsync(gameId, userId, ct);
        EnsureGameActive(game);

        var sideBet = await GetSideBetAsync(gameId, sideBetId, ct);

        if (sideBet.Status != SideBetStatus.Open)
        {
            throw new ConflictException("This side bet has already been proposed or resolved.");
        }

        if (DateTimeOffset.UtcNow < sideBet.PlacingClosesAt)
        {
            throw new ConflictException("Betting is still open for this side bet.");
        }

        sideBet.Status = SideBetStatus.PendingConfirmation;
        sideBet.ProposedOutcome = outcome;
        sideBet.ProposedByUserId = userId;
        sideBet.ProposedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        await notifier.SideBetProposed(gameId, sideBet.Id, userId, outcome);

        return sideBet;
    }

    public async Task<SideBet> ConfirmOutcomeAsync(Guid gameId, Guid sideBetId, Guid userId, CancellationToken ct = default)
    {
        var game = await EnsureMemberAsync(gameId, userId, ct);
        EnsureGameActive(game);

        var sideBet = await GetSideBetAsync(gameId, sideBetId, ct);

        if (sideBet.Status != SideBetStatus.PendingConfirmation)
        {
            throw new ConflictException("This side bet is not awaiting confirmation.");
        }

        if (sideBet.ProposedByUserId == userId)
        {
            throw new ForbiddenException("A different player must confirm this side bet.");
        }

        var outcome = sideBet.ProposedOutcome!.Value;

        sideBet.Status = SideBetStatus.Resolved;
        sideBet.Outcome = outcome;
        sideBet.ResolvedByUserId = userId;
        sideBet.ResolvedAt = DateTimeOffset.UtcNow;

        foreach (var wager in sideBet.Wagers.Where(w => w.Prediction == outcome))
        {
            var membership = game.Memberships.First(m => m.UserId == wager.UserId);
            membership.Points += wager.Stake * 2;
        }

        await db.SaveChangesAsync(ct);

        await notifier.SideBetResolved(gameId, sideBet.Id, outcome);

        return sideBet;
    }

    public async Task<SideBet> RejectOutcomeAsync(Guid gameId, Guid sideBetId, Guid userId, CancellationToken ct = default)
    {
        var game = await EnsureMemberAsync(gameId, userId, ct);
        EnsureGameActive(game);

        var sideBet = await GetSideBetAsync(gameId, sideBetId, ct);

        if (sideBet.Status != SideBetStatus.PendingConfirmation)
        {
            throw new ConflictException("This side bet is not awaiting confirmation.");
        }

        if (sideBet.ProposedByUserId == userId)
        {
            throw new ForbiddenException("A different player must reject this side bet.");
        }

        sideBet.Status = SideBetStatus.Open;
        sideBet.ProposedOutcome = null;
        sideBet.ProposedByUserId = null;
        sideBet.ProposedAt = null;

        await db.SaveChangesAsync(ct);

        await notifier.SideBetRejected(gameId, sideBet.Id);

        return sideBet;
    }

    private async Task<SideBet> GetSideBetAsync(Guid gameId, Guid sideBetId, CancellationToken ct)
    {
        return await db.SideBets
            .Include(sb => sb.Wagers)
            .FirstOrDefaultAsync(sb => sb.Id == sideBetId && sb.GameId == gameId, ct)
            ?? throw new NotFoundException("Side bet not found.");
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
