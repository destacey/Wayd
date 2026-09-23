using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkItems.Forecasting;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkItems.Forecasting;

public sealed class TeamThroughputSamplerTests
{
    private static readonly LocalDate _from = new(2026, 9, 1);
    private static readonly LocalDate _to = new(2026, 9, 5);
    private static readonly WorkType _story = new WorkTypeFaker().AsStory().Generate();

    private static WorkItem CompletedItem(Guid? teamId, Instant doneTimestamp, WorkType? type = null, WorkStatusCategory statusCategory = WorkStatusCategory.Done) =>
        new WorkItemFaker()
            .WithType(type ?? _story)
            .WithTeamId(teamId)
            .WithStatusCategory(statusCategory)
            .WithDoneTimestamp(doneTimestamp)
            .Generate();

    [Fact]
    public async Task Sample_CountsEachTeamsCompletionsPerUtcDay()
    {
        // Arrange
        using var context = new FakeWorkDbContext();
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        context.AddWorkItems(
        [
            CompletedItem(teamA, Instant.FromUtc(2026, 9, 1, 0, 0)),
            CompletedItem(teamA, Instant.FromUtc(2026, 9, 1, 23, 59)),
            CompletedItem(teamA, Instant.FromUtc(2026, 9, 3, 12, 0)),
            CompletedItem(teamB, Instant.FromUtc(2026, 9, 5, 8, 0)),
        ]);
        var sampler = new TeamThroughputSampler(context);

        // Act
        var samples = await sampler.Sample([teamA, teamB], _from, _to, TestContext.Current.CancellationToken);

        // Assert
        samples[teamA].DailyThroughput.Should().Equal(2, 0, 1, 0, 0);
        samples[teamB].DailyThroughput.Should().Equal(0, 0, 0, 0, 1);
    }

    [Fact]
    public async Task Sample_LeavesOutCompletionsOutsideTheWindow()
    {
        // Arrange
        using var context = new FakeWorkDbContext();
        var team = Guid.NewGuid();
        context.AddWorkItems(
        [
            CompletedItem(team, Instant.FromUtc(2026, 8, 31, 23, 59)),
            CompletedItem(team, Instant.FromUtc(2026, 9, 6, 0, 0)),
            CompletedItem(team, Instant.FromUtc(2026, 9, 5, 23, 59)),
        ]);
        var sampler = new TeamThroughputSampler(context);

        // Act
        var samples = await sampler.Sample([team], _from, _to, TestContext.Current.CancellationToken);

        // Assert
        samples[team].DailyThroughput.Should().Equal(0, 0, 0, 0, 1);
    }

    [Fact]
    public async Task Sample_CountsOnlyDoneRequirementItemsOnTheTeam()
    {
        // Arrange
        using var context = new FakeWorkDbContext();
        var team = Guid.NewGuid();
        var done = Instant.FromUtc(2026, 9, 2, 12, 0);
        context.AddWorkItems(
        [
            CompletedItem(team, done),
            CompletedItem(team, done, statusCategory: WorkStatusCategory.Removed),
            CompletedItem(team, done, type: new WorkTypeFaker().AsOther().Generate()),
            CompletedItem(Guid.NewGuid(), done),
            CompletedItem(null, done),
        ]);
        var sampler = new TeamThroughputSampler(context);

        // Act
        var samples = await sampler.Sample([team], _from, _to, TestContext.Current.CancellationToken);

        // Assert
        samples[team].Total.Should().Be(1);
    }

    [Fact]
    public async Task Sample_TeamThatFinishedNothing_GetsAnEmptySample()
    {
        // Arrange
        using var context = new FakeWorkDbContext();
        var team = Guid.NewGuid();
        var sampler = new TeamThroughputSampler(context);

        // Act
        var samples = await sampler.Sample([team], _from, _to, TestContext.Current.CancellationToken);

        // Assert
        samples[team].Days.Should().Be(5);
        samples[team].HasThroughput.Should().BeFalse();
    }

    [Fact]
    public void LookbackWindow_EndsYesterdayInUtc()
    {
        // Arrange
        var now = Instant.FromUtc(2026, 9, 22, 0, 30);

        // Act
        var (from, to) = TeamThroughputSampler.LookbackWindow(now, days: 90);

        // Assert
        to.Should().Be(new LocalDate(2026, 9, 21));
        from.Should().Be(new LocalDate(2026, 6, 24));
        (Period.DaysBetween(from, to) + 1).Should().Be(90);
    }
}
