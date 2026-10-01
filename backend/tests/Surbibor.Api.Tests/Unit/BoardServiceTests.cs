using Surbibor.Domain.Entities;
using Surbibor.Domain.Exceptions;
using Surbibor.Domain.Logic;
using Surbibor.Infrastructure;
using Surbibor.Infrastructure.Services;

namespace Surbibor.Api.Tests.Unit;

public class BoardServiceTests
{
    private static async Task<(SurbiborDbContext Db, BoardService Boards, EventService Events, GameService Games, Guid GameId, Guid Owner, Guid Member, List<Guid> EventIds)> SetupAsync(int eventCount = 24)
    {
        var db = TestSupport.CreateInMemoryDb();
        var notifier = new NoOpGameNotifier();
        var games = new GameService(db);
        var events = new EventService(db, notifier);
        var boards = new BoardService(db, notifier);

        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();

        db.Users.Add(new User { Id = owner, Email = "owner@test.com", Username = "owner", PasswordHash = "x", CreatedAt = DateTimeOffset.UtcNow });
        db.Users.Add(new User { Id = member, Email = "member@test.com", Username = "member", PasswordHash = "x", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var game = await games.CreateGameAsync(owner, "Test Show", "desc");
        await games.JoinGameAsync(member, game.InviteCode);

        var eventIds = new List<Guid>();
        for (var i = 0; i < eventCount; i++)
        {
            var evt = await events.AddEventAsync(game.Id, owner, $"Event {i}");
            eventIds.Add(evt.Id);
        }

        return (db, boards, events, games, game.Id, owner, member, eventIds);
    }

    [Fact]
    public async Task CreateRandomBoard_ProducesFullGridWithFreeSpace()
    {
        var (_, boards, _, _, gameId, owner, _, eventIds) = await SetupAsync();

        var board = await boards.CreateRandomBoardAsync(gameId, owner, eventIds);

        Assert.Equal(25, board.Squares.Count);
        var freeSquare = board.Squares.Single(s => s.Position == BoardLayout.FreeSpacePosition);
        Assert.True(freeSquare.IsFree);
        Assert.True(freeSquare.IsMarked);
        Assert.Equal(24, board.Squares.Count(s => !s.IsFree));
        Assert.Equal(eventIds.ToHashSet(), board.Squares.Where(s => !s.IsFree).Select(s => s.EventId!.Value).ToHashSet());
    }

    [Fact]
    public async Task CreateRandomBoard_Throws_WhenWrongEventCount()
    {
        var (_, boards, _, _, gameId, owner, _, eventIds) = await SetupAsync(eventCount: 10);

        await Assert.ThrowsAsync<ValidationException>(() => boards.CreateRandomBoardAsync(gameId, owner, eventIds));
    }

    [Fact]
    public async Task CreateBoard_Throws_OnSecondAttempt()
    {
        var (_, boards, _, _, gameId, owner, _, eventIds) = await SetupAsync();
        await boards.CreateRandomBoardAsync(gameId, owner, eventIds);

        await Assert.ThrowsAsync<ConflictException>(() => boards.CreateRandomBoardAsync(gameId, owner, eventIds));
    }

    [Fact]
    public async Task MarkSquare_Throws_WhenEventNotConfirmed()
    {
        var (_, boards, _, _, gameId, owner, _, eventIds) = await SetupAsync();
        var board = await boards.CreateRandomBoardAsync(gameId, owner, eventIds);
        var square = board.Squares.First(s => !s.IsFree);

        await Assert.ThrowsAsync<ConflictException>(() => boards.MarkSquareAsync(gameId, owner, square.Position));
    }

    [Fact]
    public async Task MarkSquare_Succeeds_AfterConfirmation()
    {
        var (_, boards, events, _, gameId, owner, member, eventIds) = await SetupAsync();
        var board = await boards.CreateRandomBoardAsync(gameId, owner, eventIds);
        var square = board.Squares.First(s => !s.IsFree);

        await events.ProposeAsync(gameId, square.EventId!.Value, member);
        await events.ConfirmAsync(gameId, square.EventId!.Value, owner);

        var updated = await boards.MarkSquareAsync(gameId, owner, square.Position);

        Assert.True(updated.Squares.Single(s => s.Position == square.Position).IsMarked);
    }

    [Fact]
    public async Task MarkSquare_Awards5Points()
    {
        var (_, boards, events, games, gameId, owner, member, eventIds) = await SetupAsync();
        var board = await boards.CreateRandomBoardAsync(gameId, owner, eventIds);
        var square = board.Squares.First(s => !s.IsFree);

        await events.ProposeAsync(gameId, square.EventId!.Value, member);
        await events.ConfirmAsync(gameId, square.EventId!.Value, owner);
        await boards.MarkSquareAsync(gameId, owner, square.Position);

        var game = await games.GetGameForUserAsync(gameId, owner);
        Assert.Equal(55, game.Memberships.Single(m => m.UserId == owner).Points);
    }

    [Fact]
    public async Task Winning_CompletesGame_AndBlocksFurtherMarks()
    {
        var (db, boards, events, games, gameId, owner, member, eventIds) = await SetupAsync();

        // Force a deterministic layout: row 0 (positions 0-4) gets the first four event ids
        // plus a fifth arbitrary one; place remaining events elsewhere.
        var manualEventIds = eventIds;
        var positions = Enumerable.Range(0, 25).Where(p => p != BoardLayout.FreeSpacePosition).ToList();
        var manualPlacements = positions.Zip(manualEventIds, (p, e) => new BoardPlacement(p, e)).ToList();

        var board = await boards.CreateManualBoardAsync(gameId, owner, manualPlacements);

        var rowPositions = new[] { 0, 1, 2, 3, 4 };
        foreach (var pos in rowPositions)
        {
            var square = board.Squares.Single(s => s.Position == pos);
            await events.ProposeAsync(gameId, square.EventId!.Value, member);
            await events.ConfirmAsync(gameId, square.EventId!.Value, owner);
        }

        Board result = board;
        foreach (var pos in rowPositions)
        {
            result = await boards.MarkSquareAsync(gameId, owner, pos);
        }

        var game = await games.GetGameForUserAsync(gameId, owner);
        Assert.Equal(GameStatus.Completed, game.Status);
        Assert.Equal(owner, game.WinnerId);

        // Further game activity should now be blocked because the game is completed.
        var anotherSquare = board.Squares.First(s => !s.IsFree && !rowPositions.Contains(s.Position));
        await Assert.ThrowsAsync<ConflictException>(() => events.ProposeAsync(gameId, anotherSquare.EventId!.Value, member));
    }

    [Fact]
    public async Task GetOtherPlayersProgress_ExcludesSelf_AndReportsPlayersWithoutBoards()
    {
        var (_, boards, _, _, gameId, owner, member, _) = await SetupAsync();

        var progress = await boards.GetOtherPlayersProgressAsync(gameId, owner);

        var other = Assert.Single(progress);
        Assert.Equal(member, other.UserId);
        Assert.Equal("member", other.Username);
        Assert.False(other.HasBoard);
        Assert.Empty(other.MarkedPositions);
    }

    [Fact]
    public async Task GetOtherPlayersProgress_ReturnsMarkedPositions()
    {
        var (_, boards, events, _, gameId, owner, member, eventIds) = await SetupAsync();
        var board = await boards.CreateRandomBoardAsync(gameId, member, eventIds);

        var square = board.Squares.First(s => !s.IsFree);
        await events.ProposeAsync(gameId, square.EventId!.Value, owner);
        await events.ConfirmAsync(gameId, square.EventId!.Value, member);
        await boards.MarkSquareAsync(gameId, member, square.Position);

        var other = Assert.Single(await boards.GetOtherPlayersProgressAsync(gameId, owner));

        Assert.True(other.HasBoard);
        Assert.Equal(new[] { square.Position, BoardLayout.FreeSpacePosition }.Order(), other.MarkedPositions);
    }

    [Fact]
    public async Task GetOtherPlayersProgress_RequiresMembership()
    {
        var (_, boards, _, _, gameId, _, _, _) = await SetupAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() => boards.GetOtherPlayersProgressAsync(gameId, Guid.NewGuid()));
    }
}
