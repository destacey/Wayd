using System.Reflection;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Wayd.Infrastructure.DataProtection;
using Wayd.Infrastructure.Persistence.Extensions;

namespace Wayd.Infrastructure.Tests.Sut.Persistence.Extensions;

/// <summary>
/// Regression tests for the JSON-conversion ValueComparer.
///
/// EF Core's default change tracking for converted properties uses reference
/// equality, which silently drops in-place mutations to nested fields of a
/// JSON-mapped value object (e.g. healing a dangling pointer inside
/// AzureDevOpsBoardsConnection.Configuration). HasJsonConversion and
/// HasEncryptedJsonConversion must attach a ValueComparer that compares
/// by serialized JSON so structural mutations actually persist.
/// </summary>
public sealed class PropertyBuilderExtensionsTests
{
    public PropertyBuilderExtensionsTests()
    {
        // HasEncryptedJsonConversion's converter resolves the protector via the
        // process-wide accessor. Initialize it once per test class with a fresh key.
        var type = typeof(ISecretProtector).Assembly
            .GetType("Wayd.Infrastructure.DataProtection.AesGcmSecretProtector", throwOnError: true)!;
        var key = RandomNumberGenerator.GetBytes(32);
        var protector = (ISecretProtector)Activator.CreateInstance(
            type,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: new object[] { key },
            culture: null)!;

        typeof(SecretProtectorAccessor)
            .GetMethod("Set", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { protector });
    }

    private sealed class TestConfig
    {
        public string Organization { get; set; } = "";
        public List<TestItem> Items { get; set; } = new();
    }

    private sealed class TestItem
    {
        public string Name { get; set; } = "";
        public TestState? State { get; set; }
    }

    private sealed class TestState
    {
        public Guid InternalId { get; set; }
        public bool IsActive { get; set; }
    }

    private sealed class TestEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public TestConfig Configuration { get; set; } = new();
    }

    private sealed class JsonConversionContext(DbContextOptions<JsonConversionContext> options) : DbContext(options)
    {
        public DbSet<TestEntity> Entities => Set<TestEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestEntity>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Configuration).HasJsonConversion();
            });
        }
    }

    private sealed class EncryptedJsonConversionContext(DbContextOptions<EncryptedJsonConversionContext> options) : DbContext(options)
    {
        public DbSet<TestEntity> Entities => Set<TestEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestEntity>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Configuration).HasEncryptedJsonConversion();
            });
        }
    }

    private static DbContextOptions<T> InMemoryOptions<T>() where T : DbContext
        => new DbContextOptionsBuilder<T>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

    private static IProperty GetConfigurationProperty(DbContext context)
    {
        var entityType = context.Model.FindEntityType(typeof(TestEntity))!;
        return entityType.FindProperty(nameof(TestEntity.Configuration))!;
    }

    [Fact]
    public void HasJsonConversion_ValueComparer_TreatsStructurallyEqualValuesAsEqual()
    {
        using var context = new JsonConversionContext(InMemoryOptions<JsonConversionContext>());
        var comparer = GetConfigurationProperty(context).GetValueComparer()!;

        var a = new TestConfig
        {
            Organization = "WAYD",
            Items = [new TestItem { Name = "x", State = new TestState { InternalId = Guid.Parse("00000000-0000-0000-0000-000000000001"), IsActive = true } }]
        };
        var b = new TestConfig
        {
            Organization = "WAYD",
            Items = [new TestItem { Name = "x", State = new TestState { InternalId = Guid.Parse("00000000-0000-0000-0000-000000000001"), IsActive = true } }]
        };

        comparer.Equals(a, b).Should().BeTrue();
    }

    [Fact]
    public void HasJsonConversion_ValueComparer_DetectsInPlaceNestedMutation()
    {
        using var context = new JsonConversionContext(InMemoryOptions<JsonConversionContext>());
        var comparer = GetConfigurationProperty(context).GetValueComparer()!;

        var original = new TestConfig
        {
            Organization = "WAYD",
            Items = [new TestItem { Name = "x", State = new TestState { InternalId = Guid.NewGuid(), IsActive = true } }]
        };

        // Deep clone via the comparer's snapshot — this is what EF stores when the entity loads.
        var snapshot = (TestConfig)comparer.Snapshot(original)!;

        // Mutate the live object in place (mirrors ClearWorkProcessIntegrationState).
        original.Items[0].State = null;

        comparer.Equals(snapshot, original).Should().BeFalse();
    }

    [Fact]
    public void HasJsonConversion_ValueComparer_Snapshot_IsDeepClone()
    {
        using var context = new JsonConversionContext(InMemoryOptions<JsonConversionContext>());
        var comparer = GetConfigurationProperty(context).GetValueComparer()!;

        var original = new TestConfig
        {
            Organization = "WAYD",
            Items = [new TestItem { Name = "x", State = new TestState { InternalId = Guid.NewGuid(), IsActive = true } }]
        };

        var snapshot = (TestConfig)comparer.Snapshot(original)!;

        // Mutating the original must not affect the snapshot — otherwise EF can't detect the change.
        original.Items[0].State!.IsActive = false;

        snapshot.Items[0].State!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task HasEncryptedJsonConversion_DetectsInPlaceMutation_OnSaveChanges()
    {
        // End-to-end: load an entity, mutate a nested field, save. EF should detect
        // the change via the ValueComparer and persist it without the caller having
        // to set IsModified manually.
        var dbName = Guid.NewGuid().ToString();
        var entityId = Guid.NewGuid();
        var staleInternalId = Guid.NewGuid();

        var ct = TestContext.Current.CancellationToken;

        using (var setup = new EncryptedJsonConversionContext(
            new DbContextOptionsBuilder<EncryptedJsonConversionContext>().UseInMemoryDatabase(dbName).Options))
        {
            setup.Entities.Add(new TestEntity
            {
                Id = entityId,
                Configuration = new TestConfig
                {
                    Organization = "WAYD",
                    Items = [new TestItem { Name = "Agile", State = new TestState { InternalId = staleInternalId, IsActive = true } }]
                }
            });
            await setup.SaveChangesAsync(ct);
        }

        using (var mutate = new EncryptedJsonConversionContext(
            new DbContextOptionsBuilder<EncryptedJsonConversionContext>().UseInMemoryDatabase(dbName).Options))
        {
            var loaded = await mutate.Entities.FirstAsync(e => e.Id == entityId, ct);
            loaded.Configuration.Items[0].State = null;
            await mutate.SaveChangesAsync(ct);
        }

        using (var verify = new EncryptedJsonConversionContext(
            new DbContextOptionsBuilder<EncryptedJsonConversionContext>().UseInMemoryDatabase(dbName).Options))
        {
            var reloaded = await verify.Entities.FirstAsync(e => e.Id == entityId, ct);
            reloaded.Configuration.Items[0].State.Should().BeNull(
                "the ValueComparer must detect the in-place mutation so EF persists the JSON column");
        }
    }
}
