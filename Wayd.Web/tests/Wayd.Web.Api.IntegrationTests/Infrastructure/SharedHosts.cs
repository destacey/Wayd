using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Wayd.Web.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts derived from a collection's factory with settings it does not have, one per distinct set of settings and
/// shared by every test that asks for the same set.
/// </summary>
/// <remarks>
/// Booting a host takes seconds, so one host per set of settings is shared rather than one booted per test. A shared
/// host keeps whatever a test leaves in it, so a test must not depend on arriving first: a rate-limit test spends a
/// partition no other test uses, with a new user or <see cref="ClientAddresses"/>. A host that fails to start fails
/// every test that asks for the same settings.
/// </remarks>
internal static class SharedHosts
{
    private static readonly ConditionalWeakTable<WebApplicationFactory<Program>, ConcurrentDictionary<string, Lazy<WebApplicationFactory<Program>>>> Hosts = new();

    /// <summary>
    /// A host of <paramref name="factory"/>'s with <paramref name="settings"/> layered over its configuration, booted the
    /// first time any test asks for that set. Never dispose it: <paramref name="factory"/> disposes it, with every other
    /// host derived from it.
    /// </summary>
    /// <param name="factory">The collection's factory, whose database and test services the host keeps.</param>
    /// <param name="settings">Configuration keys and values; their order does not matter.</param>
    public static WebApplicationFactory<Program> HostWith(this WebApplicationFactory<Program> factory, params (string Key, string? Value)[] settings)
    {
        var key = string.Concat(settings.OrderBy(s => s.Key, StringComparer.Ordinal).Select(s => $"{Part(s.Key)}{Part(s.Value)}"));

        return Hosts.GetValue(factory, _ => new(StringComparer.Ordinal))
            .GetOrAdd(key, _ => new Lazy<WebApplicationFactory<Program>>(() => factory.WithWebHostBuilder(builder =>
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                    settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))))))
            .Value;
    }

    // Length-prefixed, so no key or value can run into the next, and null kept apart from empty: configuration binds an
    // empty string but treats null as unset.
    private static string Part(string? value) => value is null ? "-;" : $"{value.Length}:{value};";
}
