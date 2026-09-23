using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Surbibor.Api.Auth;
using Surbibor.Api.Dtos;
using Surbibor.Domain.Entities;
using Surbibor.Infrastructure.Services;

namespace Surbibor.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/games")]
public class GamesController(GameService gameService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<GameSummaryResponse>> Create(CreateGameRequest request, CancellationToken ct)
    {
        var game = await gameService.CreateGameAsync(User.GetUserId(), request.Name, request.Description ?? string.Empty, ct);
        return Ok(ToSummary(game));
    }

    [HttpGet]
    public async Task<ActionResult<List<GameSummaryResponse>>> List(CancellationToken ct)
    {
        var games = await gameService.ListGamesForUserAsync(User.GetUserId(), ct);
        return Ok(games.Select(ToSummary).ToList());
    }

    [HttpGet("{gameId:guid}")]
    public async Task<ActionResult<GameDetailResponse>> Get(Guid gameId, CancellationToken ct)
    {
        var game = await gameService.GetGameForUserAsync(gameId, User.GetUserId(), ct);

        return Ok(new GameDetailResponse(
            game.Id,
            game.Name,
            game.Description,
            game.InviteCode,
            game.Status.ToString(),
            game.WinnerId,
            game.CreatedAt,
            game.CompletedAt,
            game.Memberships.Select(m => new GameMemberResponse(m.UserId, m.User.Username, m.JoinedAt, m.Points)).ToList()));
    }

    [HttpPost("join")]
    public async Task<ActionResult<GameSummaryResponse>> Join(JoinGameRequest request, CancellationToken ct)
    {
        var game = await gameService.JoinGameAsync(User.GetUserId(), request.InviteCode, ct);
        return Ok(ToSummary(game));
    }

    private static GameSummaryResponse ToSummary(Game game) =>
        new(game.Id, game.Name, game.Description, game.InviteCode, game.Status.ToString(), game.CreatedAt);
}
