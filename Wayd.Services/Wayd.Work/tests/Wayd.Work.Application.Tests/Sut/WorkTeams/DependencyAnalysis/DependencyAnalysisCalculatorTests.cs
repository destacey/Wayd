using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Work.Application.WorkItemDependencies.Dtos;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkTeams;
using Wayd.Work.Application.WorkTeams.DependencyAnalysis;
using Wayd.Work.Application.WorkTeams.Dtos;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkTeams.DependencyAnalysis;

public sealed class DependencyAnalysisCalculatorTests
{
    private static readonly LocalDate From = new(2026, 7, 1);
    private static readonly LocalDate To = new(2026, 9, 28);
    private static readonly Instant RangeStart = Instant.FromUtc(2026, 7, 1, 0, 0);
    private static readonly Instant RangeEnd = Instant.FromUtc(2026, 9, 29, 0, 0);

    private static readonly WorkTeamNavigationDto Root = Team("ART", "Platform ART", "Team of Teams");
    private static readonly WorkTeamNavigationDto Payments = Team("PAY", "Payments");
    private static readonly WorkTeamNavigationDto Mobile = Team("MOB", "Mobile");
    private static readonly WorkTeamNavigationDto Data = Team("DAT", "Data");
    private static readonly WorkTeamNavigationDto Identity = Team("IDN", "Identity");

    private static readonly HashSet<Guid> Scope = [Payments.Id, Mobile.Id];

    private static int _nextKey = 1;

    private static WorkTeamNavigationDto Team(string code, string name, string type = "Team") => new()
    {
        Id = Guid.NewGuid(),
        Key = _nextKey++,
        Name = name,
        Code = code,
        Type = type,
    };

    private static WorkItemDetailsNavigationDto Item(WorkTeamNavigationDto? team) => new()
    {
        Id = Guid.NewGuid(),
        Key = $"WI-{_nextKey++}",
        Title = "Item",
        WorkspaceKey = "WI",
        Type = "Story",
        Status = "New",
        StatusCategory = SimpleNavigationDto.Create(1, "Proposed"),
        Team = team,
    };

    private static DependencyAnalysisItem Dependency(
        WorkTeamNavigationDto? predecessor,
        WorkTeamNavigationDto? successor,
        Instant createdOn,
        Instant? closedOn = null) => new(
        new DependencyDto
        {
            Id = Guid.NewGuid(),
            Source = Item(predecessor),
            Target = Item(successor),
            LinkType = SimpleNavigationDto.Create(2, "Dependency"),
            State = SimpleNavigationDto.Create(1, "To Do"),
            Health = SimpleNavigationDto.Create(1, "Healthy"),
            CreatedOn = createdOn,
        },
        closedOn);

    private static Instant On(int month, int day) => Instant.FromUtc(2026, month, day, 12, 0);

    private static TeamDependencyAnalysisDto Calculate(params DependencyAnalysisItem[] items) =>
        DependencyAnalysisCalculator.Calculate(Root, From, To, TeamReportCalendar.Utc, Scope, items);

    [Fact]
    public void Calculate_CountsOnlyDependenciesOpenDuringTheRange()
    {
        // Arrange
        var openThroughout = Dependency(Payments, Mobile, On(5, 1));
        var closedOnTheFirstDay = Dependency(Payments, Data, On(5, 2), RangeStart);
        var closedJustBefore = Dependency(Payments, Data, On(5, 1), RangeStart.Minus(Duration.FromSeconds(1)));
        var createdOnTheLastDay = Dependency(Mobile, Data, RangeEnd.Minus(Duration.FromSeconds(1)));
        var createdJustAfter = Dependency(Mobile, Data, RangeEnd);

        // Act
        var result = Calculate(createdOnTheLastDay, closedOnTheFirstDay, openThroughout, closedJustBefore, createdJustAfter);

        // Assert
        result.Dependencies.Select(d => d.Id).Should().Equal(
            openThroughout.Dependency.Id,
            closedOnTheFirstDay.Dependency.Id,
            createdOnTheLastDay.Dependency.Id);
        result.Team.Should().Be(Root);
        result.From.Should().Be(From);
        result.To.Should().Be(To);
    }

    [Fact]
    public void Calculate_CountsTheRangeAndPeriodsInTheCalendarsTimeZone()
    {
        // Arrange
        var tokyo = TeamReportCalendar.From([new TeamTimeZonePeriod(new LocalDate(2026, 1, 1), null, "Asia/Tokyo")]);
        // 01:00 on July 1st in Tokyo, still June 30th in UTC.
        var createdOnTheFirstTokyoDay = Dependency(Payments, Mobile, Instant.FromUtc(2026, 6, 30, 16, 0));
        // 01:00 on September 29th in Tokyo, after the range there although inside it in UTC.
        var createdAfterTheLastTokyoDay = Dependency(Payments, Mobile, Instant.FromUtc(2026, 9, 28, 16, 0));

        // Act
        var result = DependencyAnalysisCalculator.Calculate(
            Root, From, To, tokyo, Scope, [createdOnTheFirstTokyoDay, createdAfterTheLastTokyoDay]);

        // Assert
        result.TimeZone.Should().Be("Asia/Tokyo");
        result.Dependencies.Select(d => d.Id).Should().Equal(createdOnTheFirstTokyoDay.Dependency.Id);
        result.Summary.Created.Should().Be(1);
        result.Periods[0].Created.Should().Be(1);
    }

    [Fact]
    public void Calculate_SummarisesCreatedClosedOpenAndTeams()
    {
        // Arrange
        var items = new[]
        {
            Dependency(Payments, Mobile, On(5, 1)),                 // open throughout
            Dependency(Payments, Data, On(7, 10), On(8, 1)),        // created and closed in the range
            Dependency(Identity, Mobile, On(6, 1), On(10, 5)),      // closed after the range
            Dependency(Mobile, null, On(9, 1)),                     // created in the range, an end with no team
        };

        // Act
        var result = Calculate(items);

        // Assert
        result.Summary.Should().BeEquivalentTo(new DependencyAnalysisSummaryDto
        {
            Dependencies = 4,
            Created = 2,
            Closed = 1,
            OpenAtEnd = 3,
            TeamsInvolved = 4,
            TeamsOutside = 2,
        });
    }

    [Fact]
    public void Calculate_GroupsPairsByDirectionMostDependenciesFirst()
    {
        // Arrange
        var items = new[]
        {
            Dependency(Mobile, Payments, On(7, 5)),
            Dependency(Payments, Data, On(5, 1), On(8, 1)),
            Dependency(Payments, Data, On(7, 20)),
            Dependency(Data, Payments, On(6, 1)),
            Dependency(null, Mobile, On(6, 1)),
        };

        // Act
        var result = Calculate(items);

        // Assert
        result.Pairs.Select(p => (p.PredecessorTeam?.Code, p.SuccessorTeam?.Code, p.Dependencies)).Should().Equal(
            ("PAY", "DAT", 2),
            ("DAT", "PAY", 1),
            ("MOB", "PAY", 1),
            (null, "MOB", 1));
        result.Pairs[0].Should().BeEquivalentTo(new DependencyAnalysisPairDto
        {
            PredecessorTeam = Payments,
            SuccessorTeam = Data,
            PredecessorTeamInScope = true,
            SuccessorTeamInScope = false,
            Dependencies = 2,
            Created = 1,
            Closed = 1,
            OpenAtEnd = 1,
        });
        result.Pairs[3].PredecessorTeamInScope.Should().BeFalse();
        result.Pairs[3].SuccessorTeamInScope.Should().BeTrue();
    }

    [Fact]
    public void Calculate_CountsEachPeriodsCreatedClosedAndOpenAtItsEnd()
    {
        // Arrange: a 90-day range splits into two-week periods; the first runs Jul 1 to Jul 14.
        var items = new[]
        {
            Dependency(Payments, Mobile, On(6, 1)),                 // open throughout
            Dependency(Payments, Data, On(7, 3), On(7, 20)),        // created in the first, closed in the second
            Dependency(Mobile, Data, On(7, 16)),                    // created in the second
        };

        // Act
        var result = Calculate(items);

        // Assert
        result.Periods.Should().HaveCount(7);
        result.Periods.Take(2).Should().BeEquivalentTo(new[]
        {
            new DependencyAnalysisPeriodDto { Start = From, End = From.PlusDays(13), Created = 1, Closed = 0, Open = 2 },
            new DependencyAnalysisPeriodDto { Start = From.PlusDays(14), End = From.PlusDays(27), Created = 1, Closed = 1, Open = 2 },
        }, o => o.WithStrictOrdering());
        result.Periods[^1].End.Should().Be(To);
        result.Periods[^1].Open.Should().Be(2);
    }

    [Fact]
    public void Calculate_LinkAddedAfterItsPredecessorWasDone_ClosesAsItOpens()
    {
        // Arrange: the predecessor was done before the range began, but the link was made inside it.
        var addedLate = Dependency(Payments, Mobile, On(8, 10), On(6, 15));

        // Act
        var result = Calculate(addedLate);

        // Assert
        result.Summary.Should().BeEquivalentTo(new DependencyAnalysisSummaryDto
        {
            Dependencies = 1,
            Created = 1,
            Closed = 1,
            OpenAtEnd = 0,
            TeamsInvolved = 2,
            TeamsOutside = 0,
        });
        result.Periods.Sum(p => p.Open).Should().Be(0);
    }

    [Fact]
    public void Calculate_ClosedDependency_ReadsAsClosedRatherThanItsStoredHealth()
    {
        // Arrange
        var open = Dependency(Payments, Mobile, On(7, 1));
        var closed = Dependency(Payments, Data, On(7, 2), On(8, 1));

        // Act
        var result = Calculate(open, closed);

        // Assert
        result.Dependencies.Select(d => (d.Id, d.Health.Name)).Should().Equal(
            (open.Dependency.Id, "Healthy"),
            (closed.Dependency.Id, "Closed"));
    }
}
