using Surbibor.Domain.Entities;
using Surbibor.Domain.Exceptions;
using Surbibor.Infrastructure.Services;

namespace Surbibor.Api.Tests.Unit;

public class EventServiceTests
{
    private static async Task<(EventService Events, GameService Games, Guid GameId, Guid OwnerId, Guid MemberId)> SetupAsync()
    {
        var db = TestSupport.CreateInMemoryDb();
        var games = new GameService(db);
        var events = new EventService(db, new NoOpGameNotifier());

        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();

        db.Users.Add(new User { Id = owner, Email = "owner@test.com", Username = "owner", PasswordHash = "x", CreatedAt = DateTimeOffset.UtcNow });
        db.Users.Add(new User { Id = member, Email = "member@test.com", Username = "member", PasswordHash = "x", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var game = await games.CreateGameAsync(owner, "Test Show", "desc");
        await games.JoinGameAsync(member, game.InviteCode);

        return (events, games, game.Id, owner, member);
    }

    [Fact]
    public async Task Propose_MovesEventToPendingConfirmation()
    {
        var (events, _, gameId, owner, _) = await SetupAsync();
        var evt = await events.AddEventAsync(gameId, owner, "Host says the catchphrase");

        var proposed = await events.ProposeAsync(gameId, evt.Id, owner);

        Assert.Equal(EventStatus.PendingConfirmation, proposed.Status);
        Assert.Equal(owner, proposed.ProposedByUserId);
    }

    [Fact]
    public async Task Confirm_ByDifferentMember_Succeeds()
    {
        var (events, _, gameId, owner, member) = await SetupAsync();
        var evt = await events.AddEventAsync(gameId, owner, "Someone cries on camera");
        await events.ProposeAsync(gameId, evt.Id, owner);

        var confirmed = await events.ConfirmAsync(gameId, evt.Id, member);

        Assert.Equal(EventStatus.Confirmed, confirmed.Status);
        Assert.Equal(member, confirmed.ConfirmedByUserId);
    }

    [Fact]
    public async Task Confirm_BySameProposer_Throws()
    {
        var (events, _, gameId, owner, _) = await SetupAsync();
        var evt = await events.AddEventAsync(gameId, owner, "A contestant is eliminated");
        await events.ProposeAsync(gameId, evt.Id, owner);

        await Assert.ThrowsAsync<ForbiddenException>(() => events.ConfirmAsync(gameId, evt.Id, owner));
    }

    [Fact]
    public async Task Confirm_WhenNotPending_Throws()
    {
        var (events, _, gameId, owner, member) = await SetupAsync();
        var evt = await events.AddEventAsync(gameId, owner, "Someone wins a prize");

        await Assert.ThrowsAsync<ConflictException>(() => events.ConfirmAsync(gameId, evt.Id, member));
    }

    [Fact]
    public async Task Unpropose_ByProposer_ReturnsEventToOpen()
    {
        var (events, _, gameId, owner, _) = await SetupAsync();
        var evt = await events.AddEventAsync(gameId, owner, "A twist is revealed");
        await events.ProposeAsync(gameId, evt.Id, owner);

        var unproposed = await events.UnproposeAsync(gameId, evt.Id, owner);

        Assert.Equal(EventStatus.Open, unproposed.Status);
        Assert.Null(unproposed.ProposedByUserId);
    }

    [Fact]
    public async Task Unpropose_ByDifferentMember_Throws()
    {
        var (events, _, gameId, owner, member) = await SetupAsync();
        var evt = await events.AddEventAsync(gameId, owner, "A twist is revealed");
        await events.ProposeAsync(gameId, evt.Id, owner);

        await Assert.ThrowsAsync<ForbiddenException>(() => events.UnproposeAsync(gameId, evt.Id, member));
    }

    [Fact]
    public async Task Unpropose_WhenNotPending_Throws()
    {
        var (events, _, gameId, owner, _) = await SetupAsync();
        var evt = await events.AddEventAsync(gameId, owner, "Someone wins a prize");

        await Assert.ThrowsAsync<ConflictException>(() => events.UnproposeAsync(gameId, evt.Id, owner));
    }

    [Fact]
    public async Task Reject_ReturnsEventToOpen()
    {
        var (events, _, gameId, owner, member) = await SetupAsync();
        var evt = await events.AddEventAsync(gameId, owner, "A twist is revealed");
        await events.ProposeAsync(gameId, evt.Id, owner);

        var rejected = await events.RejectAsync(gameId, evt.Id, member);

        Assert.Equal(EventStatus.Open, rejected.Status);
        Assert.Null(rejected.ProposedByUserId);
    }

    [Fact]
    public async Task NonMember_CannotAddEvents()
    {
        var (events, _, gameId, _, _) = await SetupAsync();
        var outsider = Guid.NewGuid();

        await Assert.ThrowsAsync<ForbiddenException>(() => events.AddEventAsync(gameId, outsider, "Sneaky event"));
    }
}
