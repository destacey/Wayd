using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Asserts that every anonymous /api/auth endpoint is rate limited per caller address, against the booted host: the
/// policies are attached by attribute, so nothing short of a real request shows that an endpoint lost its limit or
/// was moved into another endpoint's budget.
/// </summary>
/// <remarks>
/// Each test lowers the limits on a host of its own, so its limiter starts empty and no other test is throttled.
/// Requests carry an empty body: request validation refuses it with 422 before the action runs, so nothing is written
/// and no account is locked, while the limiter, which runs first, still counts it.
/// </remarks>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class AuthRateLimitTests(WaydSqlServerApiFactory factory)
{
    private const int Limit = 2;

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
        using var host = LimitedHost(limit);
        var client = host.CreateClient();
        for (var i = 0; i < Limit; i++)
            (await Send(client, method, path)).StatusCode.Should().Be(WithinLimit(method));

        // Act
        var response = await Send(client, method, path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter.Should().NotBeNull();
        response.Headers.RetryAfter!.Delta.Should().BePositive();
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task SignInAndExchange_ShareOneBudget()
    {
        // Arrange — the sign-in budget spent entirely on password sign-in
        using var host = LimitedHost("AuthSignIn");
        var client = host.CreateClient();
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
        using var host = LimitedHost("AuthSignIn", "AuthRefresh");
        var client = host.CreateClient();
        for (var i = 0; i < Limit; i++)
            await Send(client, "POST", "/api/auth/refresh-token");
        (await Send(client, "POST", "/api/auth/refresh-token")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // Act
        var response = await Send(client, "POST", "/api/auth/login");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>
    /// What a request within its limit answers: request validation refuses the empty body of a POST before the action
    /// runs, so no test signs anyone in or completes setup, and the provider list needs no body.
    /// </summary>
    private static HttpStatusCode WithinLimit(string method) =>
        method == "POST" ? HttpStatusCode.UnprocessableEntity : HttpStatusCode.OK;

    /// <summary>A host whose named limits allow <see cref="Limit"/> requests an hour.</summary>
    private WebApplicationFactory<Program> LimitedHost(params string[] limits) =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(limits.SelectMany(limit => new[]
            {
                new KeyValuePair<string, string?>($"RateLimitSettings:{limit}:PermitLimit", Limit.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string?>($"RateLimitSettings:{limit}:WindowSeconds", "3600"),
            }))));

    private static Task<HttpResponseMessage> Send(HttpClient client, string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
