using Microsoft.EntityFrameworkCore;
using Surbibor.Domain.Entities;
using Surbibor.Domain.Exceptions;
using Surbibor.Domain.Logic;

namespace Surbibor.Infrastructure.Services;

public class GameService(SurbiborDbContext db)
{
    public async Task<Game> CreateGameAsync(Guid creatorUserId, string name, string description, CancellationToken ct = default)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException("Game name is required.");
        }

        var random = Random.Shared;
        string inviteCode;
        do
        {
            inviteCode = InviteCodeGenerator.Generate(random);
        } while (await db.Games.AnyAsync(g => g.InviteCode == inviteCode, ct));

        var game = new Game
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description?.Trim() ?? string.Empty,
            InviteCode = inviteCode,
            CreatedByUserId = creatorUserId,
            Status = GameStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        game.Memberships.Add(new GameMembership
        {
            GameId = game.Id,
            UserId = creatorUserId,
            JoinedAt = DateTimeOffset.UtcNow,
        });

        db.Games.Add(game);
        await db.SaveChangesAsync(ct);

        return game;
    }

    public async Task<List<Game>> ListGamesForUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await db.Games
            .Where(g => g.Memberships.Any(m => m.UserId == userId))
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<Game> GetGameForUserAsync(Guid gameId, Guid userId, CancellationToken ct = default)
    {
        var game = await db.Games
            .Include(g => g.Memberships).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(g => g.Id == gameId, ct)
            ?? throw new NotFoundException("Game not found.");

        EnsureMember(game, userId);

        return game;
    }

    public async Task<Game> JoinGameAsync(Guid userId, string inviteCode, CancellationToken ct = default)
    {
        inviteCode = inviteCode.Trim().ToUpperInvariant();

        var game = await db.Games
            .Include(g => g.Memberships)
            .FirstOrDefaultAsync(g => g.InviteCode == inviteCode, ct)
            ?? throw new NotFoundException("No game found with that invite code.");

        if (game.Memberships.All(m => m.UserId != userId))
        {
            game.Memberships.Add(new GameMembership
            {
                GameId = game.Id,
                UserId = userId,
                JoinedAt = DateTimeOffset.UtcNow,
            });

            await db.SaveChangesAsync(ct);
        }

        return game;
    }

    public static void EnsureMember(Game game, Guid userId)
    {
        if (game.Memberships.All(m => m.UserId != userId))
        {
            throw new ForbiddenException("You are not a member of this game.");
        }
    }
}
