using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Surbibor.Api.Auth;
using Surbibor.Api.Dtos;
using Surbibor.Domain.Entities;
using Surbibor.Infrastructure.Services;

namespace Surbibor.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/games/{gameId:guid}/board")]
public class BoardController(BoardService boardService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BoardResponse>> Get(Guid gameId, CancellationToken ct)
    {
        var board = await boardService.GetBoardAsync(gameId, User.GetUserId(), ct);
        if (board is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(board));
    }

    // Other players' boards, as marked positions only (no events), for the progress panel.
    [HttpGet("others")]
    public async Task<ActionResult<List<PlayerBoardProgressResponse>>> GetOthers(Guid gameId, CancellationToken ct)
    {
        var progress = await boardService.GetOtherPlayersProgressAsync(gameId, User.GetUserId(), ct);
        return Ok(progress
            .Select(p => new PlayerBoardProgressResponse(p.UserId, p.Username, p.HasBoard, p.MarkedPositions))
            .ToList());
    }

    [HttpPost("random")]
    public async Task<ActionResult<BoardResponse>> CreateRandom(Guid gameId, CreateRandomBoardRequest request, CancellationToken ct)
    {
        var board = await boardService.CreateRandomBoardAsync(gameId, User.GetUserId(), request.EventIds, ct);
        var full = await boardService.GetBoardAsync(gameId, User.GetUserId(), ct);
        return Ok(ToResponse(full!));
    }

    [HttpPost("manual")]
    public async Task<ActionResult<BoardResponse>> CreateManual(Guid gameId, CreateManualBoardRequest request, CancellationToken ct)
    {
        var placements = request.Positions.Select(p => new BoardPlacement(p.Position, p.EventId)).ToList();
        await boardService.CreateManualBoardAsync(gameId, User.GetUserId(), placements, ct);
        var full = await boardService.GetBoardAsync(gameId, User.GetUserId(), ct);
        return Ok(ToResponse(full!));
    }

    [HttpPost("squares/{position:int}/mark")]
    public async Task<ActionResult<BoardResponse>> Mark(Guid gameId, int position, CancellationToken ct)
    {
        await boardService.MarkSquareAsync(gameId, User.GetUserId(), position, ct);
        var full = await boardService.GetBoardAsync(gameId, User.GetUserId(), ct);
        return Ok(ToResponse(full!));
    }

    private static BoardResponse ToResponse(Board board) => new(
        board.Id,
        board.GameId,
        board.UserId,
        board.CreatedAt,
        board.Squares
            .OrderBy(s => s.Position)
            .Select(s => new BoardSquareResponse(
                s.Position,
                s.EventId,
                s.Event?.Text,
                s.Event?.Status.ToString(),
                s.Event?.ProposedByUserId,
                s.IsFree,
                s.IsMarked,
                s.MarkedAt))
            .ToList());
}
