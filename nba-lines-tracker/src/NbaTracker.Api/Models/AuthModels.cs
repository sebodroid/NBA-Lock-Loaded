namespace NbaTracker.Api.Models;

public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);

public record LoginResponse(string AccessToken, string RefreshToken, int ExpiresIn);
