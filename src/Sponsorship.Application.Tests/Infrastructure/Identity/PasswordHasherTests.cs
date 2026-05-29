using FluentAssertions;
using Sponsorship.Infrastructure.Identity;
using Xunit;

namespace Sponsorship.Application.Tests.Infrastructure.Identity;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Hash_does_not_return_the_plaintext()
    {
        var hash = _hasher.Hash("Demo@123");

        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().NotBe("Demo@123");
    }

    [Fact]
    public void Verify_returns_true_for_correct_password()
    {
        var hash = _hasher.Hash("Demo@123");

        _hasher.Verify("Demo@123", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_returns_false_for_wrong_password()
    {
        var hash = _hasher.Hash("Demo@123");

        _hasher.Verify("wrong-password", hash).Should().BeFalse();
    }

    [Fact]
    public void Hashing_same_password_twice_yields_different_salted_hashes()
    {
        var first = _hasher.Hash("Demo@123");
        var second = _hasher.Hash("Demo@123");

        first.Should().NotBe(second, "BCrypt salts each hash");
        _hasher.Verify("Demo@123", first).Should().BeTrue();
        _hasher.Verify("Demo@123", second).Should().BeTrue();
    }
}
