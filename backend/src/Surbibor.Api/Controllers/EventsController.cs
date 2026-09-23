using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Surbibor.Api.Auth;
using Surbibor.Api.Dtos;
using Surbibor.Domain.Entities;
using Surbibor.Infrastructure.Services;

namespace Surbibor.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/games/{gameId:guid}/events")]
public class EventsController(EventService eventService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<EventResponse>>> List(Guid gameId, CancellationToken ct)
    {
        var events = await eventService.ListEventsAsync(gameId, User.GetUserId(), ct);
        return Ok(events.Select(ToResponse).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<EventResponse>> Add(Guid gameId, AddEventRequest request, CancellationToken ct)
    {
        var evt = await eventService.AddEventAsync(gameId, User.GetUserId(), request.Text, ct);
        return Ok(ToResponse(evt));
    }

    [HttpPost("{eventId:guid}/propose")]
    public async Task<ActionResult<EventResponse>> Propose(Guid gameId, Guid eventId, CancellationToken ct)
    {
        var evt = await eventService.ProposeAsync(gameId, eventId, User.GetUserId(), ct);
        return Ok(ToResponse(evt));
    }

    [HttpPost("{eventId:guid}/confirm")]
    public async Task<ActionResult<EventResponse>> Confirm(Guid gameId, Guid eventId, CancellationToken ct)
    {
        var evt = await eventService.ConfirmAsync(gameId, eventId, User.GetUserId(), ct);
        return Ok(ToResponse(evt));
    }

    [HttpPost("{eventId:guid}/unpropose")]
    public async Task<ActionResult<EventResponse>> Unpropose(Guid gameId, Guid eventId, CancellationToken ct)
    {
        var evt = await eventService.UnproposeAsync(gameId, eventId, User.GetUserId(), ct);
        return Ok(ToResponse(evt));
    }

    [HttpPost("{eventId:guid}/reject")]
    public async Task<ActionResult<EventResponse>> Reject(Guid gameId, Guid eventId, CancellationToken ct)
    {
        var evt = await eventService.RejectAsync(gameId, eventId, User.GetUserId(), ct);
        return Ok(ToResponse(evt));
    }

    private static EventResponse ToResponse(Event evt) =>
        new(evt.Id, evt.Text, evt.Status.ToString(), evt.CreatedByUserId, evt.ProposedByUserId, evt.ConfirmedByUserId, evt.CreatedAt);
}
