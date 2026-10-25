using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using NodaTime;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.FeatureManagement.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Infrastructure.Auth;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Web.Api.IntegrationTests.Infrastructure;
using Wayd.Web.Api.Mcp;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Asserts the limits a signed-in caller meets, against the booted host: one budget per user across every endpoint and
/// credential, counted after authorization has named the user, a hosted MCP tool call counted once, and import
/// submissions held to a number in flight.
/// </summary>
/// <remarks>
/// Each test lowers a limit on a host of its own, so its limiter starts empty and no other test is throttled. Users and
/// tokens are created through the shared host, whose limits are raised, so creating them spends nothing here.
/// </remarks>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class PerUserRateLimitTests(WaydSqlServerApiFactory factory)
{
    private const string Profile = "/api/me";

    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public async Task SignedInUser_AnswersTooManyRequestsWithProblemDetails_PastTheirLimit()
    {
        // Arrange
        using var host = LimitedPerUser(tokens: 2);
        var client = ClientFor(host, await _factory.CreateAuthenticatedClient());
        for (var i = 0; i < 2; i++)
            (await client.GetAsync(Profile, TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        var response = await client.GetAsync(Profile, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter?.Delta.Should().BePositive();
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var problem = await Json(response);
        problem.GetProperty("status").GetInt32().Should().Be(429);
        problem.GetProperty("title").GetString().Should().Be("Too Many Requests");
        problem.GetProperty("detail").GetString().Should().StartWith("Too many requests. Try again in ");
        problem.TryGetProperty("traceId", out _).Should().BeTrue("a refusal carries the same trace fields as every other problem");
    }

    [Fact]
    public async Task Users_DoNotShareALimit()
    {
        // Arrange — the first user's budget spent entirely
        using var host = LimitedPerUser(tokens: 2);
        var spent = ClientFor(host, await _factory.CreateAuthenticatedClient());
        var other = ClientFor(host, await _factory.CreateAuthenticatedClient());
        for (var i = 0; i < 3; i++)
            await spent.GetAsync(Profile, TestContext.Current.CancellationToken);

        // Act
        var response = await other.GetAsync(Profile, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OneUsersCredentials_ShareOneLimit()
    {
        // Arrange — the web app's token spends the budget, so a personal access token must not buy a second one
        using var host = LimitedPerUser(tokens: 2);
        var (user, token) = await CreateUserWithToken();
        var session = ClientFor(host, user);
        for (var i = 0; i < 2; i++)
            (await session.GetAsync(Profile, TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.OK);
        var withToken = host.CreateClient();
        withToken.DefaultRequestHeaders.Add("x-api-key", token);

        // Act
        var response = await withToken.GetAsync(Profile, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task AnonymousRequests_AreNotLimitedPerUser()
    {
        // Arrange — a per-user budget of one, which anonymous requests would exhaust at once if they were counted
        using var host = LimitedPerUser(tokens: 1);
        var client = host.CreateClient();
        await client.GetAsync("/api/auth/providers", TestContext.Current.CancellationToken);

        // Act
        var response = await client.GetAsync("/api/auth/providers", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task McpToolCall_IsCountedOnce()
    {
        // Arrange — two tokens: connecting and listing must spend none, and each call exactly one
        await SetMcpFlag(true);
        using var host = LimitedPerUser(tokens: 2);
        var (_, token) = await CreateUserWithToken();
        await using var mcp = await Connect(host, token);
        await mcp.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var first = await mcp.CallToolAsync("Me_GetProfile", cancellationToken: TestContext.Current.CancellationToken);
        var second = await mcp.CallToolAsync("Me_GetProfile", cancellationToken: TestContext.Current.CancellationToken);
        var third = await mcp.CallToolAsync("Me_GetProfile", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        first.IsError.Should().NotBe(true);
        second.IsError.Should().NotBe(true);
        third.IsError.Should().BeTrue();
        third.Content.OfType<TextContentBlock>().First().Text.Should().Contain("Too many requests. Try again in ");
    }

    [Fact]
    public async Task ImportSubmission_AnswersTooManyRequests_WhileTheUsersLimitIsInFlight()
    {
        // Arrange — one submission held inside its response wait, so it holds the user's only slot
        using var host = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection([new KeyValuePair<string, string?>("RateLimitSettings:Imports:PermitLimit", "1")])));
        var user = await _factory.CreateAuthenticatedClient(
            ApplicationPermission.NameFor(ApplicationAction.Import, ApplicationResource.StrategicThemes));
        var client = ClientFor(host, user);

        HttpResponseMessage response;
        Task<HttpResponseMessage> held;
        bool heldWhileRefused;
        using (_factory.WaitOnImportsFor(TimeSpan.FromSeconds(30)))
        using (_factory.ImportClaims.Hold())
        {
            held = client.PostAsync(ThemeImport, ThemeFile(), TestContext.Current.CancellationToken);
            await UntilQueued(user.UserId);

            // Act
            response = await client.PostAsync(ThemeImport, ThemeFile(), TestContext.Current.CancellationToken);
            heldWhileRefused = !held.IsCompleted;
        }
        using var heldResponse = await held;

        // Assert
        heldWhileRefused.Should().BeTrue("the first submission must still hold the slot when the second arrives");
        heldResponse.IsSuccessStatusCode.Should().BeTrue("the held submission is accepted once the gate opens");
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await Json(response)).GetProperty("status").GetInt32().Should().Be(429);
    }

    private const string ThemeImport = "/api/strategic-management/strategic-themes/import";

    /// <summary>A host whose per-user bucket holds <paramref name="tokens"/> requests and does not refill within a test.</summary>
    private WebApplicationFactory<Program> LimitedPerUser(int tokens) =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>("RateLimitSettings:PerUser:TokenLimit", tokens.ToString(CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string?>("RateLimitSettings:PerUser:TokensPerPeriod", "1"),
                new KeyValuePair<string, string?>("RateLimitSettings:PerUser:PeriodSeconds", "3600"),
            ])));

    /// <summary>A client of <paramref name="host"/> carrying <paramref name="user"/>'s token.</summary>
    private static HttpClient ClientFor(WebApplicationFactory<Program> host, AuthenticatedClient user)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = user.Client.DefaultRequestHeaders.Authorization;
        return client;
    }

    private async Task<(AuthenticatedClient User, string Token)> CreateUserWithToken()
    {
        var user = await _factory.CreateAuthenticatedClient();
        var response = await user.Client.PostAsJsonAsync(
            "/api/user-management/personal-access-tokens",
            new { name = $"Limit test {Guid.NewGuid():N}"[..24], expiresAt = SystemClock.Instance.GetCurrentInstant().Plus(Duration.FromDays(7)).ToString() },
            TestContext.Current.CancellationToken);
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var created = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return (user, created.GetProperty("token").GetString()!);
    }

    private static async Task<McpClient> Connect(WebApplicationFactory<Program> host, string token)
    {
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(host.Server.BaseAddress, McpServerSetup.Path),
                AdditionalHeaders = new Dictionary<string, string> { ["x-api-key"] = token },
            },
            host.CreateClient(),
            ownsHttpClient: true);

        return await McpClient.CreateAsync(transport, cancellationToken: TestContext.Current.CancellationToken);
    }

    private async Task SetMcpFlag(bool enabled)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId("integration-test-harness");
        var flagId = await scope.ServiceProvider.GetRequiredService<IFeatureManagementDbContext>().FeatureFlags
            .Where(f => f.Name == FeatureFlags.Names.McpServer)
            .Select(f => f.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

        var toggled = await scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(
            new ToggleFeatureFlagCommand(flagId, enabled), TestContext.Current.CancellationToken);
        toggled.IsSuccess.Should().BeTrue(toggled.IsFailure ? toggled.Error : null);
    }

    /// <summary>Waits until <paramref name="userId"/> has a queued run, which a submission creates only once it holds its slot.</summary>
    private async Task UntilQueued(string userId)
    {
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            using var scope = _factory.Services.CreateScope();
            var queued = await scope.ServiceProvider.GetRequiredService<IImportDbContext>().ImportProcesses
                .AnyAsync(p => p.SubmittedByUserId == userId && p.Status == ImportProcessStatus.Queued, TestContext.Current.CancellationToken);
            if (queued)
                return;

            elapsed.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(20), "the held submission should have queued its run");
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }

    private static MultipartFormDataContent ThemeFile()
    {
        var name = $"Theme {Guid.NewGuid():N}"[..24];
        var csv = $"ImportId,Name,Description,State\nt1,{name},Submitted by the rate limit test.,Active\n";
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        return new MultipartFormDataContent { { file, "file", "themes.csv" } };
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        (await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken)).RootElement;
}
