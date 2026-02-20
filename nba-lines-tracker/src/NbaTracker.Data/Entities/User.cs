namespace NbaTracker.Data.Entities;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;           // login credential — unique index
    public string Username { get; set; } = null!;        // display name (optional, kept for Phase 4)
    public string PasswordHash { get; set; } = null!;    // BCrypt hash — never store plaintext
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
