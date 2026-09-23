using Microsoft.AspNetCore.Mvc;
using Surbibor.Api.Auth;
using Surbibor.Api.Dtos;
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
        var token = jwtTokenService.CreateToken(user);

        return Ok(new AuthResponse(token, new UserResponse(user.Id, user.Email, user.Username)));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await authService.LoginAsync(request.Email, request.Password, ct);
        var token = jwtTokenService.CreateToken(user);

        return Ok(new AuthResponse(token, new UserResponse(user.Id, user.Email, user.Username)));
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult<AuthResponse>> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await authService.ResetPasswordAsync(request.Email, request.NewPassword, request.ConfirmPassword, ct);
        var token = jwtTokenService.CreateToken(user);

        return Ok(new AuthResponse(token, new UserResponse(user.Id, user.Email, user.Username)));
    }
}
