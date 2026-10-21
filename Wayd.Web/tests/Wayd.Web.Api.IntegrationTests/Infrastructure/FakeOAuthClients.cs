using System.Collections.Concurrent;
using CSharpFunctionalExtensions;
using Wayd.Infrastructure.Auth.OAuth;

namespace Wayd.Web.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Stands in for the Client ID Metadata Document resolver, so the booted host never fetches over the network. A test
/// registers the clients it uses; any other <c>client_id</c> fails with the reason the real resolver gives for a
/// document it cannot retrieve. The resolver's own rules are covered by <c>ClientMetadataDocumentResolverTests</c>.
/// </summary>
public sealed class FakeOAuthClients : IOAuthClientResolver
{
    /// <summary>The reason given for a client no test registered.</summary>
    public const string UnknownClient = "The client's metadata document could not be retrieved.";

    private readonly ConcurrentDictionary<string, OAuthClient> _clients = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _resolves = new(StringComparer.Ordinal);

    /// <summary>How many times <paramref name="clientId"/> has been resolved: each time, the real resolver could fetch.</summary>
    public int ResolveCount(string clientId) => _resolves.TryGetValue(clientId, out var count) ? count : 0;

    /// <summary>Makes <paramref name="client"/> resolvable by its <see cref="OAuthClient.ClientId"/>.</summary>
    public OAuthClient Register(OAuthClient client)
    {
        _clients[client.ClientId] = client;
        return client;
    }

    /// <inheritdoc />
    public Task<Result<OAuthClient>> Resolve(string clientId, CancellationToken cancellationToken)
    {
        _resolves.AddOrUpdate(clientId, 1, (_, count) => count + 1);
        return Task.FromResult(_clients.TryGetValue(clientId, out var client)
            ? Result.Success(client)
            : Result.Failure<OAuthClient>(UnknownClient));
    }
}
