using FluentAssertions;
using Sponsorship.Application.Tests.TestSupport;
using Xunit;

namespace Sponsorship.Application.Tests.Domain;

/// Covers the RefreshToken lifecycle helpers that the auth flow's rotation and
/// reuse-detection logic relies on.
public class RefreshTokenTests
{
    private static readonly DateTime Now = TestData.Now;

    [Fact]
    public void New_token_is_active_and_not_revoked()
    {
        var token = TestData.RefreshToken(expiresAt: Now.AddDays(7));

        token.IsRevoked.Should().BeFalse();
        token.IsExpired(Now).Should().BeFalse();
        token.IsActive(Now).Should().BeTrue();
    }

    [Fact]
    public void IsExpired_is_true_at_and_after_expiry_instant()
    {
        var token = TestData.RefreshToken(expiresAt: Now);

        token.IsExpired(Now.AddTicks(-1)).Should().BeFalse();
        token.IsExpired(Now).Should().BeTrue("expiry uses now >= ExpiresAt");
        token.IsExpired(Now.AddSeconds(1)).Should().BeTrue();
    }

    [Fact]
    public void Revoke_marks_token_revoked_and_inactive()
    {
        var token = TestData.RefreshToken(expiresAt: Now.AddDays(7));

        token.Revoke(Now);

        token.IsRevoked.Should().BeTrue();
        token.RevokedAt.Should().Be(Now);
        token.IsActive(Now).Should().BeFalse();
        token.ReplacedByToken.Should().BeNull();
    }

    [Fact]
    public void Revoke_with_replacement_records_successor_token()
    {
        var token = TestData.RefreshToken();

        token.Revoke(Now, "next-token");

        token.ReplacedByToken.Should().Be("next-token");
    }

    [Fact]
    public void Expired_token_is_not_active_even_when_not_revoked()
    {
        var token = TestData.RefreshToken(expiresAt: Now.AddDays(-1));

        token.IsExpired(Now).Should().BeTrue();
        token.IsActive(Now).Should().BeFalse();
    }
}
