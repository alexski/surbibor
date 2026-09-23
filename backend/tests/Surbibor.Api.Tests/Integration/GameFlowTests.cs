using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Surbibor.Api.Dtos;

namespace Surbibor.Api.Tests.Integration;

public class GameFlowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public GameFlowTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, UserResponse User)> RegisterAndAuthenticateAsync(string emailPrefix)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            $"{emailPrefix}@example.com", $"{emailPrefix}_user", "SuperSecret123"));

        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        return (client, auth.User);
    }

    [Fact]
    public async Task FullGameLifecycle_RegisterCreateProposeConfirmMarkAndWin()
    {
        var (ownerClient, owner) = await RegisterAndAuthenticateAsync("owner");
        var (memberClient, _) = await RegisterAndAuthenticateAsync("member");

        // Create game
        var createResponse = await ownerClient.PostAsJsonAsync("/api/games", new CreateGameRequest("Reality Show S1", "weekly recap"));
        createResponse.EnsureSuccessStatusCode();
        var game = await createResponse.Content.ReadFromJsonAsync<GameSummaryResponse>();
        Assert.NotNull(game);

        // Member joins via invite code
        var joinResponse = await memberClient.PostAsJsonAsync("/api/games/join", new JoinGameRequest(game!.InviteCode));
        joinResponse.EnsureSuccessStatusCode();

        // Owner adds 24 events
        var eventIds = new List<Guid>();
        for (var i = 0; i < 24; i++)
        {
            var addResponse = await ownerClient.PostAsJsonAsync($"/api/games/{game.Id}/events", new AddEventRequest($"Event {i} happens"));
            addResponse.EnsureSuccessStatusCode();
            var evt = await addResponse.Content.ReadFromJsonAsync<EventResponse>();
            eventIds.Add(evt!.Id);
        }

        // Owner builds a random board
        var boardResponse = await ownerClient.PostAsJsonAsync($"/api/games/{game.Id}/board/random", new CreateRandomBoardRequest(eventIds));
        boardResponse.EnsureSuccessStatusCode();
        var board = await boardResponse.Content.ReadFromJsonAsync<BoardResponse>();
        Assert.NotNull(board);
        Assert.Equal(25, board!.Squares.Count);
        Assert.Contains(board.Squares, s => s.IsFree && s.IsMarked);

        // Building a second board should be rejected — boards are locked at creation.
        var secondAttempt = await ownerClient.PostAsJsonAsync($"/api/games/{game.Id}/board/random", new CreateRandomBoardRequest(eventIds));
        Assert.Equal(HttpStatusCode.Conflict, secondAttempt.StatusCode);

        // Pick one non-free square, propose + confirm its event, then mark it.
        var targetSquare = board.Squares.First(s => !s.IsFree);

        var proposeResponse = await memberClient.PostAsync($"/api/games/{game.Id}/events/{targetSquare.EventId}/propose", null);
        proposeResponse.EnsureSuccessStatusCode();

        // The proposer cannot confirm their own proposal.
        var selfConfirm = await memberClient.PostAsync($"/api/games/{game.Id}/events/{targetSquare.EventId}/confirm", null);
        Assert.Equal(HttpStatusCode.Forbidden, selfConfirm.StatusCode);

        var confirmResponse = await ownerClient.PostAsync($"/api/games/{game.Id}/events/{targetSquare.EventId}/confirm", null);
        confirmResponse.EnsureSuccessStatusCode();

        var markResponse = await ownerClient.PostAsync($"/api/games/{game.Id}/board/squares/{targetSquare.Position}/mark", null);
        markResponse.EnsureSuccessStatusCode();
        var markedBoard = await markResponse.Content.ReadFromJsonAsync<BoardResponse>();
        Assert.True(markedBoard!.Squares.Single(s => s.Position == targetSquare.Position).IsMarked);

        // Marking an unconfirmed square is rejected.
        var otherSquare = board.Squares.First(s => !s.IsFree && s.Position != targetSquare.Position);
        var badMark = await ownerClient.PostAsync($"/api/games/{game.Id}/board/squares/{otherSquare.Position}/mark", null);
        Assert.Equal(HttpStatusCode.Conflict, badMark.StatusCode);

        // Unauthenticated requests are rejected.
        var anonymousClient = _factory.CreateClient();
        var anonResponse = await anonymousClient.GetAsync($"/api/games/{game.Id}");
        Assert.Equal(HttpStatusCode.Unauthorized, anonResponse.StatusCode);
    }
}
