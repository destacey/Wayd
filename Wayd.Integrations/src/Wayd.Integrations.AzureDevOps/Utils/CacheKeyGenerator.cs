using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Wayd.Integrations.AzureDevOps.Utils;

/// <summary>
/// Generates cache keys for Azure DevOps resources with optimized performance.
/// Uses an internal cache to avoid repeated SHA256 computations for the same inputs.
/// </summary>
internal static class CacheKeyGenerator
{
    // Cache for the computed cache keys to avoid repeated SHA256 computations
    private static readonly ConcurrentDictionary<string, string> _cacheKeyCache = new();

    /// <summary>
    /// Generates a cache key for Azure DevOps resources.
    /// </summary>
    /// <param name="resourceType">The type of resource (e.g., "azdo-iterations")</param>
    /// <param name="organizationUrl">The Azure DevOps organization URL</param>
    /// <param name="projectIdOrName">The project ID or name</param>
    /// <param name="teamSettings">Optional team settings dictionary</param>
    /// <param name="extra">Optional extra parameters</param>
    /// <returns>A deterministic cache key that includes a SHA256 hash for team settings</returns>
    public static string GetCacheKey(
        string resourceType,
        string organizationUrl,
        string projectIdOrName,
        Dictionary<Guid, Guid?>? teamSettings,
        string? extra = null)
    {
        // Normalize organization host (avoid duplicate keys for URL variants)
        var orgHost = Uri.TryCreate(organizationUrl, UriKind.Absolute, out var uri)
            ? uri.Host.ToLowerInvariant()
            : organizationUrl.ToLowerInvariant();

        // Deterministic teamSettings representation
        var teamPart = teamSettings is null || teamSettings.Count == 0
            ? "no-teams"
            : string.Join("|", teamSettings.OrderBy(kvp => kvp.Key)
                                          .Select(kvp => $"{kvp.Key}:{(kvp.Value.HasValue ? kvp.Value.Value.ToString("D") : "null")}"));

        // Build a simple key for the cache key lookup to avoid recomputing the same hash
        var lookupKey = $"{resourceType}::{orgHost}::{projectIdOrName}::{teamPart}{(extra is null ? string.Empty : "::" + extra)}";

        // Use GetOrAdd to avoid repeated SHA256 computations for the same inputs
        return _cacheKeyCache.GetOrAdd(lookupKey, key =>
        {
            // Build a compact fingerprint for teamSettings + extra params
            var fingerprintSource = teamPart + (extra is null ? string.Empty : "|" + extra);
            var fingerprint = ComputeSha256Hex(fingerprintSource);

            return $"{resourceType}::{orgHost}::{projectIdOrName}::{fingerprint}";
        });
    }

    /// <summary>
    /// Computes a SHA256 hash and returns it as a lowercase hexadecimal string.
    /// </summary>
    private static string ComputeSha256Hex(string input)
    {
        // Encode once, compute hash via the static API
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Clears the internal cache. Useful for testing or to free memory.
    /// </summary>
    internal static void ClearCache()
    {
        _cacheKeyCache.Clear();
    }
}
