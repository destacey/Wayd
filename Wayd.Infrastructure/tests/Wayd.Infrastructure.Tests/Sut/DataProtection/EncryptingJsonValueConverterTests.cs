using System.Reflection;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Wayd.Common.Domain.DataProtection;
using Wayd.Infrastructure.DataProtection;

namespace Wayd.Infrastructure.Tests.Sut.DataProtection;

public class EncryptingJsonValueConverterTests
{
    public EncryptingJsonValueConverterTests()
    {
        // The converter resolves the protector via the process-wide accessor.
        // Initialize it once per test class with a fresh key.
        var type = typeof(ISecretProtector).Assembly
            .GetType("Wayd.Infrastructure.DataProtection.AesGcmSecretProtector", throwOnError: true)!;
        var key = RandomNumberGenerator.GetBytes(32);
        var protector = (ISecretProtector)Activator.CreateInstance(
            type,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: new object[] { key },
            culture: null)!;

        // Use reflection — Set is internal.
        typeof(SecretProtectorAccessor)
            .GetMethod("Set", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { protector });
    }

    private sealed class TestConfig
    {
        public string Organization { get; set; } = "";
        [Encrypted] public string PersonalAccessToken { get; set; } = "";
        public List<TestNested> Items { get; set; } = new();
    }

    private sealed class TestNested
    {
        public string Name { get; set; } = "";
        [Encrypted] public string ApiKey { get; set; } = "";
    }

    [Fact]
    public void Round_trips_simple_secret_property()
    {
        var converter = new EncryptingJsonValueConverter<TestConfig>();
        var original = new TestConfig { Organization = "acme", PersonalAccessToken = "pat-123" };

        var stored = ((ValueConverter<TestConfig, string>)converter).ConvertToProvider(original) as string;
        stored.Should().NotBeNull();
        stored!.Should().NotContain("pat-123");
        stored.Should().Contain("acme"); // non-encrypted fields stay readable

        var restored = ((ValueConverter<TestConfig, string>)converter).ConvertFromProvider(stored) as TestConfig;
        restored.Should().NotBeNull();
        restored!.Organization.Should().Be("acme");
        restored.PersonalAccessToken.Should().Be("pat-123");
    }

    [Fact]
    public void Round_trips_secrets_nested_inside_collections()
    {
        var converter = new EncryptingJsonValueConverter<TestConfig>();
        var original = new TestConfig
        {
            Organization = "acme",
            PersonalAccessToken = "pat-outer",
            Items = new List<TestNested>
            {
                new() { Name = "a", ApiKey = "key-a" },
                new() { Name = "b", ApiKey = "key-b" },
            },
        };

        var stored = ((ValueConverter<TestConfig, string>)converter).ConvertToProvider(original) as string;
        stored!.Should().NotContain("pat-outer");
        stored.Should().NotContain("key-a");
        stored.Should().NotContain("key-b");

        var restored = ((ValueConverter<TestConfig, string>)converter).ConvertFromProvider(stored) as TestConfig;
        restored!.Items[0].ApiKey.Should().Be("key-a");
        restored.Items[1].ApiKey.Should().Be("key-b");
        restored.Items[0].Name.Should().Be("a");
    }

    [Fact]
    public void Write_path_does_not_mutate_the_input_object()
    {
        // Regression: an earlier version walked the input object and
        // overwrote [Encrypted] properties in place, leaving the tracked
        // EF entity holding ciphertext after SaveChanges. The converter
        // must encrypt a clone instead.
        var converter = new EncryptingJsonValueConverter<TestConfig>();
        var original = new TestConfig
        {
            Organization = "acme",
            PersonalAccessToken = "pat-untouched",
            Items = new List<TestNested>
            {
                new() { Name = "a", ApiKey = "key-untouched-a" },
                new() { Name = "b", ApiKey = "key-untouched-b" },
            },
        };

        var stored = ((ValueConverter<TestConfig, string>)converter).ConvertToProvider(original) as string;
        stored.Should().NotBeNull();
        stored!.Should().NotContain("pat-untouched");

        original.PersonalAccessToken.Should().Be("pat-untouched");
        original.Items[0].ApiKey.Should().Be("key-untouched-a");
        original.Items[1].ApiKey.Should().Be("key-untouched-b");
    }

    [Fact]
    public void Decrypts_legacy_plaintext_values_unchanged()
    {
        // Simulate a row written before encryption shipped: JSON with a plaintext PAT.
        var legacyJson = System.Text.Json.JsonSerializer.Serialize(new TestConfig
        {
            Organization = "acme",
            PersonalAccessToken = "legacy-plaintext-pat",
        });

        var converter = new EncryptingJsonValueConverter<TestConfig>();
        var restored = ((ValueConverter<TestConfig, string>)converter).ConvertFromProvider(legacyJson) as TestConfig;

        restored!.PersonalAccessToken.Should().Be("legacy-plaintext-pat");
    }
}
