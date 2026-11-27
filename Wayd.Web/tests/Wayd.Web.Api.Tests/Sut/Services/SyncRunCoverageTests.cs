using FluentAssertions;
using Hangfire.Common;
using Wayd.AppIntegration.Domain.Models;
using Wayd.Common.Application.Enums;
using Wayd.Web.Api.Services;

namespace Wayd.Web.Api.Tests.Sut.Services;

public sealed class SyncRunCoverageTests
{
    private static readonly Guid ConnectionA = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
    private static readonly Guid ConnectionB = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");

    private static Guid? Connection(string? name) => name switch
    {
        null => null,
        "A" => ConnectionA,
        "B" => ConnectionB,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    // Built from the jobs' own calls, so a renamed or reordered parameter fails here rather than in production.
    private static Job WorkSync(SyncType syncType, string? connection, SyncTriggerSource trigger = SyncTriggerSource.Scheduled) =>
        Job.FromExpression<JobManager>(m => m.RunWorkSync(syncType, trigger, Connection(connection), CancellationToken.None));

    private static Job PeopleSync(SyncType syncType, string? connection, SyncTriggerSource trigger = SyncTriggerSource.Scheduled) =>
        Job.FromExpression<JobManager>(m => m.RunPeopleSync(syncType, trigger, Connection(connection), CancellationToken.None));

    [Theory]
    [InlineData(SyncType.Full, null, SyncType.Differential, null, true)]
    [InlineData(SyncType.Differential, null, SyncType.Differential, null, true)]
    [InlineData(SyncType.Full, null, SyncType.Full, null, true)]
    [InlineData(SyncType.Differential, null, SyncType.Full, null, false)]
    [InlineData(SyncType.Full, null, SyncType.Differential, "A", true)]
    [InlineData(SyncType.Differential, "A", SyncType.Differential, "A", true)]
    [InlineData(SyncType.Full, "A", SyncType.Differential, "A", true)]
    [InlineData(SyncType.Differential, "A", SyncType.Full, "A", false)]
    [InlineData(SyncType.Full, "A", SyncType.Full, "B", false)]
    [InlineData(SyncType.Full, "A", SyncType.Full, null, false)]
    public void Covers_ForWorkSync_WhenTheRunningSyncDoesAllTheStartedOnesWork(
        SyncType runningType, string? runningConnection, SyncType startedType, string? startedConnection, bool expected)
    {
        // Arrange
        var sut = new SyncRunCoverage();

        // Act
        var covers = sut.Covers(WorkSync(runningType, runningConnection), WorkSync(startedType, startedConnection));

        // Assert
        covers.Should().Be(expected);
    }

    [Theory]
    [InlineData(SyncType.Full, null, SyncType.Differential, "B", true)]
    [InlineData(SyncType.Differential, "A", SyncType.Full, "A", false)]
    [InlineData(SyncType.Full, "A", SyncType.Full, "B", false)]
    public void Covers_ForPeopleSync_WhenTheRunningSyncDoesAllTheStartedOnesWork(
        SyncType runningType, string? runningConnection, SyncType startedType, string? startedConnection, bool expected)
    {
        // Arrange
        var sut = new SyncRunCoverage();

        // Act
        var covers = sut.Covers(PeopleSync(runningType, runningConnection), PeopleSync(startedType, startedConnection));

        // Assert
        covers.Should().Be(expected);
    }

    [Fact]
    public void Covers_IgnoresWhatTriggeredEachRun()
    {
        // Arrange
        var sut = new SyncRunCoverage();

        // Act
        var covers = sut.Covers(
            WorkSync(SyncType.Full, null, SyncTriggerSource.Scheduled),
            WorkSync(SyncType.Full, null, SyncTriggerSource.Manual));

        // Assert
        covers.Should().BeTrue();
    }
}
