using System.Reflection;
using Sponsorship.Domain.Entities;

namespace Sponsorship.Application.Tests.TestSupport;

/// Central place to build domain entities for tests with sensible defaults.
/// Keeps individual tests focused on the one field that matters to them.
public static class TestData
{
    public static readonly DateTime Now = new(2026, 5, 29, 12, 0, 0, DateTimeKind.Utc);

    public static Role Role(int id = 1, string name = Sponsorship.Domain.Entities.Role.Requestor)
        => new(id, name);

    public static User User(
        Guid? id = null,
        string email = "requestor@demo.local",
        string fullName = "Reqi Requestor",
        string? department = "Engineering",
        string passwordHash = "hash",
        int roleId = 1,
        Role? role = null,
        bool active = true)
    {
        var user = new User(id ?? Guid.NewGuid(), email, fullName, department, passwordHash, roleId, Now);
        if (role is not null) SetNavigation(user, nameof(Sponsorship.Domain.Entities.User.Role), role);
        if (!active) user.Deactivate();
        return user;
    }

    public static SponsorshipType SponsorshipType(int id = 1, string name = "Event", bool active = true)
    {
        var type = new SponsorshipType(id, name);
        if (!active) type.Deactivate();
        return type;
    }

    public static SponsorshipRequest DraftRequest(
        Guid? id = null,
        Guid? requestorId = null,
        string title = "Annual Tech Conference",
        string department = "Engineering",
        int sponsorshipTypeId = 1,
        string eventName = "DevWorld 2026",
        DateTime? eventDate = null,
        decimal requestedAmount = 5000m,
        string purpose = "Brand visibility and recruiting.",
        string? expectedBenefit = "Pipeline of senior hires.",
        string? remarks = "Priority event.",
        User? requestor = null,
        SponsorshipType? type = null)
    {
        var request = new SponsorshipRequest(
            id ?? Guid.NewGuid(),
            title,
            requestorId ?? Guid.NewGuid(),
            department,
            sponsorshipTypeId,
            eventName,
            eventDate ?? Now.Date.AddDays(30),
            requestedAmount,
            purpose,
            expectedBenefit,
            remarks,
            Now);

        if (requestor is not null)
            SetNavigation(request, nameof(SponsorshipRequest.Requestor), requestor);
        if (type is not null)
            SetNavigation(request, nameof(SponsorshipRequest.SponsorshipType), type);

        return request;
    }

    public static RefreshToken RefreshToken(
        Guid? id = null,
        Guid? userId = null,
        string token = "refresh-token-value",
        DateTime? expiresAt = null,
        DateTime? createdAt = null)
        => new(
            id ?? Guid.NewGuid(),
            userId ?? Guid.NewGuid(),
            token,
            expiresAt ?? Now.AddDays(7),
            createdAt ?? Now);

    /// Sets a navigation property whose setter is private (test-only escape hatch).
    public static void SetNavigation(object target, string propertyName, object value)
    {
        var prop = target.GetType().GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Property '{propertyName}' not found.");
        prop.SetValue(target, value);
    }
}
