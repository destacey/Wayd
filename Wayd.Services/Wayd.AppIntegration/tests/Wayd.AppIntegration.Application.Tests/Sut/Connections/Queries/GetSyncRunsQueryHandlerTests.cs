using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.Queries;
using Wayd.AppIntegration.Application.Tests.Infrastructure;
using Wayd.AppIntegration.Domain.Models;
using Wayd.Common.Application.Enums;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Tests.Shared;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Queries;

public class GetSyncRunsQueryHandlerTests
{
    private readonly FakeAppIntegrationDbContext _db = new();
    private readonly TestingDateTimeProvider _clock;
    private readonly GetSyncRunsQueryHandler _sut;

    // Clock is set to t2 + 30m so all three timestamps fall well within the default 24h window.
    private static readonly Instant _t0 = Instant.FromUtc(2026, 1, 1, 9, 0, 0);
    private static readonly Instant _t1 = Instant.FromUtc(2026, 1, 1, 10, 0, 0);
    private static readonly Instant _t2 = Instant.FromUtc(2026, 1, 1, 11, 0, 0);
    private static readonly Instant _now = Instant.FromUtc(2026, 1, 1, 11, 30, 0);

    public GetSyncRunsQueryHandlerTests()
    {
        _clock = new TestingDateTimeProvider(_now.ToDateTimeUtc());
        _sut = new GetSyncRunsQueryHandler(_db, _clock);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyRunsForRequestedConnection()
    {
        var targetId = Guid.NewGuid();
        var otherId = Guid.NewGuid();

        _db.AddSyncRun(SyncRun.Start(targetId, Connector.AzureDevOps, SyncType.Full, SyncTriggerSource.Scheduled, _t0));
        _db.AddSyncRun(SyncRun.Start(otherId, Connector.AzureDevOps, SyncType.Full, SyncTriggerSource.Scheduled, _t1));

        var result = await _sut.Handle(new GetSyncRunsQuery(targetId), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].ConnectionId.Should().Be(targetId);
    }

    [Fact]
    public async Task Handle_ReturnsRunsOrderedByStartedAtDescending()
    {
        var connectionId = Guid.NewGuid();

        _db.AddSyncRun(SyncRun.Start(connectionId, Connector.AzureDevOps, SyncType.Full, SyncTriggerSource.Scheduled, _t0));
        _db.AddSyncRun(SyncRun.Start(connectionId, Connector.AzureDevOps, SyncType.Full, SyncTriggerSource.Scheduled, _t2));
        _db.AddSyncRun(SyncRun.Start(connectionId, Connector.AzureDevOps, SyncType.Full, SyncTriggerSource.Scheduled, _t1));

        var result = await _sut.Handle(new GetSyncRunsQuery(connectionId), CancellationToken.None);

        result.Should().HaveCount(3);
        result[0].StartedAt.Should().Be(_t2);
        result[1].StartedAt.Should().Be(_t1);
        result[2].StartedAt.Should().Be(_t0);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNoRunsExist()
    {
        var result = await _sut.Handle(new GetSyncRunsQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_DefaultsToLast24Hours_WhenSinceIsNull()
    {
        var connectionId = Guid.NewGuid();

        // _now = 2026-01-01 11:30 UTC. Cutoff = 2026-12-31 11:30 UTC (24h prior).
        // Older run sits before the window; newer sits inside.
        var olderRun = SyncRun.Start(connectionId, Connector.AzureDevOps, SyncType.Full, SyncTriggerSource.Scheduled, _now.Minus(Duration.FromHours(30)));
        var newerRun = SyncRun.Start(connectionId, Connector.AzureDevOps, SyncType.Full, SyncTriggerSource.Scheduled, _now.Minus(Duration.FromHours(1)));

        _db.AddSyncRun(olderRun);
        _db.AddSyncRun(newerRun);

        var result = await _sut.Handle(new GetSyncRunsQuery(connectionId), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].StartedAt.Should().Be(newerRun.StartedAt);
    }

    [Fact]
    public async Task Handle_HonorsExplicitSince()
    {
        var connectionId = Guid.NewGuid();

        _db.AddSyncRun(SyncRun.Start(connectionId, Connector.AzureDevOps, SyncType.Full, SyncTriggerSource.Scheduled, _t0));
        _db.AddSyncRun(SyncRun.Start(connectionId, Connector.AzureDevOps, SyncType.Full, SyncTriggerSource.Scheduled, _t2));

        // Cutoff between _t0 and _t2 — only _t2 should come back.
        var since = _t1;

        var result = await _sut.Handle(new GetSyncRunsQuery(connectionId, Since: since), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].StartedAt.Should().Be(_t2);
    }
}
