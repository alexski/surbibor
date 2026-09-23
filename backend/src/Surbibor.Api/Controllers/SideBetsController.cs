using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Surbibor.Api.Auth;
using Surbibor.Api.Dtos;
using Surbibor.Domain.Entities;
using Surbibor.Infrastructure.Services;

namespace Surbibor.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/games/{gameId:guid}/sidebets")]
public class SideBetsController(SideBetService sideBetService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SideBetResponse>>> List(Guid gameId, CancellationToken ct)
    {
        var sideBets = await sideBetService.ListSideBetsAsync(gameId, User.GetUserId(), ct);
        return Ok(sideBets.Select(ToResponse).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<SideBetResponse>> Create(Guid gameId, CreateSideBetRequest request, CancellationToken ct)
    {
        var sideBet = await sideBetService.CreateSideBetAsync(
            gameId, User.GetUserId(), request.Text, request.PlacingClosesAt, request.ExpectedResolutionAt, ct);
        return Ok(ToResponse(sideBet));
    }

    [HttpPost("{sideBetId:guid}/wagers")]
    public async Task<ActionResult<SideBetResponse>> PlaceWager(Guid gameId, Guid sideBetId, PlaceWagerRequest request, CancellationToken ct)
    {
        var sideBet = await sideBetService.PlaceWagerAsync(gameId, sideBetId, User.GetUserId(), request.Prediction, request.Stake, ct);
        return Ok(ToResponse(sideBet));
    }

    [HttpPost("{sideBetId:guid}/propose")]
    public async Task<ActionResult<SideBetResponse>> Propose(Guid gameId, Guid sideBetId, ProposeSideBetOutcomeRequest request, CancellationToken ct)
    {
        var sideBet = await sideBetService.ProposeOutcomeAsync(gameId, sideBetId, User.GetUserId(), request.Outcome, ct);
        return Ok(ToResponse(sideBet));
    }

    [HttpPost("{sideBetId:guid}/confirm")]
    public async Task<ActionResult<SideBetResponse>> Confirm(Guid gameId, Guid sideBetId, CancellationToken ct)
    {
        var sideBet = await sideBetService.ConfirmOutcomeAsync(gameId, sideBetId, User.GetUserId(), ct);
        return Ok(ToResponse(sideBet));
    }

    [HttpPost("{sideBetId:guid}/reject")]
    public async Task<ActionResult<SideBetResponse>> Reject(Guid gameId, Guid sideBetId, CancellationToken ct)
    {
        var sideBet = await sideBetService.RejectOutcomeAsync(gameId, sideBetId, User.GetUserId(), ct);
        return Ok(ToResponse(sideBet));
    }

    private static SideBetResponse ToResponse(SideBet sb) => new(
        sb.Id,
        sb.Text,
        sb.CreatedByUserId,
        sb.CreatedAt,
        sb.PlacingClosesAt,
        sb.ExpectedResolutionAt,
        sb.Status.ToString(),
        sb.ProposedOutcome,
        sb.ProposedByUserId,
        sb.ProposedAt,
        sb.Outcome,
        sb.ResolvedByUserId,
        sb.ResolvedAt,
        sb.Wagers
            .OrderBy(w => w.PlacedAt)
            .Select(w => new SideBetWagerResponse(w.UserId, w.Prediction, w.Stake, w.PlacedAt))
            .ToList());
}
