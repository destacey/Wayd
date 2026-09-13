using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Wayd.Tools.DataGeneration.Cli.Generation;
using Wayd.Tools.DataGeneration.Cli.Recipes;
using Wayd.Tools.DataGeneration.Cli.Seeding;
using Wayd.Tools.DataGeneration.Cli.Ui;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

/// <summary>
/// The page's server, over real HTTP on the loopback address. The seed itself is a stand-in, so nothing here
/// reaches a Wayd API: what is under test is who may start one, with which token, and what the page sees.
/// </summary>
public class UiServerTests : IAsyncLifetime
{
    private const string PageToken = "page-token";
    private const string PastedToken = "pasted-pat";
    private const string EnvironmentToken = "environment-pat";

    // Small and org-only, so starting a seed does not spend the test's time generating a company.
    private static readonly object _recipe = new
    {
        organization = new { valueStreams = 1, teams = 3 },
        ppm = new { enabled = false },
        productManagement = new { enabled = false },
        planning = new { enabled = false },
    };

    private readonly List<(string ApiUrl, string ApiKey)> _seeds = [];
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    /// <summary>What the stand-in does once it is called. Logs a line and succeeds unless a test says otherwise.</summary>
    private Func<Action<string>, CancellationToken, Task> _seedWork = (log, _) =>
    {
        log("stand-in seed ran");
        return Task.CompletedTask;
    };

    private string? _environmentApiKey = EnvironmentToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    private async Task Start()
    {
        var settings = new UiSettings(
            new DirectoryInfo(Path.GetTempPath()),
            DefaultApi: null,
            _environmentApiKey,
            Seed);

        _app = UiServer.Build(settings, PageToken);
        await _app.StartAsync(TestContext.Current.CancellationToken);

        _client = new HttpClient { BaseAddress = new Uri(_app.Urls.First()) };
        _client.DefaultRequestHeaders.Add("X-Wayd-Ui-Token", PageToken);
    }

    private Task Seed(GeneratedDataset dataset, ResolvedRecipe resolved, string apiUrl, string apiKey, Action<string> log, CancellationToken cancellationToken)
    {
        lock (_seeds)
            _seeds.Add((apiUrl, apiKey));

        return _seedWork(log, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null)
            await _app.DisposeAsync();

        GC.SuppressFinalize(this);
    }

    private Task<HttpResponseMessage> PostSeed(string? apiKey, string? api = "https://localhost:5001") =>
        _client.PostAsJsonAsync("/api/seed", new { recipe = _recipe, seed = 7, api, apiKey }, TestContext.Current.CancellationToken);

    private async Task<Guid> StartedRun(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return body.RootElement.GetProperty("runId").GetGuid();
    }

    private async Task<string> Events(Guid runId) =>
        await _client.GetStringAsync($"/api/seed/{runId}/events", TestContext.Current.CancellationToken);

    [Fact]
    public async Task Build_RefusesARequestWithoutThePageToken()
    {
        // Arrange — the server holds a credential while a seed runs, so guessing the port must not be enough
        await Start();
        using var stranger = new HttpClient { BaseAddress = _client.BaseAddress };

        // Act
        var schema = await stranger.GetAsync("/api/schema", TestContext.Current.CancellationToken);
        var seed = await stranger.PostAsJsonAsync("/api/seed", new { recipe = _recipe, api = "https://localhost:5001", apiKey = PastedToken }, TestContext.Current.CancellationToken);

        // Assert
        schema.StatusCode.Should().Be(HttpStatusCode.NotFound);
        seed.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _seeds.Should().BeEmpty();
    }

    [Fact]
    public async Task StartSeed_WithNoTokenAnywhere_IsRefusedWithoutStartingARun()
    {
        // Arrange
        _environmentApiKey = null;
        await Start();

        // Act
        var response = await PostSeed(apiKey: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Contain("WAYD_API_KEY");
        _seeds.Should().BeEmpty();
    }

    [Fact]
    public async Task StartSeed_WithoutAnHttpApiUrl_IsRefusedWithoutStartingARun()
    {
        // Arrange
        await Start();

        // Act
        var response = await PostSeed(PastedToken, api: "localhost:5001");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _seeds.Should().BeEmpty();
    }

    [Fact]
    public async Task StartSeed_FallsBackToTheTokenTheToolWasStartedWith()
    {
        // Arrange
        await Start();

        // Act
        var runId = await StartedRun(await PostSeed(apiKey: null));
        await Events(runId);

        // Assert
        _seeds.Should().ContainSingle().Which.ApiKey.Should().Be(EnvironmentToken);
    }

    [Fact]
    public async Task StartSeed_PrefersAPastedTokenOverTheEnvironments()
    {
        // Arrange
        await Start();

        // Act
        var runId = await StartedRun(await PostSeed(PastedToken));
        await Events(runId);

        // Assert
        _seeds.Should().ContainSingle().Which.Should().Be(("https://localhost:5001/", PastedToken));
    }

    [Fact]
    public async Task Events_StreamTheRunsLogAndHowItEnded_WithoutTheToken()
    {
        // Arrange
        await Start();
        var runId = await StartedRun(await PostSeed(PastedToken));

        // Act
        var stream = await Events(runId);

        // Assert
        stream.Should().Contain("Generated ").And.Contain("stand-in seed ran");
        stream.Should().EndWith("event: done\ndata: {\"state\":\"Succeeded\",\"error\":null}\n\n");
        stream.Should().NotContain(PastedToken);
    }

    [Fact]
    public async Task Events_ReportWhyARunFailed()
    {
        // Arrange
        _seedWork = (_, _) => throw new SeedException("The teams import was rejected.");
        await Start();
        var runId = await StartedRun(await PostSeed(PastedToken));

        // Act
        var stream = await Events(runId);

        // Assert
        stream.Should().Contain("\"state\":\"Failed\"").And.Contain("The teams import was rejected.");
    }

    [Fact]
    public async Task StartSeed_RefusesASecondSeedWhileOneIsRunning()
    {
        // Arrange — two seeds into one environment would race each other's imports
        var release = new TaskCompletionSource();
        _seedWork = (_, _) => release.Task;
        await Start();
        var first = await StartedRun(await PostSeed(PastedToken));

        // Act
        var second = await PostSeed(PastedToken);
        release.SetResult();
        await Events(first);
        var third = await PostSeed(PastedToken);

        // Assert
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        third.StatusCode.Should().Be(HttpStatusCode.Accepted, "a finished seed no longer blocks the next");
    }

    [Fact]
    public async Task Cancel_StopsTheRun()
    {
        // Arrange
        _seedWork = (_, cancellationToken) => Task.Delay(Timeout.Infinite, cancellationToken);
        await Start();
        var runId = await StartedRun(await PostSeed(PastedToken));

        // Act
        var cancel = await _client.PostAsync($"/api/seed/{runId}/cancel", null, TestContext.Current.CancellationToken);
        var stream = await Events(runId);

        // Assert
        cancel.StatusCode.Should().Be(HttpStatusCode.Accepted);
        stream.Should().Contain("\"state\":\"Canceled\"");
    }

    [Fact]
    public async Task Defaults_SayWhetherATokenIsAvailableWithoutRevealingIt()
    {
        // Arrange
        await Start();

        // Act
        var body = await _client.GetStringAsync("/api/seed/defaults", TestContext.Current.CancellationToken);

        // Assert
        body.Should().Contain("\"apiKeyFromEnvironment\":true").And.NotContain(EnvironmentToken);
    }
}
