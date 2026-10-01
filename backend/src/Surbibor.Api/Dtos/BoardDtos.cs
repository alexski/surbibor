namespace Surbibor.Api.Dtos;

public record ManualPlacementDto(int Position, Guid EventId);

public record CreateRandomBoardRequest(List<Guid> EventIds);

public record CreateManualBoardRequest(List<ManualPlacementDto> Positions);

public record BoardSquareResponse(
    int Position,
    Guid? EventId,
    string? EventText,
    string? EventStatus,
    Guid? EventProposedByUserId,
    bool IsFree,
    bool IsMarked,
    DateTimeOffset? MarkedAt);

public record PlayerBoardProgressResponse(Guid UserId, string Username, bool HasBoard, IReadOnlyList<int> MarkedPositions);

public record BoardResponse(Guid Id, Guid GameId, Guid UserId, DateTimeOffset CreatedAt, IReadOnlyList<BoardSquareResponse> Squares);
