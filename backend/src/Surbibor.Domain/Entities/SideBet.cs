namespace Surbibor.Domain.Entities;

public class SideBet
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public Game Game { get; set; } = null!;

    public string Text { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>No wagers are accepted at or after this time.</summary>
    public DateTimeOffset PlacingClosesAt { get; set; }

    /// <summary>Informational: when the outcome is expected to be known. Does not block late resolution.</summary>
    public DateTimeOffset ExpectedResolutionAt { get; set; }

    public SideBetStatus Status { get; set; } = SideBetStatus.Open;

    public bool? ProposedOutcome { get; set; }
    public Guid? ProposedByUserId { get; set; }
    public DateTimeOffset? ProposedAt { get; set; }

    public bool? Outcome { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    public List<SideBetWager> Wagers { get; set; } = [];
}
