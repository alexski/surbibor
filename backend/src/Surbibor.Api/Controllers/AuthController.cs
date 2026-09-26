using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Surbibor.Api.Auth;
using Surbibor.Api.Dtos;
using Surbibor.Domain.Entities;
using Surbibor.Infrastructure.Services;

namespace Surbibor.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService, JwtTokenService jwtTokenService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var user = await authService.RegisterAsync(request.Email, request.Username, request.Password, ct);
        return Ok(ToAuthResponse(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await authService.LoginAsync(request.Email, request.Password, ct);
        return Ok(ToAuthResponse(user));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken ct)
    {
        var user = await authService.GetUserAsync(User.GetUserId(), ct);
        return Ok(ToUserResponse(user));
    }

    [HttpPost("verify-email")]
    public async Task<ActionResult<UserResponse>> VerifyEmail(VerifyEmailRequest request, CancellationToken ct)
    {
        var user = await authService.VerifyEmailAsync(request.Token, ct);
        return Ok(ToUserResponse(user));
    }

    [Authorize]
    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification(CancellationToken ct)
    {
        await authService.ResendVerificationEmailAsync(User.GetUserId(), ct);
        return NoContent();
    }

    // Always 204, whether or not the email matches an account, so callers can't probe for registered emails.
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await authService.RequestPasswordResetAsync(request.Email, ct);
        return NoContent();
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult<AuthResponse>> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await authService.ResetPasswordAsync(request.Token, request.NewPassword, request.ConfirmPassword, ct);
        return Ok(ToAuthResponse(user));
    }

    private AuthResponse ToAuthResponse(User user) => new(jwtTokenService.CreateToken(user), ToUserResponse(user));

    private static UserResponse ToUserResponse(User user) =>
        new(user.Id, user.Email, user.Username, user.EmailVerifiedAt is not null);
}
