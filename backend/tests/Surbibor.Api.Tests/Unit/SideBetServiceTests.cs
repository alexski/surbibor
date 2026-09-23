using Surbibor.Domain.Entities;
using Surbibor.Domain.Exceptions;
using Surbibor.Infrastructure;
using Surbibor.Infrastructure.Services;

namespace Surbibor.Api.Tests.Unit;

public class SideBetServiceTests
{
    private static readonly DateTimeOffset Open = DateTimeOffset.UtcNow.AddHours(1);

    private static async Task<(SurbiborDbContext Db, SideBetService SideBets, GameService Games, Guid GameId, Guid Owner, Guid Member)> SetupAsync()
    {
        var db = TestSupport.CreateInMemoryDb();
        var notifier = new NoOpGameNotifier();
        var games = new GameService(db);
        var sideBets = new SideBetService(db, notifier);

        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();

        db.Users.Add(new User { Id = owner, Email = "owner@test.com", Username = "owner", PasswordHash = "x", CreatedAt = DateTimeOffset.UtcNow });
        db.Users.Add(new User { Id = member, Email = "member@test.com", Username = "member", PasswordHash = "x", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var game = await games.CreateGameAsync(owner, "Test Show", "desc");
        await games.JoinGameAsync(member, game.InviteCode);

        return (db, sideBets, games, game.Id, owner, member);
    }

    private static Task<SideBet> CreateOpenBetAsync(SideBetService sideBets, Guid gameId, Guid userId) =>
        sideBets.CreateSideBetAsync(gameId, userId, "Host wears a hat", Open, Open.AddHours(1));

    /// <summary>Simulates the betting deadline having already passed, bypassing the service's future-only validation on create.</summary>
    private static async Task CloseBettingAsync(SurbiborDbContext db, Guid sideBetId)
    {
        var sideBet = await db.SideBets.FindAsync(sideBetId);
        sideBet!.PlacingClosesAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Members_StartWith50Points()
    {
        var (_, _, games, gameId, owner, member) = await SetupAsync();

        var game = await games.GetGameForUserAsync(gameId, owner);

        Assert.All(game.Memberships, m => Assert.Equal(50, m.Points));
    }

    [Fact]
    public async Task PlaceWager_DeductsStakeFromBalance()
    {
        var (db, sideBets, games, gameId, owner, member) = await SetupAsync();
        var bet = await CreateOpenBetAsync(sideBets, gameId, owner);

        await sideBets.PlaceWagerAsync(gameId, bet.Id, member, prediction: true, stake: 20);

        var game = await games.GetGameForUserAsync(gameId, owner);
        Assert.Equal(30, game.Memberships.Single(m => m.UserId == member).Points);
    }

    [Fact]
    public async Task PlaceWager_BelowMinimumStake_Throws()
    {
        var (_, sideBets, _, gameId, owner, member) = await SetupAsync();
        var bet = await CreateOpenBetAsync(sideBets, gameId, owner);

        await Assert.ThrowsAsync<ValidationException>(() => sideBets.PlaceWagerAsync(gameId, bet.Id, member, true, 4));
    }

    [Fact]
    public async Task PlaceWager_AboveBalance_Throws()
    {
        var (_, sideBets, _, gameId, owner, member) = await SetupAsync();
        var bet = await CreateOpenBetAsync(sideBets, gameId, owner);

        await Assert.ThrowsAsync<ValidationException>(() => sideBets.PlaceWagerAsync(gameId, bet.Id, member, true, 51));
    }

    [Fact]
    public async Task PlaceWager_WhenBalanceBelowFive_AllowsAllIn()
    {
        var (db, sideBets, games, gameId, owner, member) = await SetupAsync();

        // Drain the member down to 3 points via a losing wager, then confirm the loss to lock it in.
        var drainBet = await CreateOpenBetAsync(sideBets, gameId, owner);
        await sideBets.PlaceWagerAsync(gameId, drainBet.Id, member, prediction: true, stake: 47);
        await CloseBettingAsync(db, drainBet.Id);
        await sideBets.ProposeOutcomeAsync(gameId, drainBet.Id, owner, outcome: false);
        await sideBets.ConfirmOutcomeAsync(gameId, drainBet.Id, member);

        var game = await games.GetGameForUserAsync(gameId, owner);
        Assert.Equal(3, game.Memberships.Single(m => m.UserId == member).Points);

        var bet = await CreateOpenBetAsync(sideBets, gameId, owner);
        var afterAllIn = await sideBets.PlaceWagerAsync(gameId, bet.Id, member, prediction: true, stake: 3);

        Assert.Single(afterAllIn.Wagers);
    }

    [Fact]
    public async Task PlaceWager_Twice_Throws()
    {
        var (_, sideBets, _, gameId, owner, member) = await SetupAsync();
        var bet = await CreateOpenBetAsync(sideBets, gameId, owner);
        await sideBets.PlaceWagerAsync(gameId, bet.Id, member, true, 10);

        await Assert.ThrowsAsync<ConflictException>(() => sideBets.PlaceWagerAsync(gameId, bet.Id, member, false, 10));
    }

    [Fact]
    public async Task PlaceWager_AfterClosing_Throws()
    {
        var (db, sideBets, _, gameId, owner, member) = await SetupAsync();
        var bet = await CreateOpenBetAsync(sideBets, gameId, owner);
        await CloseBettingAsync(db, bet.Id);

        await Assert.ThrowsAsync<ConflictException>(() => sideBets.PlaceWagerAsync(gameId, bet.Id, member, true, 10));
    }

    [Fact]
    public async Task ProposeOutcome_BeforeBettingCloses_Throws()
    {
        var (_, sideBets, _, gameId, owner, _) = await SetupAsync();
        var bet = await CreateOpenBetAsync(sideBets, gameId, owner);

        await Assert.ThrowsAsync<ConflictException>(() => sideBets.ProposeOutcomeAsync(gameId, bet.Id, owner, true));
    }

    [Fact]
    public async Task ConfirmOutcome_PaysDoubleToWinners_AndNothingToLosers()
    {
        var (db, sideBets, games, gameId, owner, member) = await SetupAsync();
        var bet = await CreateOpenBetAsync(sideBets, gameId, owner);

        await sideBets.PlaceWagerAsync(gameId, bet.Id, owner, prediction: true, stake: 10);
        await sideBets.PlaceWagerAsync(gameId, bet.Id, member, prediction: false, stake: 15);
        await CloseBettingAsync(db, bet.Id);

        await sideBets.ProposeOutcomeAsync(gameId, bet.Id, owner, outcome: true);
        var resolved = await sideBets.ConfirmOutcomeAsync(gameId, bet.Id, member);

        Assert.Equal(SideBetStatus.Resolved, resolved.Status);
        Assert.True(resolved.Outcome);

        var game = await games.GetGameForUserAsync(gameId, owner);
        Assert.Equal(50 - 10 + 20, game.Memberships.Single(m => m.UserId == owner).Points);
        Assert.Equal(50 - 15, game.Memberships.Single(m => m.UserId == member).Points);
    }

    [Fact]
    public async Task ConfirmOutcome_BySameProposer_Throws()
    {
        var (db, sideBets, _, gameId, owner, _) = await SetupAsync();
        var bet = await CreateOpenBetAsync(sideBets, gameId, owner);
        await CloseBettingAsync(db, bet.Id);
        await sideBets.ProposeOutcomeAsync(gameId, bet.Id, owner, true);

        await Assert.ThrowsAsync<ForbiddenException>(() => sideBets.ConfirmOutcomeAsync(gameId, bet.Id, owner));
    }

    [Fact]
    public async Task RejectOutcome_ReturnsToOpen_AndDoesNotPayOut()
    {
        var (db, sideBets, games, gameId, owner, member) = await SetupAsync();
        var bet = await CreateOpenBetAsync(sideBets, gameId, owner);
        await sideBets.PlaceWagerAsync(gameId, bet.Id, owner, prediction: true, stake: 10);
        await CloseBettingAsync(db, bet.Id);

        await sideBets.ProposeOutcomeAsync(gameId, bet.Id, owner, true);
        var rejected = await sideBets.RejectOutcomeAsync(gameId, bet.Id, member);

        Assert.Equal(SideBetStatus.Open, rejected.Status);
        Assert.Null(rejected.ProposedByUserId);

        var game = await games.GetGameForUserAsync(gameId, owner);
        Assert.Equal(40, game.Memberships.Single(m => m.UserId == owner).Points);
    }

    [Fact]
    public async Task NonMember_CannotPlaceWager()
    {
        var (_, sideBets, _, gameId, owner, _) = await SetupAsync();
        var bet = await CreateOpenBetAsync(sideBets, gameId, owner);
        var outsider = Guid.NewGuid();

        await Assert.ThrowsAsync<ForbiddenException>(() => sideBets.PlaceWagerAsync(gameId, bet.Id, outsider, true, 10));
    }
}
