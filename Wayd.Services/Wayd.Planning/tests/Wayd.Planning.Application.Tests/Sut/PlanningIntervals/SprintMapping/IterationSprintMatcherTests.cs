using NodaTime;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Common.Models;
using Wayd.Planning.Application.PlanningIntervals.SprintMapping;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PlanningIntervals.SprintMapping;

public sealed class IterationSprintMatcherTests
{
    private static readonly LocalDate FirstMonday = new(2026, 1, 5);

    // Two back-to-back two-week iterations: Jan 5-18 and Jan 19-Feb 1.
    private readonly PlanningIntervalIteration _first = Iteration(FirstMonday, FirstMonday.PlusDays(13));
    private readonly PlanningIntervalIteration _second = Iteration(FirstMonday.PlusDays(14), FirstMonday.PlusDays(27));

    [Fact]
    public void Match_MapsEachAlignedSprintToItsIteration()
    {
        // Arrange
        var sprint1 = Sprint(FirstMonday, FirstMonday.PlusDays(13));
        var sprint2 = Sprint(FirstMonday.PlusDays(14), FirstMonday.PlusDays(27));

        // Act
        var matches = IterationSprintMatcher.Match([_first, _second], [sprint1, sprint2]);

        // Assert
        matches.Should().BeEquivalentTo(new Dictionary<Guid, Guid>
        {
            [_first.Id] = sprint1.Id,
            [_second.Id] = sprint2.Id,
        });
    }

    [Fact]
    public void Match_MapsASprintOffByADayAtEitherEnd()
    {
        // Arrange — a team whose sprints run Tuesday to Monday.
        var sprint1 = Sprint(FirstMonday.PlusDays(1), FirstMonday.PlusDays(14));
        var sprint2 = Sprint(FirstMonday.PlusDays(15), FirstMonday.PlusDays(28));

        // Act
        var matches = IterationSprintMatcher.Match([_first, _second], [sprint1, sprint2]);

        // Assert
        matches.Should().BeEquivalentTo(new Dictionary<Guid, Guid>
        {
            [_first.Id] = sprint1.Id,
            [_second.Id] = sprint2.Id,
        });
    }

    [Fact]
    public void Match_MapsAStraddlingSprintToTheIterationHoldingMostOfIt()
    {
        // Arrange — 4 days in the first iteration, 10 in the second.
        var sprint = Sprint(FirstMonday.PlusDays(10), FirstMonday.PlusDays(23));

        // Act
        var matches = IterationSprintMatcher.Match([_first, _second], [sprint]);

        // Assert
        matches.Should().BeEquivalentTo(new Dictionary<Guid, Guid> { [_second.Id] = sprint.Id });
    }

    [Fact]
    public void Match_LeavesASprintSplitEvenlyBetweenTwoIterations()
    {
        // Arrange — 7 days in each.
        var sprint = Sprint(FirstMonday.PlusDays(7), FirstMonday.PlusDays(20));

        // Act
        var matches = IterationSprintMatcher.Match([_first, _second], [sprint]);

        // Assert
        matches.Should().BeEmpty();
    }

    [Fact]
    public void Match_PicksTheSprintWithTheMostDaysInTheIteration()
    {
        // Arrange — all three have most of their days in the first iteration.
        var sevenDays = Sprint(FirstMonday.PlusDays(2), FirstMonday.PlusDays(8));
        var fiveDays = Sprint(FirstMonday.PlusDays(9), FirstMonday.PlusDays(13));
        var nineOfTwelveDays = Sprint(FirstMonday.PlusDays(-3), FirstMonday.PlusDays(8));

        // Act
        var matches = IterationSprintMatcher.Match([_first, _second], [sevenDays, fiveDays, nineOfTwelveDays]);

        // Assert
        matches.Should().BeEquivalentTo(new Dictionary<Guid, Guid> { [_first.Id] = nineOfTwelveDays.Id });
    }

    [Fact]
    public void Match_BreaksATieWithTheEarlierSprint()
    {
        // Arrange — two one-week sprints in one two-week iteration.
        var week1 = Sprint(FirstMonday, FirstMonday.PlusDays(6));
        var week2 = Sprint(FirstMonday.PlusDays(7), FirstMonday.PlusDays(13));

        // Act
        var matches = IterationSprintMatcher.Match([_first], [week2, week1]);

        // Assert
        matches.Should().BeEquivalentTo(new Dictionary<Guid, Guid> { [_first.Id] = week1.Id });
    }

    [Fact]
    public void Match_IgnoresASprintWithoutBothDates()
    {
        // Arrange
        var noEnd = new PlanningSprintFaker().AsSprint().WithDateRange(IterationDateRange.Create(FirstMonday, null)).Generate();
        var noDates = new PlanningSprintFaker().AsSprint().WithDateRange(IterationDateRange.Create(null, null)).Generate();

        // Act
        var matches = IterationSprintMatcher.Match([_first, _second], [noEnd, noDates]);

        // Assert
        matches.Should().BeEmpty();
    }

    [Fact]
    public void Match_IgnoresAnIterationThatIsNotASprint()
    {
        // Arrange
        var kanbanIteration = new PlanningSprintFaker().AsIteration()
            .WithDateRange(IterationDateRange.Create(FirstMonday, FirstMonday.PlusDays(13)))
            .Generate();

        // Act
        var matches = IterationSprintMatcher.Match([_first], [kanbanIteration]);

        // Assert
        matches.Should().BeEmpty();
    }

    [Fact]
    public void Match_LeavesASprintWhoseIterationIsNotOffered()
    {
        // Arrange — the sprint belongs to the first iteration, which the caller left out.
        var sprint = Sprint(FirstMonday, FirstMonday.PlusDays(13));

        // Act
        var matches = IterationSprintMatcher.Match([_second], [sprint]);

        // Assert
        matches.Should().BeEmpty();
    }

    private static PlanningIntervalIteration Iteration(LocalDate start, LocalDate end) =>
        new PlanningIntervalIterationFaker().WithDateRange(new LocalDateRange(start, end)).Generate();

    private static PlanningSprint Sprint(LocalDate start, LocalDate end) =>
        new PlanningSprintFaker().AsSprint().WithDateRange(IterationDateRange.Create(start, end)).Generate();
}
