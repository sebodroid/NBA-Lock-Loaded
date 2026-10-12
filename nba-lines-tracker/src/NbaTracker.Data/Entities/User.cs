namespace NbaTracker.Data.Entities;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;           // login credential — unique index
    public string Username { get; set; } = null!;        // display name (optional, kept for Phase 4)
    public string PasswordHash { get; set; } = null!;    // BCrypt hash — never store plaintext
    public bool IsAdmin { get; set; }
    public bool IsApproved { get; set; }   // self-registered accounts start false; admin-created/seeded start true
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
