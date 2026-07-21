using System.Security.Cryptography;
using System.Text;

namespace Wayd.Integrations.AzureDevOps.Utils;

/// <summary>
/// Generates deterministic cache keys for Azure DevOps resources.
/// </summary>
internal static class CacheKeyGenerator
{
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
        // Normalize the organization URL to authority + path (avoid duplicate keys for URL variants).
        // The path segment MUST be included: on dev.azure.com the organization lives in the path
        // (https://dev.azure.com/{org}), so a host-only key would collide across organizations.
        var orgKey = Uri.TryCreate(organizationUrl, UriKind.Absolute, out var uri)
            ? $"{uri.Authority}{uri.AbsolutePath.TrimEnd('/')}".ToLowerInvariant()
            : organizationUrl.Trim().TrimEnd('/').ToLowerInvariant();

        // Deterministic teamSettings representation
        var teamPart = teamSettings is null || teamSettings.Count == 0
            ? "no-teams"
            : string.Join("|", teamSettings.OrderBy(kvp => kvp.Key)
                                          .Select(kvp => $"{kvp.Key}:{(kvp.Value.HasValue ? kvp.Value.Value.ToString("D") : "null")}"));

        // Compact fingerprint for teamSettings + extra params
        var fingerprintSource = teamPart + (extra is null ? string.Empty : "|" + extra);
        var fingerprint = ComputeSha256Hex(fingerprintSource);

        return $"{resourceType}::{orgKey}::{projectIdOrName}::{fingerprint}";
    }

    /// <summary>
    /// Computes a SHA256 hash and returns it as a lowercase hexadecimal string.
    /// </summary>
    private static string ComputeSha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
