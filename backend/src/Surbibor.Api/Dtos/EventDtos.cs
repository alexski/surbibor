namespace Surbibor.Api.Dtos;

public record AddEventRequest(string Text);

public record EventResponse(
    Guid Id,
    string Text,
    string Status,
    Guid CreatedByUserId,
    Guid? ProposedByUserId,
    Guid? ConfirmedByUserId,
    DateTimeOffset CreatedAt);
