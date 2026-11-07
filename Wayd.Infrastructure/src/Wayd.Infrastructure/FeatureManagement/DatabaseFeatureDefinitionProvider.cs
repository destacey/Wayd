using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Domain.FeatureManagement;

namespace Wayd.Infrastructure.FeatureManagement;

/// <summary>
/// Supplies feature flag definitions from the database to Microsoft.FeatureManagement, cached in memory so a flag
/// check does not query the database.
/// </summary>
internal sealed class DatabaseFeatureDefinitionProvider(
    IServiceScopeFactory scopeFactory,
    IMemoryCache cache,
    ILogger<DatabaseFeatureDefinitionProvider> logger) : IFeatureDefinitionProvider, IFeatureFlagCacheInvalidator
{
    private const string CacheKey = "FeatureManagement:AllDefinitions";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long the fallback served after a failed load is cached. Short, so current flags come back soon after the
    /// database does; non-zero, so an outage does not send every flag check to the database.
    /// </summary>
    internal static readonly TimeSpan FailedLoadCacheExpiration = TimeSpan.FromSeconds(15);

    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IMemoryCache _cache = cache;
    private readonly ILogger<DatabaseFeatureDefinitionProvider> _logger = logger;

    /// <summary>Lets one request reload on a cache miss while the rest wait for its result.</summary>
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    /// <summary>The last set read from the database, served while the database cannot be read.</summary>
    private volatile Dictionary<string, FeatureDefinition>? _lastLoaded;

    public async Task<FeatureDefinition?> GetFeatureDefinitionAsync(string featureName)
    {
        var definitions = await GetCachedDefinitions();
        definitions.TryGetValue(featureName, out var definition);
        return definition;
    }

    public async IAsyncEnumerable<FeatureDefinition> GetAllFeatureDefinitionsAsync()
    {
        var definitions = await GetCachedDefinitions();
        foreach (var definition in definitions.Values)
        {
            yield return definition;
        }
    }

    public void InvalidateCache()
    {
        _cache.Remove(CacheKey);
        _logger.LogDebug("Feature flag cache invalidated.");
    }

    private async Task<Dictionary<string, FeatureDefinition>> GetCachedDefinitions()
    {
        if (TryGetCached(out var cached))
            return cached;

        await _loadLock.WaitAsync();
        try
        {
            if (TryGetCached(out cached))
                return cached;

            var definitions = await LoadDefinitionsFromDatabase();
            if (definitions is not null)
            {
                _lastLoaded = definitions;
                Cache(definitions, CacheExpiration);
                return definitions;
            }

            var fallback = _lastLoaded ?? new Dictionary<string, FeatureDefinition>(StringComparer.OrdinalIgnoreCase);
            Cache(fallback, FailedLoadCacheExpiration);
            return fallback;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private bool TryGetCached(out Dictionary<string, FeatureDefinition> definitions) =>
        _cache.TryGetValue(CacheKey, out definitions!) && definitions is not null;

    private void Cache(Dictionary<string, FeatureDefinition> definitions, TimeSpan expiration) =>
        _cache.Set(CacheKey, definitions, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiration });

    /// <summary>Loads the non-archived flags, or returns <see langword="null"/> when the database cannot be read.</summary>
    private async Task<Dictionary<string, FeatureDefinition>?> LoadDefinitionsFromDatabase()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IFeatureManagementDbContext>();

            var flags = await dbContext.FeatureFlags
                .AsNoTracking()
                .Where(f => !f.IsArchived)
                .ToListAsync();

            var definitions = new Dictionary<string, FeatureDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var flag in flags)
            {
                definitions[flag.Name] = ToFeatureDefinition(flag);
            }

            _logger.LogDebug("Loaded {Count} feature flag definitions from database.", definitions.Count);
            return definitions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load feature flag definitions from database. Serving the last loaded flags, or every flag as off if none have loaded, until the next attempt.");
            return null;
        }
    }

    private static FeatureDefinition ToFeatureDefinition(FeatureFlag flag)
    {
        var definition = new FeatureDefinition
        {
            Name = flag.Name,
            Telemetry = new TelemetryConfiguration { Enabled = true }
        };

        if (flag.IsEnabled)
        {
            definition.EnabledFor =
            [
                new FeatureFilterConfiguration { Name = "AlwaysOn" }
            ];
        }
        else
        {
            definition.EnabledFor = [];
        }

        return definition;
    }
}
