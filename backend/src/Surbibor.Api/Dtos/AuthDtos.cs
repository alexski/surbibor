namespace Surbibor.Api.Dtos;

public record RegisterRequest(string Email, string Username, string Password);

public record LoginRequest(string Email, string Password);

public record ResetPasswordRequest(string Email, string NewPassword, string ConfirmPassword);

public record AuthResponse(string Token, UserResponse User);

public record UserResponse(Guid Id, string Email, string Username);
