namespace NbaTracker.Api.Models;

public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record RegisterRequest(string Email, string Username, string Password);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Token, string NewPassword);

public record LoginResponse(string AccessToken, string RefreshToken, int ExpiresIn, bool IsAdmin);

public record PendingUserResponse(int Id, string Email, string Username, DateTime CreatedAt);
