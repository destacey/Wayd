using System.Reflection;
using System.Security.Cryptography;
using Wayd.Infrastructure.DataProtection;

namespace Wayd.Infrastructure.Tests.Sut.DataProtection;

public class AesGcmSecretProtectorTests
{
    private static ISecretProtector NewProtector()
    {
        // AesGcmSecretProtector is internal; use reflection so we don't have to
        // expose it via InternalsVisibleTo just for one test class.
        var type = typeof(ISecretProtector).Assembly
            .GetType("Wayd.Infrastructure.DataProtection.AesGcmSecretProtector", throwOnError: true)!;
        var key = RandomNumberGenerator.GetBytes(32);
        return (ISecretProtector)Activator.CreateInstance(
            type,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: new object[] { key },
            culture: null)!;
    }

    [Fact]
    public void Protect_then_Unprotect_round_trips_plaintext()
    {
        var protector = NewProtector();
        var plaintext = "pat-abc123-secret";

        var protectedValue = protector.Protect(plaintext);

        protector.IsProtected(protectedValue).Should().BeTrue();
        protectedValue.Should().NotContain(plaintext);
        protector.Unprotect(protectedValue).Should().Be(plaintext);
    }

    [Fact]
    public void Protect_is_idempotent_when_input_is_already_protected()
    {
        var protector = NewProtector();
        var protectedOnce = protector.Protect("hello");

        var protectedTwice = protector.Protect(protectedOnce);

        protectedTwice.Should().Be(protectedOnce);
    }

    [Fact]
    public void IsProtected_returns_false_for_plaintext()
    {
        var protector = NewProtector();

        protector.IsProtected("plain-pat").Should().BeFalse();
        protector.IsProtected(string.Empty).Should().BeFalse();
    }

    [Fact]
    public void Protect_produces_distinct_ciphertexts_for_the_same_plaintext()
    {
        // Random nonce per call — same plaintext must encrypt to different
        // ciphertexts (otherwise an attacker can correlate identical secrets).
        var protector = NewProtector();
        var plaintext = "same-secret";

        var a = protector.Protect(plaintext);
        var b = protector.Protect(plaintext);

        a.Should().NotBe(b);
        protector.Unprotect(a).Should().Be(plaintext);
        protector.Unprotect(b).Should().Be(plaintext);
    }

    [Fact]
    public void Unprotect_throws_when_ciphertext_is_tampered()
    {
        var protector = NewProtector();
        var protectedValue = protector.Protect("tamper-target");

        // Flip a character inside the ciphertext segment. AES-GCM authentication
        // must reject this.
        var parts = protectedValue.Split(':');
        var ciphertext = parts[2];
        var flipped = (char)(ciphertext[0] ^ 0x01);
        parts[2] = flipped + ciphertext[1..];
        var tampered = string.Join(':', parts);

        Action act = () => protector.Unprotect(tampered);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Unprotect_throws_when_input_is_plaintext()
    {
        var protector = NewProtector();

        Action act = () => protector.Unprotect("not-encrypted");

        act.Should().Throw<FormatException>();
    }
}
