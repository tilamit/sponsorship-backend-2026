using Sponsorship.Domain.Common;

namespace Sponsorship.Domain.Entities;

public class User : Entity<Guid>
{
    public string Email { get; private set; } = default!;
    public string FullName { get; private set; } = default!;
    public string? Department { get; private set; }
    public string PasswordHash { get; private set; } = default!;
    public int RoleId { get; private set; }
    public Role Role { get; private set; } = default!;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private User() { }

    public User(Guid id, string email, string fullName, string? department,
        string passwordHash, int roleId, DateTime createdAt)
    {
        Id = id;
        Email = email;
        FullName = fullName;
        Department = department;
        PasswordHash = passwordHash;
        RoleId = roleId;
        IsActive = true;
        CreatedAt = createdAt;
    }

    public void Deactivate() => IsActive = false;
}
