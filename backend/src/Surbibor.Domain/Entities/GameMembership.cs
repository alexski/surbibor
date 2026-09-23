namespace Surbibor.Domain.Entities;

public class GameMembership
{
    public Guid GameId { get; set; }
    public Game Game { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTimeOffset JoinedAt { get; set; }

    public int Points { get; set; } = 50;
}
