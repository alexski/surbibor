namespace Surbibor.Api.Dtos;

public record CreateGameRequest(string Name, string? Description);

public record JoinGameRequest(string InviteCode);

public record GameSummaryResponse(Guid Id, string Name, string Description, string InviteCode, string Status, DateTimeOffset CreatedAt);

public record GameMemberResponse(Guid UserId, string Username, DateTimeOffset JoinedAt, int Points);

public record GameDetailResponse(
    Guid Id,
    string Name,
    string Description,
    string InviteCode,
    string Status,
    Guid? WinnerId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<GameMemberResponse> Members);
