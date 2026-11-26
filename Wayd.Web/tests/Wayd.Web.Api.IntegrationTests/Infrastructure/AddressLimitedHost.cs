using System.Globalization;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Wayd.Web.Api.IntegrationTests.Infrastructure;

/// <summary>
/// The host on which every limit keyed by caller address allows <see cref="Limit"/> requests an hour: the anonymous
/// sign-in endpoints and the OAuth authorization server. The collection's own host raises them all so the suite is not
/// throttled, so a test of one of these limits uses this host, from an address of its own
/// (<see cref="ClientAddresses.CreateClientFromNewAddress"/>).
/// </summary>
/// <remarks>
/// One host for all of them, shared across test classes: each policy keeps its own windows, so lowering the rest
/// changes nothing for the limit a test exercises.
/// </remarks>
internal static class AddressLimitedHost
{
    /// <summary>How many requests each address-keyed limit allows in an hour.</summary>
    public const int Limit = 2;

    private static readonly string[] Policies = ["AuthSignIn", "AuthRefresh", "AuthProviders", "AuthSetup", "OAuthAuthorize", "OAuthToken"];

    /// <summary>
    /// The address-limited host derived from <paramref name="factory"/>; never dispose it. Send from
    /// <see cref="ClientAddresses.CreateClientFromNewAddress"/>: a plain client shares one address with every other, so
    /// two tests using one would throttle each other.
    /// </summary>
    public static WebApplicationFactory<Program> AddressLimited(this WaydSqlServerApiFactory factory) =>
        factory.HostWith([.. Policies.SelectMany(policy => new (string, string?)[]
        {
            ($"RateLimitSettings:{policy}:PermitLimit", Limit.ToString(CultureInfo.InvariantCulture)),
            ($"RateLimitSettings:{policy}:WindowSeconds", "3600"),
        })]);
}
