namespace Surbibor.Api.Dtos;

public record RegisterRequest(string Email, string Username, string Password);

public record LoginRequest(string Email, string Password);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Token, string NewPassword, string ConfirmPassword);

public record VerifyEmailRequest(string Token);

public record AuthResponse(string Token, UserResponse User);

public record UserResponse(Guid Id, string Email, string Username, bool EmailVerified);
