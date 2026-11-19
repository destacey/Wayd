using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Tests.Shared;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkItems.Queries;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkItems.Queries;

public sealed class GetWorkItemMetricsQueryTests : IDisposable
{
    // 6 March in UTC, still 5 March in the system zone.
    private static readonly Instant Now = Instant.FromUtc(2026, 3, 6, 5, 0);

    private static readonly WorkType Story = new WorkTypeFaker().AsStory().Generate();

    private readonly Guid _workspaceId = Guid.NewGuid();
    private readonly WorkItemKey _key = new("WAYD-1");
    private readonly FakeWorkDbContext _dbContext = new();
    private readonly GetWorkItemMetricsQueryHandler _handler;

    public GetWorkItemMetricsQueryTests()
    {
        MapsterTestConfiguration.Ensure();
        var clock = new TestingDateTimeProvider(new FakeClock(Now));
        _handler = new GetWorkItemMetricsQueryHandler(
            _dbContext,
            NullLogger<GetWorkItemMetricsQueryHandler>.Instance,
            new TestingSystemCalendar(clock));
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_NamesTheDaysOfADoneItemInTheSystemZone()
    {
        // Arrange — created at 04:00 UTC on 2 March and done at 03:00 UTC on 4 March, which are still 1 and 3 March
        // in the system zone
        _dbContext.AddWorkItem(
            new WorkItemFaker(_workspaceId)
                .WithType(Story)
                .WithKey(_key)
                .WithStatusCategory(StatusCategory.Done)
                .WithCreated(Instant.FromUtc(2026, 3, 2, 4, 0))
                .WithActivatedTimestamp(Instant.FromUtc(2026, 3, 2, 4, 0))
                .WithDoneTimestamp(Instant.FromUtc(2026, 3, 4, 3, 0))
                .Generate());

        // Act
        var result = await _handler.Handle(new GetWorkItemMetricsQuery(_workspaceId, _key), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Select(d => d.Date).Should().Equal(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 3));
        result.Value.Last().Done.Should().Be(1);
    }

    [Fact]
    public async Task Handle_RunsAnOpenItemUpToTodayInTheSystemZone()
    {
        // Arrange
        _dbContext.AddWorkItem(
            new WorkItemFaker(_workspaceId)
                .WithType(Story)
                .WithKey(_key)
                .WithStatusCategory(StatusCategory.Active)
                .WithCreated(Instant.FromUtc(2026, 3, 3, 12, 0))
                .WithActivatedTimestamp(Instant.FromUtc(2026, 3, 3, 12, 0))
                .WithDoneTimestamp(null)
                .Generate());

        // Act
        var result = await _handler.Handle(new GetWorkItemMetricsQuery(_workspaceId, _key), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Last().Date.Should().Be(new DateOnly(2026, 3, 5));
    }
}
