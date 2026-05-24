using Sponsorship.Domain.Common;

namespace Sponsorship.Domain.Entities;

public class RefreshToken : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public User User { get; private set; } = default!;
    public string Token { get; private set; } = default!;
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? ReplacedByToken { get; private set; }

    public bool IsExpired(DateTime now) => now >= ExpiresAt;
    public bool IsRevoked => RevokedAt is not null;
    public bool IsActive(DateTime now) => !IsRevoked && !IsExpired(now);

    private RefreshToken() { }

    public RefreshToken(Guid id, Guid userId, string token, DateTime expiresAt, DateTime createdAt)
    {
        Id = id;
        UserId = userId;
        Token = token;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
    }

    public void Revoke(DateTime now, string? replacedByToken = null)
    {
        RevokedAt = now;
        ReplacedByToken = replacedByToken;
    }
}
