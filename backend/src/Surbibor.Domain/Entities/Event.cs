namespace Surbibor.Domain.Entities;

public class Event
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public Game Game { get; set; } = null!;

    public string Text { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public EventStatus Status { get; set; } = EventStatus.Open;
    public Guid? ProposedByUserId { get; set; }
    public DateTimeOffset? ProposedAt { get; set; }
    public Guid? ConfirmedByUserId { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
}
