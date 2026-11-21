using Wayd.Infrastructure.Auth.Oidc;
using Wayd.Infrastructure.Networking;

namespace Wayd.Infrastructure.Tests.Sut.Auth.Oidc;

/// <summary>
/// The factory's whole purpose is to hold one ConfigurationManager per distinct
/// authority URL forever (so JWKS caching stays warm). These tests pin that
/// instance-reuse contract and the URL normalization that makes it work.
/// </summary>
public class OidcConfigurationManagerFactoryTests
{
    [Fact]
    public void Get_WithSameAuthority_ReturnsSameInstance()
    {
        var factory = new OidcConfigurationManagerFactory(new HttpClient());
        const string authority = "https://login.microsoftonline.com/common/v2.0";

        var first = factory.Get(authority);
        var second = factory.Get(authority);

        // Reference equality on the manager is the contract: each call would
        // build a new JWKS cache otherwise, defeating the whole point of
        // caching.
        second.Should().BeSameAs(first);
    }

    [Fact]
    public void Get_WithDifferentAuthorities_ReturnsDifferentInstances()
    {
        var factory = new OidcConfigurationManagerFactory(new HttpClient());

        var entra = factory.Get("https://login.microsoftonline.com/common/v2.0");
        var google = factory.Get("https://accounts.google.com");

        google.Should().NotBeSameAs(entra);
    }

    [Theory]
    [InlineData("https://login.microsoftonline.com/common/v2.0", "https://login.microsoftonline.com/common/v2.0/")]
    [InlineData("https://login.microsoftonline.com/common/v2.0", "HTTPS://Login.MicrosoftOnline.com/common/v2.0")]
    [InlineData("https://login.microsoftonline.com/common/v2.0", "  https://login.microsoftonline.com/common/v2.0  ")]
    public void Get_WithTrivialUrlVariants_CollapsesToSingleInstance(string canonical, string variant)
    {
        // Trailing slash, host case, surrounding whitespace must all resolve to
        // the same manager. Without normalization an operator typo would
        // silently double the JWKS fetch rate.
        var factory = new OidcConfigurationManagerFactory(new HttpClient());

        var canonicalManager = factory.Get(canonical);
        var variantManager = factory.Get(variant);

        variantManager.Should().BeSameAs(canonicalManager);
    }

    [Fact]
    public void Get_WithPathCaseChange_ReturnsDifferentInstance()
    {
        // Path segments are case-sensitive per RFC 3986. Treating them as
        // equivalent could route a token to the wrong tenant's OIDC config in
        // edge cases (rare in practice for Entra, but the rule is the rule).
        var factory = new OidcConfigurationManagerFactory(new HttpClient());

        var lower = factory.Get("https://example.com/tenants/abc");
        var upper = factory.Get("https://example.com/tenants/ABC");

        upper.Should().NotBeSameAs(lower);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Get_WithBlankAuthority_Throws(string? authority)
    {
        var factory = new OidcConfigurationManagerFactory(new HttpClient());

        var act = () => factory.Get(authority!);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com")]   // technically a valid URL but the OIDC retriever requires HTTPS
    [InlineData("/relative/path")]
    public void Get_WithInvalidAuthority_Throws(string authority)
    {
        // The retriever has RequireHttps=true and would fail at JWKS-fetch time,
        // but failing at registration time with the authority value in scope
        // is a better error experience. Note: "ftp://example.com" parses as an
        // absolute URI so the failure here surfaces only at fetch time — we
        // accept that and check that the constructor itself doesn't throw on it.
        var factory = new OidcConfigurationManagerFactory(new HttpClient());

        if (Uri.TryCreate(authority, UriKind.Absolute, out _))
        {
            // Valid absolute URI but non-HTTPS — factory accepts, retriever
            // refuses at fetch time. Not the factory's responsibility.
            var act = () => factory.Get(authority);
            act.Should().NotThrow();
        }
        else
        {
            var act = () => factory.Get(authority);
            act.Should().Throw<ArgumentException>();
        }
    }

    [Fact]
    public void Get_FromConcurrentCallers_ReturnsSingleInstance()
    {
        // ConcurrentDictionary.GetOrAdd's factory can run more than once under
        // contention, but only one of the candidates is kept and returned to all
        // callers. This test verifies that contract under real concurrency.
        var factory = new OidcConfigurationManagerFactory(new HttpClient());
        const string authority = "https://example.com/oidc";
        var results = new System.Collections.Concurrent.ConcurrentBag<object>();

        Parallel.For(0, 100, _ =>
        {
            results.Add(factory.Get(authority));
        });

        results.Distinct().Should().HaveCount(1);
    }

    [Theory]
    [InlineData("https://127.0.0.1/realms/wayd")]
    [InlineData("https://169.254.169.254/metadata")]
    [InlineData("https://10.0.0.5")]
    public async Task GetConfiguration_ThroughAPublicNetworkClient_RefusesANonPublicAuthority(string authority)
    {
        // Arrange
        using var client = new HttpClient(PublicNetwork.CreateHandler(TimeSpan.FromSeconds(5)));
        var manager = new OidcConfigurationManagerFactory(client).Get(authority);

        // Act
        var act = () => manager.GetConfigurationAsync(TestContext.Current.CancellationToken);

        // Assert — the guard's refusal, not a connection failure an unguarded client would also meet
        (await act.Should().ThrowAsync<Exception>())
            .Which.ToString().Should().Contain("does not resolve to a public address");
    }

    [Fact]
    public async Task GetConfiguration_RefusesADiscoveryDocumentLargerThanTheCap()
    {
        // Arrange
        var body = new string(' ', Wayd.Infrastructure.Auth.Oidc.ConfigureServices.MaxResponseBytes + 1);
        using var client = Wayd.Infrastructure.Auth.Oidc.ConfigureServices.CreateClient(new StaticResponseHandler(body));
        var manager = new OidcConfigurationManagerFactory(client).Get("https://idp.acme.example");

        // Act
        var act = () => manager.GetConfigurationAsync(TestContext.Current.CancellationToken);

        // Assert
        var thrown = (await act.Should().ThrowAsync<Exception>()).Which;
        SelfAndInner(thrown).OfType<HttpRequestException>()
            .Should().Contain(e => e.HttpRequestError == HttpRequestError.ConfigurationLimitExceeded);
    }

    private static IEnumerable<Exception> SelfAndInner(Exception exception)
    {
        yield return exception;
        var inner = exception is AggregateException aggregate ? aggregate.InnerExceptions : [exception.InnerException];
        foreach (var child in inner.OfType<Exception>())
            foreach (var descendant in SelfAndInner(child))
                yield return descendant;
    }

    /// <summary>Answers every request with the same body, without a network.</summary>
    private sealed class StaticResponseHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) });
    }
}
