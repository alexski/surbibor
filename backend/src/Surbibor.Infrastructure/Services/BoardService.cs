using Microsoft.EntityFrameworkCore;
using Surbibor.Domain.Entities;
using Surbibor.Domain.Exceptions;
using Surbibor.Domain.Logic;

namespace Surbibor.Infrastructure.Services;

public record BoardPlacement(int Position, Guid EventId);

/// <summary>
/// Another player's board, reduced to which positions are marked. Deliberately carries no
/// event ids or text, so players can't see what's on each other's boards.
/// </summary>
public record PlayerBoardProgress(Guid UserId, string Username, bool HasBoard, IReadOnlyList<int> MarkedPositions);

public class BoardService(SurbiborDbContext db, IGameNotifier notifier)
{
    public async Task<Board?> GetBoardAsync(Guid gameId, Guid userId, CancellationToken ct = default)
    {
        await EnsureMemberAsync(gameId, userId, ct);

        return await db.Boards
            .Include(b => b.Squares).ThenInclude(s => s.Event)
            .FirstOrDefaultAsync(b => b.GameId == gameId && b.UserId == userId, ct);
    }

    public async Task<IReadOnlyList<PlayerBoardProgress>> GetOtherPlayersProgressAsync(Guid gameId, Guid userId, CancellationToken ct = default)
    {
        await EnsureMemberAsync(gameId, userId, ct);

        var members = await db.GameMemberships
            .Where(m => m.GameId == gameId && m.UserId != userId)
            .OrderBy(m => m.JoinedAt)
            .Select(m => new { m.UserId, m.User.Username })
            .ToListAsync(ct);

        var markedByUser = await db.Boards
            .Where(b => b.GameId == gameId && b.UserId != userId)
            .Select(b => new
            {
                b.UserId,
                Marked = b.Squares.Where(s => s.IsMarked).Select(s => s.Position).OrderBy(p => p).ToList(),
            })
            .ToDictionaryAsync(b => b.UserId, b => b.Marked, ct);

        return members
            .Select(m => markedByUser.TryGetValue(m.UserId, out var marked)
                ? new PlayerBoardProgress(m.UserId, m.Username, true, marked)
                : new PlayerBoardProgress(m.UserId, m.Username, false, []))
            .ToList();
    }

    public async Task<Board> CreateRandomBoardAsync(Guid gameId, Guid userId, IReadOnlyList<Guid> eventIds, CancellationToken ct = default)
    {
        var eventTexts = await ValidateAndLoadEventsAsync(gameId, userId, eventIds, ct);

        var layout = BoardLayout.Randomize(eventIds, Random.Shared);

        return await PersistBoardAsync(gameId, userId, layout, ct);
    }

    public async Task<Board> CreateManualBoardAsync(Guid gameId, Guid userId, IReadOnlyList<BoardPlacement> placements, CancellationToken ct = default)
    {
        var eventIds = placements.Select(p => p.EventId).ToList();
        await ValidateAndLoadEventsAsync(gameId, userId, eventIds, ct);

        var layout = placements.ToDictionary(p => p.Position, p => p.EventId);

        if (!BoardLayout.IsValidManualLayout(layout, eventIds))
        {
            throw new ValidationException("Manual layout must place each of the 24 chosen events exactly once, one per non-free square.");
        }

        return await PersistBoardAsync(gameId, userId, layout, ct);
    }

    public async Task<Board> MarkSquareAsync(Guid gameId, Guid userId, int position, CancellationToken ct = default)
    {
        var game = await EnsureMemberAsync(gameId, userId, ct);

        if (game.Status != GameStatus.Active)
        {
            throw new ConflictException("This game has already been completed.");
        }

        var board = await db.Boards
            .Include(b => b.Squares).ThenInclude(s => s.Event)
            .FirstOrDefaultAsync(b => b.GameId == gameId && b.UserId == userId, ct)
            ?? throw new NotFoundException("You have not created a board for this game yet.");

        var square = board.Squares.FirstOrDefault(s => s.Position == position)
            ?? throw new NotFoundException("Invalid board position.");

        if (square.IsFree)
        {
            throw new ConflictException("The free space is already marked.");
        }

        if (square.IsMarked)
        {
            throw new ConflictException("This square is already marked.");
        }

        if (square.Event is null || square.Event.Status != EventStatus.Confirmed)
        {
            throw new ConflictException("This event has not been confirmed as having happened yet.");
        }

        square.IsMarked = true;
        square.MarkedAt = DateTimeOffset.UtcNow;

        var membership = game.Memberships.First(m => m.UserId == userId);
        membership.Points += 5;

        await db.SaveChangesAsync(ct);

        var markedCount = board.Squares.Count(s => s.IsMarked);
        await notifier.MarkCountUpdated(gameId, userId, markedCount);

        var isMarked = board.Squares.OrderBy(s => s.Position).Select(s => s.IsMarked).ToList();
        var winningLine = BingoWinChecker.FindWinningLine(isMarked);

        if (winningLine is not null)
        {
            game.Status = GameStatus.Completed;
            game.WinnerId = userId;
            game.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            await notifier.GameWon(gameId, userId, winningLine.Type, winningLine.Index);
        }

        return board;
    }

    private async Task<Board> PersistBoardAsync(Guid gameId, Guid userId, Dictionary<int, Guid> layout, CancellationToken ct)
    {
        var existing = await db.Boards.AnyAsync(b => b.GameId == gameId && b.UserId == userId, ct);
        if (existing)
        {
            throw new ConflictException("You have already created your board for this game. Boards cannot be edited once created.");
        }

        var board = new Board
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        for (var position = 0; position < BingoWinChecker.SquareCount; position++)
        {
            if (position == BoardLayout.FreeSpacePosition)
            {
                board.Squares.Add(new BoardSquare
                {
                    Id = Guid.NewGuid(),
                    BoardId = board.Id,
                    Position = position,
                    IsFree = true,
                    IsMarked = true,
                    MarkedAt = DateTimeOffset.UtcNow,
                });
            }
            else
            {
                board.Squares.Add(new BoardSquare
                {
                    Id = Guid.NewGuid(),
                    BoardId = board.Id,
                    Position = position,
                    EventId = layout[position],
                    IsFree = false,
                    IsMarked = false,
                });
            }
        }

        db.Boards.Add(board);
        await db.SaveChangesAsync(ct);

        await notifier.BoardCreated(gameId, userId);

        return board;
    }

    private async Task<List<Guid>> ValidateAndLoadEventsAsync(Guid gameId, Guid userId, IReadOnlyList<Guid> eventIds, CancellationToken ct)
    {
        var game = await EnsureMemberAsync(gameId, userId, ct);

        if (game.Status != GameStatus.Active)
        {
            throw new ConflictException("This game has already been completed.");
        }

        if (eventIds.Count != BoardLayout.NonFreeSquareCount || eventIds.Distinct().Count() != BoardLayout.NonFreeSquareCount)
        {
            throw new ValidationException($"Exactly {BoardLayout.NonFreeSquareCount} distinct events must be chosen.");
        }

        var matchingCount = await db.Events.CountAsync(e => e.GameId == gameId && eventIds.Contains(e.Id), ct);
        if (matchingCount != eventIds.Count)
        {
            throw new ValidationException("One or more chosen events do not belong to this game.");
        }

        return eventIds.ToList();
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
}
