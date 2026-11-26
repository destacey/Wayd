using System.Net;
using System.Text;
using FluentAssertions;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Asserts that every anonymous /api/auth endpoint is rate limited per caller address, against the booted host: the
/// policies are attached by attribute, so nothing short of a real request shows that an endpoint lost its limit or
/// was moved into another endpoint's budget.
/// </summary>
/// <remarks>
/// The tests share the host on which every one of these limits is lowered (<see cref="AddressLimitedHost"/>), each
/// sending from an address of its own, so its windows start empty and no other test is throttled. With every limit
/// lowered, a refusal alone does not show which limit refused, so a test that spends one endpoint's budget also shows
/// the endpoints on the other limits still answering. Requests carry an empty body: request validation refuses it with
/// 422 before the action runs, so nothing is written and no account is locked, while the limiter, which runs first,
/// still counts it.
/// </remarks>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class AuthRateLimitTests(WaydSqlServerApiFactory factory)
{
    private const int Limit = AddressLimitedHost.Limit;

    private readonly WaydSqlServerApiFactory _factory = factory;

    [Theory]
    [InlineData("AuthSignIn", "POST", "/api/auth/login")]
    [InlineData("AuthSignIn", "POST", "/api/auth/exchange")]
    [InlineData("AuthRefresh", "POST", "/api/auth/refresh-token")]
    [InlineData("AuthProviders", "GET", "/api/auth/providers")]
    [InlineData("AuthSetup", "POST", "/api/auth/setup")]
    public async Task Endpoint_AnswersTooManyRequests_PastItsLimit(string limit, string method, string path)
    {
        // Arrange
        var client = _factory.AddressLimited().CreateClientFromNewAddress();
        for (var i = 0; i < Limit; i++)
            (await Send(client, method, path)).StatusCode.Should().Be(WithinLimit(method), $"{limit} allows {Limit}");

        // Act
        var response = await Send(client, method, path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter.Should().NotBeNull();
        response.Headers.RetryAfter!.Delta.Should().BePositive();
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        foreach (var other in Endpoints.Where(e => e.Limit != limit))
            (await Send(client, other.Method, other.Path)).StatusCode.Should().Be(
                WithinLimit(other.Method), $"{other.Path} is on {other.Limit}, which {path} must not spend");
    }

    [Fact]
    public async Task SignInAndExchange_ShareOneBudget()
    {
        // Arrange — the sign-in budget spent entirely on password sign-in
        var client = _factory.AddressLimited().CreateClientFromNewAddress();
        for (var i = 0; i < Limit; i++)
            (await Send(client, "POST", "/api/auth/login")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // Act
        var response = await Send(client, "POST", "/api/auth/exchange");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Refresh_DoesNotSpendTheSignInBudget()
    {
        // Arrange — both lowered, and the refresh budget spent, so a shared window would refuse the sign-in
        var client = _factory.AddressLimited().CreateClientFromNewAddress();
        for (var i = 0; i < Limit; i++)
            await Send(client, "POST", "/api/auth/refresh-token");
        (await Send(client, "POST", "/api/auth/refresh-token")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // Act
        var response = await Send(client, "POST", "/api/auth/login");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>Every anonymous /api/auth endpoint, with the limit it is on.</summary>
    private static readonly (string Limit, string Method, string Path)[] Endpoints =
    [
        ("AuthSignIn", "POST", "/api/auth/login"),
        ("AuthSignIn", "POST", "/api/auth/exchange"),
        ("AuthRefresh", "POST", "/api/auth/refresh-token"),
        ("AuthProviders", "GET", "/api/auth/providers"),
        ("AuthSetup", "POST", "/api/auth/setup"),
    ];

    /// <summary>
    /// What a request within its limit answers: request validation refuses the empty body of a POST before the action
    /// runs, so no test signs anyone in or completes setup, and the provider list needs no body.
    /// </summary>
    private static HttpStatusCode WithinLimit(string method) =>
        method == "POST" ? HttpStatusCode.UnprocessableEntity : HttpStatusCode.OK;

    private static Task<HttpResponseMessage> Send(HttpClient client, string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
