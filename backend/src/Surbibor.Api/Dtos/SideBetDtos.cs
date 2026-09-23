namespace Surbibor.Api.Dtos;

public record CreateSideBetRequest(string Text, DateTimeOffset PlacingClosesAt, DateTimeOffset ExpectedResolutionAt);

public record PlaceWagerRequest(bool Prediction, int Stake);

public record ProposeSideBetOutcomeRequest(bool Outcome);

public record SideBetWagerResponse(Guid UserId, bool Prediction, int Stake, DateTimeOffset PlacedAt);

public record SideBetResponse(
    Guid Id,
    string Text,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset PlacingClosesAt,
    DateTimeOffset ExpectedResolutionAt,
    string Status,
    bool? ProposedOutcome,
    Guid? ProposedByUserId,
    DateTimeOffset? ProposedAt,
    bool? Outcome,
    Guid? ResolvedByUserId,
    DateTimeOffset? ResolvedAt,
    IReadOnlyList<SideBetWagerResponse> Wagers);
