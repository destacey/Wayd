using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Wayd.Infrastructure.Auth.OAuth;

internal static class ConfigureServices
{
    /// <summary>The longest a client's metadata document may take to arrive, connection included.</summary>
    private static readonly TimeSpan _fetchTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Registers how Wayd's authorization server resolves its clients. The resolver gets an
    /// <see cref="HttpClient"/> of its own rather than one from the factory, whose defaults add service discovery
    /// (which would resolve a client-chosen host name to an internal service) and retries.
    /// </summary>
    internal static IServiceCollection AddOAuthClients(this IServiceCollection services)
    {
        // The resolver's own deadline bounds the whole fetch, body included; HttpClient.Timeout would stop at the headers.
        services.AddSingleton<IOAuthClientResolver>(sp => new ClientMetadataDocumentResolver(
            new HttpClient(PublicNetwork.CreateHandler(_fetchTimeout)) { Timeout = Timeout.InfiniteTimeSpan },
            _fetchTimeout,
            sp.GetRequiredService<ILogger<ClientMetadataDocumentResolver>>()));

        return services;
    }
}
