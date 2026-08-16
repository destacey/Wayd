using FluentAssertions;
using Wayd.Common.Application.BackgroundJobs;

namespace Wayd.Common.Application.Tests.Sut.BackgroundJobs;

public sealed class SchedulableBackgroundJobTypesTests
{
    // The data-replication syncs are driven by the flows that change the underlying data, so they
    // are deliberately run-on-demand only. Pinning the set here makes a change to it a decision
    // rather than an accident — adding a type to the schedulable set without also mapping a
    // recurring invocation in BackgroundJobsController.Create would fail at runtime.
    private static readonly BackgroundJobType[] _expectedSchedulable =
    [
        BackgroundJobType.PeopleFullSync,
        BackgroundJobType.PeopleDiffSync,
        BackgroundJobType.WorkFullSync,
        BackgroundJobType.WorkDiffSync,
        BackgroundJobType.TeamGraphSync,
        BackgroundJobType.PortfolioRankRebalance,
    ];

    [Fact]
    public void Contains_MatchesTheExpectedSchedulableSet()
    {
        // Arrange
        var allTypes = Enum.GetValues<BackgroundJobType>();

        // Act
        var schedulable = allTypes.Where(SchedulableBackgroundJobTypes.Contains);

        // Assert
        schedulable.Should().BeEquivalentTo(_expectedSchedulable);
    }

    [Theory]
    [InlineData(BackgroundJobType.IterationsSync)]
    [InlineData(BackgroundJobType.StrategicThemesSync)]
    [InlineData(BackgroundJobType.ProjectsSync)]
    [InlineData(BackgroundJobType.TeamsSync)]
    public void Contains_ForRunOnDemandOnlyTypes_IsFalse(BackgroundJobType jobType)
    {
        // Arrange & Act
        var isSchedulable = SchedulableBackgroundJobTypes.Contains(jobType);

        // Assert — these run fine from the run menu; they just cannot be scheduled.
        isSchedulable.Should().BeFalse();
    }
}
