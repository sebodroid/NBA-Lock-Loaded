namespace NbaTracker.Api.Models;

public record CreateUserRequest(string Email, string Password, bool IsAdmin = false);
