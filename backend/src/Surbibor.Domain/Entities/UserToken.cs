namespace Surbibor.Domain.Entities;

/// <summary>
/// A single-use token emailed to a user (email verification or password reset).
/// Only the SHA-256 hash of the token is stored; the raw value exists only in the email link.
/// </summary>
public class UserToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public UserTokenPurpose Purpose { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}
