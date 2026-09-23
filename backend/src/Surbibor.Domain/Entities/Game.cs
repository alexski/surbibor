namespace Surbibor.Domain.Entities;

public class Game
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InviteCode { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public GameStatus Status { get; set; } = GameStatus.Active;
    public Guid? WinnerId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public List<GameMembership> Memberships { get; set; } = [];
    public List<Event> Events { get; set; } = [];
    public List<Board> Boards { get; set; } = [];
    public List<SideBet> SideBets { get; set; } = [];
}
