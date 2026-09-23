namespace Surbibor.Domain.Entities;

public class SideBetWager
{
    public Guid Id { get; set; }
    public Guid SideBetId { get; set; }
    public SideBet SideBet { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>true = predicts the bet happens, false = predicts it doesn't.</summary>
    public bool Prediction { get; set; }

    public int Stake { get; set; }
    public DateTimeOffset PlacedAt { get; set; }
}
