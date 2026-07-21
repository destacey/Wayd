using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Integrations.AzureDevOps.Tests.Support;

namespace Wayd.Integrations.AzureDevOps.Tests.Sut;

public class AzureDevOpsServiceTests
{
    private const string OrganizationUrl = "https://dev.azure.com/acme";
    private const string Token = "test-pat-token";

    private static readonly AzureDevOpsConnectionContext _connection = new(OrganizationUrl, Token);

    private readonly StubHttpMessageHandler _handler = new();
    private readonly FakeHttpClientFactory _httpClientFactory;
    private readonly FakeMemoryCache _memoryCache = new();
    private readonly AzureDevOpsService _sut;

    public AzureDevOpsServiceTests()
    {
        _httpClientFactory = new FakeHttpClientFactory(_handler);
        _sut = new AzureDevOpsService(
            NullLogger<AzureDevOpsService>.Instance,
            NullLoggerFactory.Instance,
            _httpClientFactory,
            new FixedDateTimeProvider(),
            _memoryCache);
    }

    [Fact]
    public async Task GetIterations_SecondCallWithinCacheWindow_DoesNotReissueIterationsRequest()
    {
        // Arrange - GetIterations calls GetProject (1 request) + GetOrFetchIterationsAsync (2 requests:
        // project details + iteration tree). A second call for the same project/team-settings should
        // reuse the cached iteration tree instead of hitting the iterations endpoint again.
        var projectId = Guid.NewGuid();
        _handler.EnqueueResponse(HttpStatusCode.OK, ProjectJson(projectId));
        _handler.EnqueueResponse(HttpStatusCode.OK, PropertiesJson());
        _handler.EnqueueResponse(HttpStatusCode.OK, IterationTreeJson());
        _handler.EnqueueResponse(HttpStatusCode.OK, ProjectJson(projectId));
        _handler.EnqueueResponse(HttpStatusCode.OK, PropertiesJson());

        // Act
        var first = await _sut.GetIterations(_connection, "Atlas", [], TestContext.Current.CancellationToken);
        var second = await _sut.GetIterations(_connection, "Atlas", [], TestContext.Current.CancellationToken);

        // Assert
        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        second.Value.Should().BeEquivalentTo(first.Value);
        _handler.Requests.Should().HaveCount(5); // not 6 — the iteration tree fetch was skipped the second time
        _memoryCache.SetCallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetWorkspace_WithoutProcessTemplateTypeProperty_ReturnsFailure()
    {
        // Arrange - a project with no System.ProcessTemplateType property can't be mapped to a workspace configuration
        var projectId = Guid.NewGuid();
        _handler.EnqueueResponse(HttpStatusCode.OK, ProjectJson(projectId));
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"count":0,"value":[]}""");

        // Act
        var result = await _sut.GetWorkspace(_connection, projectId, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("process template type");
    }

    [Fact]
    public async Task TestConnection_WhenUnderlyingCallFails_ReturnsFailureInsteadOfThrowing()
    {
        // Arrange
        _handler.EnqueueResponse(HttpStatusCode.Unauthorized, """{"message":"access denied"}""");

        // Act
        var result = await _sut.TestConnection(_connection);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnection_WhenConnectionDataHasNoInstanceIdMatch_Succeeds()
    {
        // Arrange
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"instanceId":"6ff2ee2f-9d9b-40b1-9502-e4c00a318c00"}""");

        // Act
        var result = await _sut.TestConnection(_connection);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task EachServiceCall_RequestsAFreshHttpClientFromTheFactory()
    {
        // Arrange - AzureDevOpsService must not cache/reuse a client across calls; each Create*Service
        // call pulls a new one from the factory, which owns pooling/lifetime.
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"instanceId":"6ff2ee2f-9d9b-40b1-9502-e4c00a318c00"}""");
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"instanceId":"6ff2ee2f-9d9b-40b1-9502-e4c00a318c00"}""");

        // Act
        await _sut.GetSystemId(_connection, TestContext.Current.CancellationToken);
        await _sut.GetSystemId(_connection, TestContext.Current.CancellationToken);

        // Assert
        _httpClientFactory.CreateClientCallCount.Should().Be(2);
    }

    private static string ProjectJson(Guid id)
    {
        return $$"""{"id":"{{id}}","name":"Atlas","description":"Test project"}""";
    }

    private static string PropertiesJson()
    {
        return """{"count":1,"value":[{"name":"System.ProcessTemplateType","value":"adcc42ab-9882-485e-a3fe-04140420fbb1"}]}""";
    }

    private static string IterationTreeJson()
    {
        return """
            {
                "id": 1,
                "identifier": "68429463-523f-4c69-8c16-4321543db2e4",
                "name": "Atlas",
                "path": "\\Atlas\\Iteration",
                "children": []
            }
            """;
    }

    private sealed class FixedDateTimeProvider : IDateTimeProvider
    {
        public Instant Now => Instant.FromUtc(2026, 1, 1, 0, 0);
        public LocalDate Today => new(2026, 1, 1);
    }
}
