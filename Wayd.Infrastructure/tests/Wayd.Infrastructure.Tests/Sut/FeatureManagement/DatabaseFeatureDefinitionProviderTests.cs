using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Infrastructure.FeatureManagement;
using Wayd.Tests.Shared.Data;
using Wayd.Tests.Shared.Infrastructure;

namespace Wayd.Infrastructure.Tests.Sut.FeatureManagement;

/// <summary>
/// Asserts how long loaded flag definitions are cached, that a failed load keeps serving the last loaded flags and is
/// retried soon, and that simultaneous cache misses share one database read.
/// </summary>
public sealed class DatabaseFeatureDefinitionProviderTests : IDisposable
{
    private const string FlagName = "test-flag";

    private readonly FakeClock _clock = new();
    private readonly MemoryCache _cache;
    private readonly ServiceProvider _services;
    private readonly List<FeatureFlag> _flags = [new FeatureFlagFaker().WithName(FlagName).AsEnabled().Generate()];
    private bool _databaseUnavailable;
    private int _loadAttempts;
    private ManualResetEventSlim? _loadGate;
    private readonly ManualResetEventSlim _loadStarted = new();

    public DatabaseFeatureDefinitionProviderTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions { Clock = _clock });

        var db = new Mock<IFeatureManagementDbContext>();
        db.Setup(d => d.FeatureFlags).Returns(() => _flags.AsDbSet());

        _services = new ServiceCollection()
            .AddScoped(_ =>
            {
                Interlocked.Increment(ref _loadAttempts);
                _loadStarted.Set();
                _loadGate?.Wait(TimeSpan.FromSeconds(10));
                if (_databaseUnavailable)
                    throw new InvalidOperationException("Database unavailable.");
                return db.Object;
            })
            .BuildServiceProvider();
    }

    [Fact]
    public async Task GetFeatureDefinitionAsync_KeepsSuccessfulLoad_PastTheFailedLoadWindow()
    {
        // Arrange
        var provider = CreateProvider();
        await provider.GetFeatureDefinitionAsync(FlagName);

        // Act
        _clock.Advance(DatabaseFeatureDefinitionProvider.FailedLoadCacheExpiration + TimeSpan.FromSeconds(1));
        var definition = await provider.GetFeatureDefinitionAsync(FlagName);

        // Assert
        definition.Should().NotBeNull();
        definition!.Name.Should().Be(FlagName);
        _loadAttempts.Should().Be(1);
    }

    [Fact]
    public async Task GetFeatureDefinitionAsync_DoesNotRetryAFailedLoad_WithinTheFailedLoadWindow()
    {
        // Arrange
        var provider = CreateProvider();
        _databaseUnavailable = true;
        await provider.GetFeatureDefinitionAsync(FlagName);

        // Act
        _clock.Advance(DatabaseFeatureDefinitionProvider.FailedLoadCacheExpiration - TimeSpan.FromSeconds(1));
        var definition = await provider.GetFeatureDefinitionAsync(FlagName);

        // Assert
        definition.Should().BeNull();
        _loadAttempts.Should().Be(1);
    }

    [Fact]
    public async Task GetFeatureDefinitionAsync_RecoversFromAFailedLoad_OnceTheFailedLoadWindowPasses()
    {
        // Arrange
        var provider = CreateProvider();
        _databaseUnavailable = true;
        var duringOutage = await provider.GetFeatureDefinitionAsync(FlagName);
        _databaseUnavailable = false;

        // Act
        _clock.Advance(DatabaseFeatureDefinitionProvider.FailedLoadCacheExpiration + TimeSpan.FromSeconds(1));
        var afterRecovery = await provider.GetFeatureDefinitionAsync(FlagName);

        // Assert
        duringOutage.Should().BeNull();
        afterRecovery.Should().NotBeNull();
        afterRecovery!.Name.Should().Be(FlagName);
        _loadAttempts.Should().Be(2);
    }

    [Fact]
    public async Task GetFeatureDefinitionAsync_ServesTheLastLoadedFlags_WhenAReloadFails()
    {
        // Arrange
        var provider = CreateProvider();
        await provider.GetFeatureDefinitionAsync(FlagName);
        _clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        _databaseUnavailable = true;

        // Act
        var duringOutage = await provider.GetFeatureDefinitionAsync(FlagName);
        _clock.Advance(DatabaseFeatureDefinitionProvider.FailedLoadCacheExpiration - TimeSpan.FromSeconds(1));
        var laterInOutage = await provider.GetFeatureDefinitionAsync(FlagName);

        // Assert
        duringOutage.Should().NotBeNull();
        duringOutage!.Name.Should().Be(FlagName);
        duringOutage.EnabledFor.Should().ContainSingle(f => f.Name == "AlwaysOn");
        laterInOutage.Should().BeSameAs(duringOutage);
        _loadAttempts.Should().Be(2);
    }

    [Fact]
    public async Task GetFeatureDefinitionAsync_ReadsTheDatabaseOnce_ForSimultaneousCacheMisses()
    {
        // Arrange
        var provider = CreateProvider();
        _loadGate = new ManualResetEventSlim();
        var first = Task.Run(() => provider.GetFeatureDefinitionAsync(FlagName), TestContext.Current.CancellationToken);
        _loadStarted.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).Should().BeTrue();

        // Act
        var others = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() => provider.GetFeatureDefinitionAsync(FlagName), TestContext.Current.CancellationToken))
            .ToList();
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        _loadGate.Set();
        var definitions = await Task.WhenAll(others.Prepend(first));

        // Assert
        definitions.Should().AllSatisfy(d => d!.Name.Should().Be(FlagName));
        _loadAttempts.Should().Be(1);
    }

    public void Dispose()
    {
        _loadGate?.Dispose();
        _loadStarted.Dispose();
        _cache.Dispose();
        _services.Dispose();
    }

    private DatabaseFeatureDefinitionProvider CreateProvider() => new(
        _services.GetRequiredService<IServiceScopeFactory>(),
        _cache,
        NullLogger<DatabaseFeatureDefinitionProvider>.Instance);

    /// <summary>A clock the cache reads expiry from, moved forward by the test.</summary>
    private sealed class FakeClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; private set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public void Advance(TimeSpan by) => UtcNow += by;
    }
}
