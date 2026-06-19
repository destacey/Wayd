using System.Text.Json;
using FluentAssertions;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.Queries;
using Wayd.AppIntegration.Application.Tests.Infrastructure;
using Wayd.AppIntegration.Domain.Models;
using Wayd.Common.Application.Enums;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Queries;

public class GetSyncRunQueryHandlerTests
{
    private readonly FakeAppIntegrationDbContext _db = new();
    private readonly GetSyncRunQueryHandler _sut;

    private static readonly Instant _now = Instant.FromUtc(2026, 1, 1, 10, 0, 0);

    public GetSyncRunQueryHandlerTests()
    {
        _sut = new GetSyncRunQueryHandler(_db);
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
    public async Task Handle_ReturnsDetailsJson_Verbatim()
    {
        // The handler must pass DetailsJson through untouched so connector-specific frontends
        // can parse it against the schema they know (work-sync vs people-sync, etc.).
        var payload = JsonSerializer.Serialize(new { employeesFetched = 42, employeesUpserted = 40 });
        var run = SyncRun.Start(Guid.NewGuid(), Connector.Entra, SyncType.Full, SyncTriggerSource.Scheduled, _now);
        run.SetDetails(payload);
        _db.AddSyncRun(run);

        var result = await _sut.Handle(new GetSyncRunQuery(run.Id), CancellationToken.None);

        result!.DetailsJson.Should().Be(payload);
    }

    [Fact]
    public async Task Handle_ReturnsNullDetailsJson_WhenNotSet()
    {
        var run = SyncRun.Start(Guid.NewGuid(), Connector.AzureDevOps, SyncType.Full, SyncTriggerSource.Scheduled, _now);
        _db.AddSyncRun(run);

        var result = await _sut.Handle(new GetSyncRunQuery(run.Id), CancellationToken.None);

        result!.DetailsJson.Should().BeNull();
    }
}
