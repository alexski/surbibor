namespace Surbibor.Domain.Entities;

public class BoardSquare
{
    public Guid Id { get; set; }
    public Guid BoardId { get; set; }
    public Board Board { get; set; } = null!;

    /// <summary>Row-major position on the 5x5 grid, 0-24. Position 12 is the free center square.</summary>
    public int Position { get; set; }

    public Guid? EventId { get; set; }
    public Event? Event { get; set; }

    public bool IsFree { get; set; }
    public bool IsMarked { get; set; }
    public DateTimeOffset? MarkedAt { get; set; }
}
