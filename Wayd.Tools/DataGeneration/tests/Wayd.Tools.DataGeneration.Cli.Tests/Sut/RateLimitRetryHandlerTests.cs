using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli.Seeding;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

/// <summary>
/// Holds a seed to waiting out a rate limit and sending the same request again, rather than failing on the first 429.
/// </summary>
public class RateLimitRetryHandlerTests
{
    [Fact]
    public async Task Send_WaitsTheStatedTimeAndResends_WhenRefused()
    {
        // Arrange
        var server = new ScriptedServer(TooManyRequests(seconds: 7), Ok());
        var waits = new List<TimeSpan>();
        using var client = Client(server, waits);

        // Act
        var response = await client.PostAsync("https://wayd.test/api/x", new StringContent("{\"name\":\"a\"}", Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        waits.Should().Equal(TimeSpan.FromSeconds(7));
        server.Bodies.Should().Equal("{\"name\":\"a\"}", "{\"name\":\"a\"}");
    }

    [Fact]
    public async Task Send_ResendsAMultipartBody_Whole()
    {
        // Arrange — the shape a CSV import posts
        var server = new ScriptedServer(TooManyRequests(seconds: 1), Ok());
        using var client = Client(server, []);
        using var content = new MultipartFormDataContent { { new ByteArrayContent(Encoding.UTF8.GetBytes("Name\nA\n")), "file", "a.csv" } };

        // Act
        await client.PostAsync("https://wayd.test/api/import", content, TestContext.Current.CancellationToken);

        // Assert
        server.Bodies.Should().HaveCount(2);
        server.Bodies[1].Should().Be(server.Bodies[0]).And.Contain("Name\nA\n");
    }

    [Fact]
    public async Task Send_ReturnsTheLastRefusal_AfterTheLastAttempt()
    {
        // Arrange
        var server = new ScriptedServer(Enumerable.Range(0, RateLimitRetryHandler.MaxAttempts + 2).Select(_ => TooManyRequests(seconds: 1)).ToArray());
        var waits = new List<TimeSpan>();
        using var client = Client(server, waits);

        // Act
        var response = await client.GetAsync("https://wayd.test/api/x", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        server.Bodies.Should().HaveCount(RateLimitRetryHandler.MaxAttempts);
        waits.Should().HaveCount(RateLimitRetryHandler.MaxAttempts - 1);
    }

    [Fact]
    public async Task Send_DoesNotResend_AnythingButARefusal()
    {
        // Arrange
        var server = new ScriptedServer(new HttpResponseMessage(HttpStatusCode.InternalServerError), Ok());
        var waits = new List<TimeSpan>();
        using var client = Client(server, waits);

        // Act
        var response = await client.GetAsync("https://wayd.test/api/x", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        server.Bodies.Should().ContainSingle();
        waits.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null, 2)]
    [InlineData(0, 2)]
    [InlineData(5, 5)]
    [InlineData(600, 30)]
    public void WaitFor_UsesTheStatedSeconds_WithinItsBounds(int? seconds, int expected)
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        if (seconds is { } s)
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(s));

        // Act
        var wait = RateLimitRetryHandler.WaitFor(response);

        // Assert
        wait.Should().Be(TimeSpan.FromSeconds(expected));
    }

    [Fact]
    public void ClientTimeout_OutlastsEveryWaitTheHandlerCanMake()
    {
        // Arrange — the waits happen inside one send, so the client's timeout must cover all of them
        var longestWaits = RateLimitRetryHandler.MaxWait * (RateLimitRetryHandler.MaxAttempts - 1);

        // Act
        var headroom = RateLimitRetryHandler.ClientTimeout - longestWaits;

        // Assert
        headroom.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMinutes(1));
    }

    private static HttpClient Client(ScriptedServer server, List<TimeSpan> waits) =>
        new(new RateLimitRetryHandler(server, (wait, _) =>
        {
            waits.Add(wait);
            return Task.CompletedTask;
        }));

    private static HttpResponseMessage TooManyRequests(int seconds)
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));
        return response;
    }

    private static HttpResponseMessage Ok() => new(HttpStatusCode.OK);

    /// <summary>Answers each request with the next scripted response, recording the body each one carried.</summary>
    private sealed class ScriptedServer(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            return _responses.Dequeue();
        }
    }
}
