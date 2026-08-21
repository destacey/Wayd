using System.Text.Json.Serialization;
using Wayd.Common.Extensions;
using Wayd.Integrations.AzureDevOps.Models.Contracts;

namespace Wayd.Integrations.AzureDevOps.Models;

internal sealed record UserResponse
{
    /// <summary>The AzDO identity GUID. Stable across email, display name, and domain changes.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Usually the work email, but AzDO also reports non-address forms for service accounts and
    /// AAD groups, so it is treated as a hint and a display fallback rather than an identity.
    /// </summary>
    [JsonPropertyName("uniqueName")]
    public string? UniqueName { get; set; }

    [JsonPropertyName("descriptor")]
    public string? Descriptor { get; set; }

    /// <summary>
    /// Projects onto the connector-neutral contract. Returns null when AzDO supplied no identity
    /// id — without one there is nothing stable to map against, and inventing a key from the
    /// address would recreate the problem identity mapping exists to solve.
    /// </summary>
    public AzdoUserRef? ToUserRef()
    {
        if (string.IsNullOrWhiteSpace(Id))
            return null;

        var uniqueName = UniqueName?.Trim();

        return new AzdoUserRef
        {
            ExternalId = Id.Trim(),
            // uniqueName is only an address when it looks like one; service accounts report
            // things like "Build\<guid>" that must not be matched against employee emails.
            Email = uniqueName is not null && uniqueName.Contains('@', StringComparison.Ordinal) ? uniqueName : null,
            DisplayName = DisplayName?.Trim().NullIfWhiteSpacePlusTrim(),
            Handle = uniqueName.NullIfWhiteSpacePlusTrim(),
        };
    }
}
