using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Surbibor.Domain.Entities;
using Surbibor.Domain.Exceptions;
using Surbibor.Infrastructure;
using Surbibor.Infrastructure.Email;
using Surbibor.Infrastructure.Services;

namespace Surbibor.Api.Tests.Unit;

public class AuthServiceTests
{
    private const string Email = "alice@example.com";
    private const string Password = "SuperSecret123";

    private readonly SurbiborDbContext _db = TestSupport.CreateInMemoryDb();
    private readonly CapturingEmailSender _emails = new();
    private readonly AuthService _auth;

    public AuthServiceTests()
    {
        _auth = new AuthService(
            _db,
            _emails,
            Options.Create(new FrontendOptions { Origin = "https://bingo.example" }),
            NullLogger<AuthService>.Instance);
    }

    private async Task<User> RegisterVerifiedAsync()
    {
        var user = await _auth.RegisterAsync(Email, "alice", Password);
        await _auth.VerifyEmailAsync(_emails.LastTokenFor(Email));
        return user;
    }

    [Fact]
    public async Task Register_SendsVerificationLink_AndUserStartsUnverified()
    {
        var user = await _auth.RegisterAsync(Email, "alice", Password);

        Assert.Null(user.EmailVerifiedAt);
        var message = Assert.Single(_emails.Sent);
        Assert.Equal(Email, message.To);
        Assert.Contains("https://bingo.example/verify-email?token=", message.TextBody);
    }

    [Fact]
    public async Task Register_StoresOnlyTokenHash()
    {
        await _auth.RegisterAsync(Email, "alice", Password);
        var rawToken = _emails.LastTokenFor(Email);

        var stored = await _db.UserTokens.SingleAsync();
        Assert.NotEqual(rawToken, stored.TokenHash);
        Assert.DoesNotContain(rawToken, stored.TokenHash);
    }

    [Fact]
    public async Task VerifyEmail_MarksUserVerified_AndTokenIsSingleUse()
    {
        await _auth.RegisterAsync(Email, "alice", Password);
        var token = _emails.LastTokenFor(Email);

        var user = await _auth.VerifyEmailAsync(token);
        Assert.NotNull(user.EmailVerifiedAt);

        await Assert.ThrowsAsync<ValidationException>(() => _auth.VerifyEmailAsync(token));
    }

    [Fact]
    public async Task VerifyEmail_RejectsExpiredToken()
    {
        await _auth.RegisterAsync(Email, "alice", Password);
        var token = _emails.LastTokenFor(Email);

        var stored = await _db.UserTokens.SingleAsync();
        stored.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ValidationException>(() => _auth.VerifyEmailAsync(token));
    }

    [Fact]
    public async Task ResendVerification_IsThrottled_AndRefusedOnceVerified()
    {
        var user = await _auth.RegisterAsync(Email, "alice", Password);

        await Assert.ThrowsAsync<ValidationException>(() => _auth.ResendVerificationEmailAsync(user.Id));

        foreach (var token in _db.UserTokens)
        {
            token.CreatedAt = DateTimeOffset.UtcNow - AuthService.EmailResendCooldown - TimeSpan.FromSeconds(1);
        }
        await _db.SaveChangesAsync();

        await _auth.ResendVerificationEmailAsync(user.Id);
        Assert.Equal(2, _emails.Sent.Count);

        await _auth.VerifyEmailAsync(_emails.LastTokenFor(Email));
        await Assert.ThrowsAsync<ValidationException>(() => _auth.ResendVerificationEmailAsync(user.Id));
    }

    [Fact]
    public async Task ResendVerification_WhenSendingFails_ThrowsServiceUnavailable()
    {
        var user = await _auth.RegisterAsync(Email, "alice", Password);
        foreach (var token in _db.UserTokens)
        {
            token.CreatedAt = DateTimeOffset.UtcNow - AuthService.EmailResendCooldown - TimeSpan.FromSeconds(1);
        }
        await _db.SaveChangesAsync();

        var failingAuth = new AuthService(
            _db,
            new FailingEmailSender(),
            Options.Create(new FrontendOptions()),
            NullLogger<AuthService>.Instance);

        await Assert.ThrowsAsync<ServiceUnavailableException>(() => failingAuth.ResendVerificationEmailAsync(user.Id));
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_CompletesSilentlyWithoutSending()
    {
        await _auth.RequestPasswordResetAsync("nobody@example.com");

        Assert.Empty(_emails.Sent);
    }

    [Fact]
    public async Task ForgotPassword_UnverifiedEmail_DoesNotSend()
    {
        await _auth.RegisterAsync(Email, "alice", Password);

        await _auth.RequestPasswordResetAsync(Email);

        Assert.Single(_emails.Sent); // only the original verification email
    }

    [Fact]
    public async Task ResetPassword_WithEmailedToken_ChangesPassword()
    {
        await RegisterVerifiedAsync();

        await _auth.RequestPasswordResetAsync("  ALICE@example.com ");
        var message = _emails.Sent.Last();
        Assert.Contains("https://bingo.example/reset-password?token=", message.TextBody);

        await _auth.ResetPasswordAsync(_emails.LastTokenFor(Email), "BrandNewPass1", "BrandNewPass1");

        await Assert.ThrowsAsync<ValidationException>(() => _auth.LoginAsync(Email, Password));
        var user = await _auth.LoginAsync(Email, "BrandNewPass1");
        Assert.Equal("alice", user.Username);
    }

    [Fact]
    public async Task ResetPassword_TokenIsSingleUse_AndOtherResetLinksAreInvalidated()
    {
        await RegisterVerifiedAsync();

        await _auth.RequestPasswordResetAsync(Email);
        var firstToken = _emails.LastTokenFor(Email);

        foreach (var token in _db.UserTokens)
        {
            token.CreatedAt = DateTimeOffset.UtcNow - AuthService.EmailResendCooldown - TimeSpan.FromSeconds(1);
        }
        await _db.SaveChangesAsync();

        await _auth.RequestPasswordResetAsync(Email);
        var secondToken = _emails.LastTokenFor(Email);
        Assert.NotEqual(firstToken, secondToken);

        await _auth.ResetPasswordAsync(secondToken, "BrandNewPass1", "BrandNewPass1");

        await Assert.ThrowsAsync<ValidationException>(() => _auth.ResetPasswordAsync(secondToken, "AnotherPass1", "AnotherPass1"));
        await Assert.ThrowsAsync<ValidationException>(() => _auth.ResetPasswordAsync(firstToken, "AnotherPass1", "AnotherPass1"));
    }

    [Fact]
    public async Task ResetPassword_VerificationTokenCannotBeUsedForReset()
    {
        await _auth.RegisterAsync(Email, "alice", Password);
        var verificationToken = _emails.LastTokenFor(Email);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _auth.ResetPasswordAsync(verificationToken, "BrandNewPass1", "BrandNewPass1"));
    }

    [Fact]
    public async Task ForgotPassword_IsThrottled()
    {
        await RegisterVerifiedAsync();

        await _auth.RequestPasswordResetAsync(Email);
        await _auth.RequestPasswordResetAsync(Email);

        Assert.Equal(2, _emails.Sent.Count); // verification + one reset
    }

    [Fact]
    public async Task ResetPassword_RejectsMismatchedOrShortPasswords()
    {
        await RegisterVerifiedAsync();
        await _auth.RequestPasswordResetAsync(Email);
        var token = _emails.LastTokenFor(Email);

        await Assert.ThrowsAsync<ValidationException>(() => _auth.ResetPasswordAsync(token, "BrandNewPass1", "Different1"));
        await Assert.ThrowsAsync<ValidationException>(() => _auth.ResetPasswordAsync(token, "short", "short"));

        // Validation failures happen before the token is consumed, so it still works.
        await _auth.ResetPasswordAsync(token, "BrandNewPass1", "BrandNewPass1");
    }
}

internal class FailingEmailSender : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct = default) =>
        throw new TimeoutException("SMTP unreachable");
}
