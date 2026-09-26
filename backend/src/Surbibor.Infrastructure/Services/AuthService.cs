using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Surbibor.Domain.Entities;
using Surbibor.Domain.Exceptions;
using Surbibor.Infrastructure.Email;

namespace Surbibor.Infrastructure.Services;

public class AuthService(
    SurbiborDbContext db,
    IEmailSender emailSender,
    IOptions<FrontendOptions> frontendOptions,
    ILogger<AuthService> logger)
{
    public static readonly TimeSpan EmailVerificationLifetime = TimeSpan.FromHours(24);
    public static readonly TimeSpan PasswordResetLifetime = TimeSpan.FromHours(1);

    // Minimum gap between two emails of the same kind to the same user, so the
    // "resend" and "forgot password" endpoints can't be used to flood an inbox.
    public static readonly TimeSpan EmailResendCooldown = TimeSpan.FromMinutes(1);

    public async Task<User> RegisterAsync(string email, string username, string password, CancellationToken ct = default)
    {
        email = email.Trim().ToLowerInvariant();
        username = username.Trim();

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            throw new ValidationException("A valid email is required.");
        }

        if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
        {
            throw new ValidationException("Username must be at least 3 characters.");
        }

        ValidatePassword(password);

        var emailTaken = await db.Users.AnyAsync(u => u.Email == email, ct);
        if (emailTaken)
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var usernameTaken = await db.Users.AnyAsync(u => u.Username == username, ct);
        if (usernameTaken)
        {
            throw new ConflictException("This username is already taken.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        // A mail outage shouldn't block sign-up; the user can resend the link later.
        try
        {
            await SendVerificationEmailAsync(user, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send verification email to user {UserId}", user.Id);
        }

        return user;
    }

    public async Task<User> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        email = email.Trim().ToLowerInvariant();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            throw new ValidationException("Invalid email or password.");
        }

        return user;
    }

    public async Task<User> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("User not found.");
    }

    public async Task ResendVerificationEmailAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await GetUserAsync(userId, ct);

        if (user.EmailVerifiedAt is not null)
        {
            throw new ValidationException("Your email is already verified.");
        }

        if (await SentRecentlyAsync(user.Id, UserTokenPurpose.EmailVerification, ct))
        {
            throw new ValidationException("A verification email was just sent. Please wait a minute before requesting another.");
        }

        try
        {
            await SendVerificationEmailAsync(user, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send verification email to user {UserId}", user.Id);
            throw new ServiceUnavailableException("We couldn't send the email right now. Please try again later.");
        }
    }

    public async Task<User> VerifyEmailAsync(string token, CancellationToken ct = default)
    {
        var userToken = await ConsumeTokenAsync(token, UserTokenPurpose.EmailVerification, ct)
            ?? throw new ValidationException("This verification link is invalid or has expired.");

        var user = userToken.User;
        user.EmailVerifiedAt ??= DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return user;
    }

    /// <summary>
    /// Emails a password reset link if the address belongs to a verified account. Always
    /// completes silently otherwise, so the endpoint can't be used to discover which emails
    /// are registered.
    /// </summary>
    public async Task RequestPasswordResetAsync(string email, CancellationToken ct = default)
    {
        email = email.Trim().ToLowerInvariant();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            return;
        }

        if (user.EmailVerifiedAt is null)
        {
            logger.LogInformation("Password reset requested for unverified user {UserId}; no email sent", user.Id);
            return;
        }

        if (await SentRecentlyAsync(user.Id, UserTokenPurpose.PasswordReset, ct))
        {
            return;
        }

        var rawToken = await CreateTokenAsync(user, UserTokenPurpose.PasswordReset, PasswordResetLifetime, ct);
        var link = BuildLink("/reset-password", rawToken);

        // Swallowed (and logged) rather than surfaced: an error that only occurs for real
        // accounts would reveal which emails are registered.
        try
        {
            await emailSender.SendAsync(BuildEmail(
                user,
                subject: "Reset your Surbibor password",
                intro: "Someone (hopefully you) asked to reset the password for your Surbibor account. Use the link below to choose a new one. It expires in 1 hour.",
                linkText: "Reset password",
                link: link,
                outro: "If you didn't request this, you can ignore this email; your password won't change."), ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send password reset email to user {UserId}", user.Id);
        }
    }

    public async Task<User> ResetPasswordAsync(string token, string newPassword, string confirmPassword, CancellationToken ct = default)
    {
        ValidatePassword(newPassword);

        if (newPassword != confirmPassword)
        {
            throw new ValidationException("Passwords do not match.");
        }

        var userToken = await ConsumeTokenAsync(token, UserTokenPurpose.PasswordReset, ct)
            ?? throw new ValidationException("This reset link is invalid or has expired.");

        var user = userToken.User;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

        // Any other outstanding reset links for this account are now stale.
        var now = DateTimeOffset.UtcNow;
        var otherTokens = await db.UserTokens
            .Where(t => t.UserId == user.Id && t.Purpose == UserTokenPurpose.PasswordReset && t.UsedAt == null && t.Id != userToken.Id)
            .ToListAsync(ct);
        foreach (var other in otherTokens)
        {
            other.UsedAt = now;
        }

        await db.SaveChangesAsync(ct);

        return user;
    }

    private async Task SendVerificationEmailAsync(User user, CancellationToken ct)
    {
        var rawToken = await CreateTokenAsync(user, UserTokenPurpose.EmailVerification, EmailVerificationLifetime, ct);
        var link = BuildLink("/verify-email", rawToken);

        await emailSender.SendAsync(BuildEmail(
            user,
            subject: "Verify your Surbibor email",
            intro: "Thanks for signing up for Surbibor Bingo! Please confirm this is your email address. The link expires in 24 hours.",
            linkText: "Verify email",
            link: link,
            outro: "If you didn't create an account, you can ignore this email."), ct);
    }

    private async Task<string> CreateTokenAsync(User user, UserTokenPurpose purpose, TimeSpan lifetime, CancellationToken ct)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var now = DateTimeOffset.UtcNow;

        db.UserTokens.Add(new UserToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Purpose = purpose,
            TokenHash = HashToken(rawToken),
            CreatedAt = now,
            ExpiresAt = now + lifetime,
        });
        await db.SaveChangesAsync(ct);

        return rawToken;
    }

    /// <summary>Marks a valid token as used and returns it (with its user), or null if invalid.</summary>
    private async Task<UserToken?> ConsumeTokenAsync(string rawToken, UserTokenPurpose purpose, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var hash = HashToken(rawToken.Trim());
        var userToken = await db.UserTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.Purpose == purpose, ct);

        if (userToken is null || userToken.UsedAt is not null || userToken.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        userToken.UsedAt = DateTimeOffset.UtcNow;
        return userToken;
    }

    private Task<bool> SentRecentlyAsync(Guid userId, UserTokenPurpose purpose, CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow - EmailResendCooldown;
        return db.UserTokens.AnyAsync(t => t.UserId == userId && t.Purpose == purpose && t.CreatedAt > cutoff, ct);
    }

    private string BuildLink(string path, string rawToken) =>
        $"{frontendOptions.Value.Origin.TrimEnd('/')}{path}?token={Uri.EscapeDataString(rawToken)}";

    private static EmailMessage BuildEmail(User user, string subject, string intro, string linkText, string link, string outro)
    {
        var text = new StringBuilder()
            .AppendLine($"Hi {user.Username},")
            .AppendLine()
            .AppendLine(intro)
            .AppendLine()
            .AppendLine(link)
            .AppendLine()
            .AppendLine(outro)
            .ToString();

        var html = $"""
            <p>Hi {WebUtility.HtmlEncode(user.Username)},</p>
            <p>{WebUtility.HtmlEncode(intro)}</p>
            <p><a href="{WebUtility.HtmlEncode(link)}">{WebUtility.HtmlEncode(linkText)}</a></p>
            <p style="color:#666;font-size:0.9em">{WebUtility.HtmlEncode(outro)}</p>
            """;

        return new EmailMessage(user.Email, subject, text, html);
    }

    private static string HashToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            throw new ValidationException("Password must be at least 8 characters.");
        }
    }
}
