using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Infrastructure.Auth.OAuth;

namespace Wayd.Infrastructure.Tests.Sut.Auth.OAuth;

/// <summary>
/// Holds the resolver to the Client ID Metadata Document rules: which <c>client_id</c> URLs it will fetch, which
/// documents it accepts, and which it refuses because they would let a client claim another's identity, expect
/// credentials Wayd never checks, or redirect where Wayd must not send an authorization code.
/// </summary>
public sealed class ClientMetadataDocumentResolverTests
{
    private const string ClientId = "https://client.acme.example/oauth/client-metadata.json";

    [Fact]
    public async Task Resolve_ReadsTheClientFromItsDocument()
    {
        // Arrange
        var handler = new StubHandler(Document());
        var resolver = Resolver(handler);

        // Act
        var result = await resolver.Resolve(ClientId, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().BeEquivalentTo(new OAuthClient(
            ClientId,
            "Acme Agent",
            ["https://client.acme.example/callback", "http://127.0.0.1:33418/callback"],
            "https://acme.example",
            null));
        handler.Requests.Should().ContainSingle().Which.Should().Be(new Uri(ClientId));
    }

    [Fact]
    public async Task Resolve_NamesTheClientByItsHost_WhenTheDocumentGivesNoName()
    {
        // Arrange
        var resolver = Resolver(new StubHandler(Document(name: null)));

        // Act
        var result = await resolver.Resolve(ClientId, TestContext.Current.CancellationToken);

        // Assert
        result.Value.Name.Should().Be("client.acme.example");
    }

    [Fact]
    public async Task Resolve_FetchesADocumentOnce_WhileItIsCached()
    {
        // Arrange
        var handler = new StubHandler(Document());
        var resolver = Resolver(handler);
        await resolver.Resolve(ClientId, TestContext.Current.CancellationToken);

        // Act
        var result = await resolver.Resolve(ClientId, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task Resolve_FetchesAgain_WhenTheDocumentSaysNotToStoreIt()
    {
        // Arrange
        var handler = new StubHandler(Document()) { CacheControl = new CacheControlHeaderValue { NoStore = true } };
        var resolver = Resolver(handler);
        await resolver.Resolve(ClientId, TestContext.Current.CancellationToken);

        // Act
        await resolver.Resolve(ClientId, TestContext.Current.CancellationToken);

        // Assert
        handler.Requests.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("client-123")]
    [InlineData("http://client.acme.example/oauth/client-metadata.json")]
    [InlineData("https://client.acme.example")]
    [InlineData("https://client.acme.example/")]
    [InlineData("https://client.acme.example/oauth/client-metadata.json#fragment")]
    [InlineData("https://user:secret@client.acme.example/oauth/client-metadata.json")]
    [InlineData("https://client.acme.example/oauth/../client-metadata.json")]
    [InlineData("https://client.acme.example/oauth/./client-metadata.json")]
    [InlineData("https://client.acme.example/oauth/%2e%2e/client-metadata.json")]
    [InlineData(@"https://client.acme.example/oauth\..\client-metadata.json")]
    [InlineData("https://client.acme.example/oauth/client metadata.json")]
    [InlineData("https://client.acme.example/oauth/client-metadata.json\n")]
    public async Task Resolve_RefusesAClientIdThatIsNotAMetadataDocumentUrl_WithoutFetchingIt(string clientId)
    {
        // Arrange
        var handler = new StubHandler(Document());
        var resolver = Resolver(handler);

        // Act
        var result = await resolver.Resolve(clientId, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Resolve_AcceptsAClientIdWithAQueryAndAPort()
    {
        // Arrange
        const string clientId = "https://client.acme.example:8443/oauth/metadata?v=2";
        var resolver = Resolver(new StubHandler(Document(clientId: clientId)));

        // Act
        var result = await resolver.Resolve(clientId, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
    }

    [Fact]
    public async Task Resolve_RemembersARefusal_RatherThanFetchingAgainAtOnce()
    {
        // Arrange — otherwise retrying a bad client_id would make Wayd fetch from a host of the caller's choosing
        var handler = new StubHandler(Document()) { Status = HttpStatusCode.NotFound };
        var resolver = Resolver(handler);
        await resolver.Resolve(ClientId, TestContext.Current.CancellationToken);

        // Act
        var result = await resolver.Resolve(ClientId, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task Resolve_SharesOneFetch_AmongConcurrentRequestsForOneClient()
    {
        // Arrange
        var release = new TaskCompletionSource();
        var handler = new StubHandler(Document()) { Gate = release.Task };
        var resolver = Resolver(handler);

        // Act
        var first = resolver.Resolve(ClientId, TestContext.Current.CancellationToken);
        var second = resolver.Resolve(ClientId, TestContext.Current.CancellationToken);
        release.SetResult();
        var results = await Task.WhenAll(first, second);

        // Assert
        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task Resolve_Fails_WhenTheBodyArrivesTooSlowly()
    {
        // Arrange — the headers arrive at once and the body never does, which a timeout on the headers alone misses
        var resolver = Resolver(new StubHandler(Document()) { BodyStalls = true }, TimeSpan.FromMilliseconds(200));

        // Act
        var result = await resolver.Resolve(ClientId, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The client's metadata document could not be retrieved.");
    }

    [Fact]
    public async Task Resolve_Fails_WhenTheConnectionDropsPartWayThroughTheBody()
    {
        // Arrange
        var resolver = Resolver(new StubHandler(Document()) { BodyFails = true });

        // Act
        var result = await resolver.Resolve(ClientId, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The client's metadata document could not be retrieved.");
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData(HttpStatusCode.NonAuthoritativeInformation)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Found)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Resolve_Fails_WhenTheDocumentIsNotServed(HttpStatusCode status)
    {
        // Arrange
        var resolver = Resolver(new StubHandler(Document()) { Status = status });

        // Act
        var result = await resolver.Resolve(ClientId, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Resolve_Fails_WhenTheDocumentCannotBeReached()
    {
        // Arrange
        var resolver = Resolver(new StubHandler(Document()) { Throws = new HttpRequestException("refused") });

        // Act
        var result = await resolver.Resolve(ClientId, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The client's metadata document could not be retrieved.");
    }

    [Fact]
    public async Task Resolve_Fails_WhenTheDocumentIsNotJson()
    {
        // Arrange
        var resolver = Resolver(new StubHandler(Document()) { MediaType = "text/html" });

        // Act
        var result = await resolver.Resolve(ClientId, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Resolve_Fails_WhenTheDocumentIsTooLarge_WhetherOrNotItSaysSo(bool declaresLength)
    {
        // Arrange
        var padded = Document().Replace("\"Acme Agent\"", $"\"{new string('a', ClientMetadataDocumentResolver.MaxDocumentBytes)}\"");
        var resolver = Resolver(new StubHandler(padded) { DeclaresLength = declaresLength });

        // Act
        var result = await resolver.Resolve(ClientId, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The client's metadata document is larger than Wayd accepts.");
    }

    [Fact]
    public async Task Resolve_Fails_WhenTheDocumentNamesAnotherClient()
    {
        // Arrange
        var resolver = Resolver(new StubHandler(Document(clientId: "https://other.example/client.json")));

        // Act
        var result = await resolver.Resolve(ClientId, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("client_id");
    }

    [Theory]
    [InlineData("""{"client_id":"https://client.acme.example/oauth/client-metadata.json","redirect_uris":["https://client.acme.example/callback"],"token_endpoint_auth_method":"private_key_jwt"}""")]
    [InlineData("""{"client_id":"https://client.acme.example/oauth/client-metadata.json","redirect_uris":["https://client.acme.example/callback"],"grant_types":["client_credentials"]}""")]
    [InlineData("""{"client_id":"https://client.acme.example/oauth/client-metadata.json","redirect_uris":["https://client.acme.example/callback"],"response_types":["token"]}""")]
    [InlineData("""{"client_id":"https://client.acme.example/oauth/client-metadata.json"}""")]
    [InlineData("""{"client_id":"https://client.acme.example/oauth/client-metadata.json","redirect_uris":[]}""")]
    [InlineData("""["https://client.acme.example/oauth/client-metadata.json"]""")]
    [InlineData("""not json""")]
    public async Task Resolve_RefusesADocumentThatDoesNotDescribeAPublicCodeFlowClient(string document)
    {
        // Arrange
        var resolver = Resolver(new StubHandler(document));

        // Act
        var result = await resolver.Resolve(ClientId, TestContext.Current.CancellationToken);

        // Assert — the reason goes back to the client as an error_description, whose characters RFC 6749 §5.2 limits
        result.IsFailure.Should().BeTrue();
        result.Error.Should().MatchRegex(@"^[\x20-\x21\x23-\x5B\x5D-\x7E]+$");
    }

    [Theory]
    [InlineData("https://client.acme.example/callback", true)]
    [InlineData("http://127.0.0.1:33418/callback", true)]
    [InlineData("http://[::1]:33418/callback", true)]
    [InlineData("http://localhost:33418/callback", true)]
    [InlineData("com.acme.agent:/oauth/callback", true)]
    [InlineData("cursor://anysphere.cursor-mcp/oauth/callback", true)]
    [InlineData("http://client.acme.example/callback", false)]
    [InlineData("https://client.acme.example/callback#token", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/html,hi", false)]
    [InlineData("file:///etc/passwd", false)]
    [InlineData("intent://callback#Intent;scheme=acme;end", false)]
    [InlineData("ms-settings:privacy", false)]
    [InlineData("wss://client.acme.example/callback", false)]
    [InlineData("ftp://client.acme.example/callback", false)]
    [InlineData("/callback", false)]
    public void IsAllowedRedirectUri_AllowsOnlyWhereACodeMaySafelyBeSent(string redirectUri, bool allowed)
    {
        // Arrange — nothing beyond the URI under test

        // Act
        var result = ClientMetadataDocumentResolver.IsAllowedRedirectUri(redirectUri);

        // Assert
        result.Should().Be(allowed);
    }

    /// <summary>A resolver fetching through <paramref name="handler"/>, with a fetch budget long enough not to matter unless given.</summary>
    private static ClientMetadataDocumentResolver Resolver(StubHandler handler, TimeSpan? fetchTimeout = null) =>
        new(new HttpClient(handler), fetchTimeout ?? TimeSpan.FromSeconds(30), NullLogger<ClientMetadataDocumentResolver>.Instance);

    /// <summary>A valid metadata document for <paramref name="clientId"/>, with the name left out when null.</summary>
    private static string Document(string clientId = ClientId, string? name = "Acme Agent") =>
        $$"""
        {
            "client_id": "{{clientId}}",
            {{(name is null ? "" : $"\"client_name\": \"{name}\",")}}
            "client_uri": "https://acme.example",
            "logo_uri": "http://acme.example/logo.png",
            "redirect_uris": ["https://client.acme.example/callback", "http://127.0.0.1:33418/callback"],
            "grant_types": ["authorization_code", "refresh_token"],
            "response_types": ["code"],
            "token_endpoint_auth_method": "none"
        }
        """;

    /// <summary>Answers every request with one document, recording what was asked for.</summary>
    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        /// <summary>Every URL asked for, in order.</summary>
        public List<Uri> Requests { get; } = [];

        /// <summary>The response's status.</summary>
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;

        /// <summary>The response's media type.</summary>
        public string MediaType { get; init; } = "application/json";

        /// <summary>Whether the response gives its Content-Length, or sends a body of unknown length as a chunked one would.</summary>
        public bool DeclaresLength { get; init; } = true;

        /// <summary>The response's Cache-Control, if any.</summary>
        public CacheControlHeaderValue? CacheControl { get; init; }

        /// <summary>Thrown in place of a response, as a refused connection would be.</summary>
        public Exception? Throws { get; init; }

        /// <summary>Awaited before answering, so concurrent requests can be made to overlap.</summary>
        public Task? Gate { get; init; }

        /// <summary>Whether the body, after the headers, never arrives.</summary>
        public bool BodyStalls { get; init; }

        /// <summary>Whether reading the body fails as a dropped connection does.</summary>
        public bool BodyFails { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            if (Gate is not null)
                await Gate;
            if (Throws is not null)
                throw Throws;

            var bytes = Encoding.UTF8.GetBytes(body);
            HttpContent content = BodyStalls ? new StreamContent(new StallingStream())
                : BodyFails ? new StreamContent(new FailingStream())
                : DeclaresLength ? new ByteArrayContent(bytes)
                : new StreamContent(new UnseekableStream(bytes));
            content.Headers.ContentType = new MediaTypeHeaderValue(MediaType);
            if (!DeclaresLength || BodyStalls || BodyFails)
                content.Headers.ContentLength = null;

            var response = new HttpResponseMessage(Status) { Content = content };
            response.Headers.CacheControl = CacheControl;
            return response;
        }
    }

    /// <summary>A body whose length cannot be known in advance, as a chunked response's cannot.</summary>
    private class UnseekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        /// <inheritdoc />
        public override bool CanSeek => false;
    }

    /// <summary>A body that never sends a byte, until the reader gives up.</summary>
    private sealed class StallingStream() : UnseekableStream([])
    {
        /// <inheritdoc />
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
    }

    /// <summary>A body whose connection drops on the first read.</summary>
    private sealed class FailingStream() : UnseekableStream([])
    {
        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new IOException("The connection was reset."));
    }
}
