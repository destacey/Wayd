using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Wayd.Infrastructure.Auth.OAuth;

/// <summary>
/// Resolves a <c>client_id</c> by fetching the Client ID Metadata Document it is the URL of
/// (draft-ietf-oauth-client-id-metadata-document), which is how MCP clients register with an authorization server
/// they have never met.
/// </summary>
/// <remarks>
/// The URL is chosen by whoever starts an authorization request, so the fetch goes out through
/// <see cref="PublicNetwork"/> only and is small and short. Every outcome is cached, a refusal briefly, and
/// concurrent requests for one client share one fetch, so repeating a request cannot make Wayd repeat the fetch
/// against a host of the requester's choosing.
/// </remarks>
internal sealed class ClientMetadataDocumentResolver : IOAuthClientResolver
{
    /// <summary>The largest document read; the draft suggests authorization servers cap it at about this size.</summary>
    internal const int MaxDocumentBytes = 5 * 1024;

    /// <summary>
    /// The most clients held in the cache. Anyone can name a new URL, so the cache is capped rather than left to
    /// grow; past the cap an entry is evicted and the next request for it fetches again.
    /// </summary>
    internal const int MaxCachedClients = 1000;

    /// <summary>How long a document is cached when its response does not say.</summary>
    internal static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromHours(1);

    /// <summary>The shortest a document is cached, so a <c>max-age</c> of zero cannot make every request a fetch.</summary>
    internal static readonly TimeSpan MinCacheDuration = TimeSpan.FromMinutes(5);

    /// <summary>The longest a document is cached, so a client's changed redirect URIs take effect within a day.</summary>
    internal static readonly TimeSpan MaxCacheDuration = TimeSpan.FromHours(24);

    /// <summary>How long a refusal is cached: long enough that retrying a bad client cannot be used to make Wayd fetch, short enough that a fixed document is soon accepted.</summary>
    internal static readonly TimeSpan FailureCacheDuration = TimeSpan.FromMinutes(1);

    /// <summary>The reason given when the document cannot be read at all, which says nothing about why.</summary>
    private const string Unavailable = "The client's metadata document could not be retrieved.";

    /// <summary>The reason given for a document over <see cref="MaxDocumentBytes"/>.</summary>
    private const string TooLarge = "The client's metadata document is larger than Wayd accepts.";

    /// <summary>
    /// Schemes a redirect URI may never use: the browser would run, read or hand off the target rather than open an
    /// app with it, or the scheme carries no app at all.
    /// </summary>
    private static readonly HashSet<string> _forbiddenRedirectSchemes = new(
        ["javascript", "data", "file", "vbscript", "blob", "about", "view-source", "intent", "ftp", "ws", "wss"],
        StringComparer.OrdinalIgnoreCase);

    private readonly HttpClient _httpClient;
    private readonly TimeSpan _fetchTimeout;
    private readonly ILogger<ClientMetadataDocumentResolver> _logger;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = MaxCachedClients });
    private readonly ConcurrentDictionary<string, Lazy<Task<Result<OAuthClient>>>> _inFlight = new(StringComparer.Ordinal);

    /// <summary>Creates a resolver that fetches through <paramref name="httpClient"/>, giving each fetch <paramref name="fetchTimeout"/> in all.</summary>
    /// <param name="httpClient">A client whose handler reaches only public addresses (<see cref="PublicNetwork.CreateHandler"/>).</param>
    /// <param name="fetchTimeout">The whole fetch's budget, connection, headers and body together.</param>
    /// <param name="logger">Records why a client's document was refused or could not be fetched.</param>
    public ClientMetadataDocumentResolver(HttpClient httpClient, TimeSpan fetchTimeout, ILogger<ClientMetadataDocumentResolver> logger)
    {
        _httpClient = httpClient;
        _fetchTimeout = fetchTimeout;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<OAuthClient>> Resolve(string clientId, CancellationToken cancellationToken)
    {
        var url = ParseClientId(clientId);
        if (url.IsFailure)
            return Result.Failure<OAuthClient>(url.Error);

        if (_cache.TryGetValue(clientId, out Result<OAuthClient> cached))
            return cached;

        // The fetch runs without the caller's token, so one caller giving up does not fail the others sharing it;
        // the fetch's own deadline bounds it.
        var shared = _inFlight.GetOrAdd(clientId, id => new Lazy<Task<Result<OAuthClient>>>(() => FetchAndCache(id, url.Value)));
        return await shared.Value.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Checks that <paramref name="clientId"/> is a URL the draft allows to identify a client: HTTPS, with a path,
    /// and no credentials, fragment, whitespace, or dot segments that would let one URL stand for another.
    /// </summary>
    internal static Result<Uri> ParseClientId(string? clientId)
    {
        const string Invalid = "client_id must be the HTTPS URL of the client's metadata document.";

        // A backslash is read as a slash, so it could hide a dot segment from the check below.
        if (string.IsNullOrWhiteSpace(clientId)
            || clientId.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c == '\\')
            || !Uri.TryCreate(clientId, UriKind.Absolute, out var url)
            || url.Scheme != Uri.UriSchemeHttps
            || url.UserInfo.Length > 0
            || clientId.Contains('#')
            || url.AbsolutePath == "/")
        {
            return Result.Failure<Uri>(Invalid);
        }

        // Read from the original string: Uri has already collapsed any dot segments in AbsolutePath.
        var afterScheme = clientId["https://".Length..];
        var pathStart = afterScheme.IndexOf('/');
        var path = afterScheme[pathStart..].Split('?', 2)[0];
        if (path.Split('/').Select(Uri.UnescapeDataString).Any(segment => segment is "." or ".."))
            return Result.Failure<Uri>(Invalid);

        return url;
    }

    /// <summary>Fetches and reads the client's document, caching the outcome: a client for as long as its response allows, a refusal for <see cref="FailureCacheDuration"/>.</summary>
    private async Task<Result<OAuthClient>> FetchAndCache(string clientId, Uri url)
    {
        try
        {
            var fetched = await Fetch(url);
            var client = fetched.IsSuccess
                ? ReadDocument(clientId, url, fetched.Value.Document)
                : Result.Failure<OAuthClient>(fetched.Error);

            if (client.IsFailure)
            {
                _logger.LogWarning("OAuth client {ClientId} was refused: {Reason}", clientId, client.Error);
                Cache(clientId, client, FailureCacheDuration);
            }
            else if (fetched.Value.CacheControl?.NoStore != true)
            {
                Cache(clientId, client, CacheDuration(fetched.Value.CacheControl));
            }

            return client;
        }
        finally
        {
            // Removed here rather than by the callers, so a fetch every caller abandoned still leaves nothing behind.
            // The fetch starts only once GetOrAdd has stored its entry, so this always removes its own.
            _inFlight.TryRemove(clientId, out _);
        }
    }

    private void Cache(string clientId, Result<OAuthClient> outcome, TimeSpan duration) =>
        _cache.Set(clientId, outcome, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = duration });

    /// <summary>Fetches the document within <see cref="_fetchTimeout"/>, which covers reading the body as well as receiving the headers.</summary>
    private async Task<Result<(JsonElement Document, CacheControlHeaderValue? CacheControl)>> Fetch(Uri url)
    {
        using var deadline = new CancellationTokenSource(_fetchTimeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                _logger.LogWarning("Fetching the metadata document {Url} answered {StatusCode}.", url, (int)response.StatusCode);
                return Result.Failure<(JsonElement, CacheControlHeaderValue?)>(Unavailable);
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType is null || !(mediaType == "application/json" || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase)))
                return Result.Failure<(JsonElement, CacheControlHeaderValue?)>("The client's metadata document must be served as application/json.");

            if (response.Content.Headers.ContentLength > MaxDocumentBytes)
                return Result.Failure<(JsonElement, CacheControlHeaderValue?)>(TooLarge);

            var body = await ReadLimited(await response.Content.ReadAsStreamAsync(deadline.Token), deadline.Token);
            if (body is null)
                return Result.Failure<(JsonElement, CacheControlHeaderValue?)>(TooLarge);

            using var json = JsonDocument.Parse(body);
            return (json.RootElement.Clone(), response.Headers.CacheControl);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or OperationCanceledException)
        {
            _logger.LogWarning(ex, "Fetching the metadata document {Url} failed.", url);
            return Result.Failure<(JsonElement, CacheControlHeaderValue?)>(Unavailable);
        }
    }

    /// <summary>Reads the body, or returns null as soon as it passes <see cref="MaxDocumentBytes"/>, whatever Content-Length claimed.</summary>
    private static async Task<byte[]?> ReadLimited(Stream stream, CancellationToken cancellationToken)
    {
        await using (stream)
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[1024];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + read > MaxDocumentBytes)
                    return null;
                buffer.Write(chunk, 0, read);
            }

            return buffer.ToArray();
        }
    }

    /// <summary>
    /// Reads a client from its metadata document. The document must name itself by exactly the URL it was fetched
    /// from, and describe a public client: Wayd verifies no client credentials, so a client that expects to
    /// authenticate is refused rather than treated as one that does not.
    /// </summary>
    /// <remarks>
    /// Every reason returned is fit for an OAuth <c>error_description</c>, whose characters RFC 6749 §5.2 limits to
    /// printable ASCII other than <c>"</c> and <c>\</c>, and repeats nothing from the document.
    /// </remarks>
    internal static Result<OAuthClient> ReadDocument(string clientId, Uri url, JsonElement document)
    {
        if (document.ValueKind != JsonValueKind.Object)
            return Result.Failure<OAuthClient>("The client's metadata document must be a JSON object.");

        if (!TryGetString(document, "client_id", out var documentClientId) || !string.Equals(documentClientId, clientId, StringComparison.Ordinal))
            return Result.Failure<OAuthClient>("The client's metadata document must give its own URL as client_id.");

        if (TryGetString(document, "token_endpoint_auth_method", out var authMethod) && authMethod != "none")
            return Result.Failure<OAuthClient>("Wayd accepts only public clients: token_endpoint_auth_method must be none.");

        if (!ListsOrOmits(document, "grant_types", "authorization_code"))
            return Result.Failure<OAuthClient>("The client's grant_types must include authorization_code.");

        if (!ListsOrOmits(document, "response_types", "code"))
            return Result.Failure<OAuthClient>("The client's response_types must include code.");

        if (!document.TryGetProperty("redirect_uris", out var redirectUris)
            || redirectUris.ValueKind != JsonValueKind.Array
            || redirectUris.GetArrayLength() == 0)
        {
            return Result.Failure<OAuthClient>("The client's metadata document must list its redirect_uris.");
        }

        var uris = new List<string>();
        foreach (var item in redirectUris.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || !IsAllowedRedirectUri(item.GetString()))
                return Result.Failure<OAuthClient>("One of the client's redirect_uris is not one Wayd will redirect to.");
            uris.Add(item.GetString()!);
        }

        var name = TryGetString(document, "client_name", out var clientName) && !string.IsNullOrWhiteSpace(clientName)
            ? clientName.Trim()
            : url.Host;

        return new OAuthClient(clientId, name, uris, HttpsUrlOrNull(document, "client_uri"), HttpsUrlOrNull(document, "logo_uri"));
    }

    /// <summary>
    /// Whether an authorization response may be sent to <paramref name="value"/>: an absolute URI with no fragment,
    /// over HTTPS, over HTTP to the loopback interface (RFC 8252 §7.3), or to an app's own scheme (§7.1).
    /// </summary>
    /// <remarks>
    /// An app's scheme is allowed without the reverse-domain form §7.1 recommends, because MCP clients in use
    /// register schemes like <c>cursor:</c> and <c>claude:</c>; the schemes that could not be an app are refused
    /// instead, as are Windows' <c>ms-</c> system handlers.
    /// </remarks>
    internal static bool IsAllowedRedirectUri(string? value)
    {
        if (value is null || !Uri.TryCreate(value, UriKind.Absolute, out var uri) || value.Contains('#'))
            return false;

        if (uri.Scheme == Uri.UriSchemeHttps)
            return true;

        if (uri.Scheme == Uri.UriSchemeHttp)
            return uri.IsLoopback;

        return !_forbiddenRedirectSchemes.Contains(uri.Scheme)
            && !uri.Scheme.StartsWith("ms-", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The response's <c>max-age</c>, or <see cref="DefaultCacheDuration"/>, held between the minimum and maximum.</summary>
    private static TimeSpan CacheDuration(CacheControlHeaderValue? cacheControl)
    {
        var duration = cacheControl?.MaxAge ?? DefaultCacheDuration;
        return duration < MinCacheDuration ? MinCacheDuration
            : duration > MaxCacheDuration ? MaxCacheDuration
            : duration;
    }

    /// <summary>Reads <paramref name="property"/> when it is present and a string.</summary>
    private static bool TryGetString(JsonElement document, string property, out string value)
    {
        if (document.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.String)
        {
            value = element.GetString()!;
            return true;
        }

        value = string.Empty;
        return false;
    }

    /// <summary>Whether <paramref name="property"/> is absent, or an array that includes <paramref name="required"/>.</summary>
    private static bool ListsOrOmits(JsonElement document, string property, string required) =>
        !document.TryGetProperty(property, out var element)
        || (element.ValueKind == JsonValueKind.Array
            && element.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.String && e.GetString() == required));

    /// <summary>Reads <paramref name="property"/> when it is an absolute HTTPS URL, and drops it otherwise.</summary>
    private static string? HttpsUrlOrNull(JsonElement document, string property) =>
        TryGetString(document, property, out var value)
        && Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
            ? value
            : null;
}
