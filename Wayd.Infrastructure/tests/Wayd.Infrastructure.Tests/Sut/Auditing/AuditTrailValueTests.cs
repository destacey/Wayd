using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.DataProtection;
using Wayd.Infrastructure.Auditing;
using Wayd.Infrastructure.DataProtection;
using Wayd.Infrastructure.Persistence.Extensions;
using Wayd.Infrastructure.Tests.Infrastructure;

namespace Wayd.Infrastructure.Tests.Sut.Auditing;

/// <summary>
/// Proves the property builder extensions mark how the audit trail records a property, and that the trail reads a
/// marked property back in that form.
/// </summary>
[Collection(SecretProtectorCollection.Name)]
public sealed class AuditTrailValueTests
{
    public AuditTrailValueTests()
    {
        // The encrypting converter resolves the protector through the process-wide accessor. The collection above
        // keeps this class from running alongside others that swap it mid-test.
        SecretProtectorAccessor.Set(new AesGcmSecretProtector(RandomNumberGenerator.GetBytes(32)));
    }

    private sealed class Secrets
    {
        [Encrypted]
        public string ApiKey { get; set; } = "";

        public string Endpoint { get; set; } = "";
    }

    private sealed class Probe
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public string Reason { get; set; } = "";
        public Secrets Configuration { get; set; } = new();
    }

    private sealed class ProbeContext(DbContextOptions<ProbeContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Probe>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Reason).ExcludeFromAuditTrail();
                b.Property(e => e.Configuration).HasEncryptedJsonConversion();
            });
        }
    }

    private static ProbeContext CreateContext() => new(new DbContextOptionsBuilder<ProbeContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    [Fact]
    public void TryGet_UnmarkedProperty_ReturnsTheValueAsTheApplicationSeesIt()
    {
        // Arrange
        using var context = CreateContext();
        var entry = context.Add(new Probe { Name = "Apollo" });

        // Act
        var recorded = AuditTrailValues.TryGet(entry.Property(p => p.Name), current: true, out var value);

        // Assert
        recorded.Should().BeTrue();
        value.Should().Be("Apollo");
    }

    [Fact]
    public void TryGet_ExcludedProperty_RecordsNothing()
    {
        // Arrange
        using var context = CreateContext();
        var entry = context.Add(new Probe { Reason = "sick leave" });

        // Act
        var recorded = AuditTrailValues.TryGet(entry.Property(p => p.Reason), current: true, out var value);

        // Assert
        recorded.Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void TryGet_EncryptedJsonProperty_ReturnsTheStoredFormWithTheSecretEncrypted()
    {
        // Arrange
        using var context = CreateContext();
        var entry = context.Add(new Probe { Configuration = new Secrets { ApiKey = "plain-key", Endpoint = "https://acme.example" } });

        // Act
        var recorded = AuditTrailValues.TryGet(entry.Property(p => p.Configuration), current: true, out var value);

        // Assert
        recorded.Should().BeTrue();
        var stored = value.Should().BeOfType<JsonElement>().Subject;
        stored.GetProperty("Endpoint").GetString().Should().Be("https://acme.example");
        var apiKey = stored.GetProperty("ApiKey").GetString()!;
        SecretProtectorAccessor.Current.IsProtected(apiKey).Should().BeTrue();
        SecretProtectorAccessor.Current.Unprotect(apiKey).Should().Be("plain-key");
    }
}
