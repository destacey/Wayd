using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.Dtos;
using Wayd.AppIntegration.Application.Connections.Queries;
using Wayd.AppIntegration.Application.Tests.Infrastructure;
using Wayd.AppIntegration.Domain.Models;
using Wayd.Common.Application.Enums;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Queries;

public class GetSyncRunQueryHandlerTests
{
    private readonly FakeAppIntegrationDbContext _db = new();
    private readonly Mock<ILogger<GetSyncRunQueryHandler>> _logger = new();
    private readonly GetSyncRunQueryHandler _sut;

    private static readonly Instant _now = Instant.FromUtc(2026, 1, 1, 10, 0, 0);

    public GetSyncRunQueryHandlerTests()
    {
        _sut = new GetSyncRunQueryHandler(_db, _logger.Object);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenRunNotFound()
    {
        var result = await _sut.Handle(new GetSyncRunQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ReturnsDto_WithScalarFieldsPopulated()
    {
        var connectionId = Guid.NewGuid();
        var run = SyncRun.Start(connectionId, Connector.AzureDevOps, SyncType.Full, SyncTriggerSource.Manual, _now);
        _db.AddSyncRun(run);

        var result = await _sut.Handle(new GetSyncRunQuery(run.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(run.Id);
        result.ConnectionId.Should().Be(connectionId);
        result.ConnectorType.Should().Be(Connector.AzureDevOps);
        result.SyncType.Should().Be(SyncType.Full);
        result.TriggerSource.Should().Be(SyncTriggerSource.Manual);
        result.Status.Should().Be(SyncRunStatus.Running);
        result.StartedAt.Should().Be(_now);
    }

    [Fact]
    public async Task Handle_ParsesDetailsJson_IntoTypedList()
    {
        var workspaceId = Guid.NewGuid();
        var details = new List<WorkspaceSyncDetail>
        {
            new(workspaceId, "My Workspace", true, 42, 3, 1, 0, false, null)
        };
        var run = SyncRun.Start(Guid.NewGuid(), Connector.AzureDevOps, SyncType.Full, SyncTriggerSource.Scheduled, _now);
        run.SetDetails(JsonSerializer.Serialize(details));
        _db.AddSyncRun(run);

        var result = await _sut.Handle(new GetSyncRunQuery(run.Id), CancellationToken.None);

        result!.Details.Should().HaveCount(1);
        result.Details[0].InternalWorkspaceId.Should().Be(workspaceId);
        result.Details[0].WorkspaceName.Should().Be("My Workspace");
        result.Details[0].WorkItemsProcessed.Should().Be(42);
        result.Details[0].Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ReturnsEmptyDetails_WhenDetailsJsonIsNull()
    {
        var run = SyncRun.Start(Guid.NewGuid(), Connector.AzureDevOps, SyncType.Full, SyncTriggerSource.Scheduled, _now);
        _db.AddSyncRun(run);

        var result = await _sut.Handle(new GetSyncRunQuery(run.Id), CancellationToken.None);

        result!.Details.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsEmptyDetails_AndLogsWarning_WhenDetailsJsonIsMalformed()
    {
        var run = SyncRun.Start(Guid.NewGuid(), Connector.AzureDevOps, SyncType.Full, SyncTriggerSource.Scheduled, _now);
        run.SetDetails("not valid json {{{{");
        _db.AddSyncRun(run);

        var result = await _sut.Handle(new GetSyncRunQuery(run.Id), CancellationToken.None);

        result!.Details.Should().BeEmpty();
        _logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("DetailsJson")),
                It.IsAny<JsonException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
