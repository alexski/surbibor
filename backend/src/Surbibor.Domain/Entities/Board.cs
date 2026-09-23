namespace Surbibor.Domain.Entities;

public class Board
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public Game Game { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public List<BoardSquare> Squares { get; set; } = [];
}
