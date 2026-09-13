using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli.Generation;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

/// <summary>
/// The generated planning history, checked against the rules the Planning imports enforce and the shape the
/// planning pages read. Every import is atomic, so one row breaking one of these fails a whole file at seed
/// time — far from the generator code that produced it.
/// </summary>
public class PlanningGeneratorTests
{
    private static readonly DateOnly _asOf = new(2026, 6, 15);

    private static readonly string[] _closedStatuses = ["Completed", "Canceled", "Missed"];

    private static GenerationContext Context(int seed = 1234) => new() { AsOf = _asOf, Seed = seed };

    private static (GeneratedOrg Org, GeneratedPlanning Planning) Generate(
        PlanningOptions? options = null, OrgOptions? orgOptions = null, int seed = 1234)
    {
        var context = Context(seed);
        var org = new OrgGenerator(orgOptions ?? new OrgOptions { ValueStreams = 3, Teams = 18 }, context).Generate();

        return (org, new PlanningGenerator(org.Structure, options ?? new PlanningOptions(), context).Generate());
    }

    private static readonly (GeneratedOrg Org, GeneratedPlanning Planning) _default = Generate();

    private static GeneratedPlanning Data => _default.Planning;

    private static DateTimeOffset StartOfToday => new(_asOf.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    [Fact]
    public void Generate_ProducesEveryKindOfRecord()
    {
        // Arrange & Act
        var data = Data;

        // Assert — an empty set here is a seed that skips an area and demonstrates nothing
        data.PlanningIntervals.Should().NotBeEmpty();
        data.Objectives.Should().NotBeEmpty();
        data.Risks.Should().NotBeEmpty();
    }

    [Fact]
    public void Generate_NamesEveryIntervalUniquelyWithinTheImportLimits()
    {
        // Arrange — a name is unique across every interval in the environment, not only within an ART
        var intervals = Data.PlanningIntervals;

        // Act & Assert
        intervals.Select(p => p.Name).Should().OnlyHaveUniqueItems(p => p.ToUpperInvariant());
        intervals.Should().OnlyContain(p => p.Name.Length <= 128);
        intervals.Should().OnlyContain(p => p.Description.Length <= 2048);
        intervals.Should().OnlyContain(p => p.IterationPrefix.Length <= 32);
        intervals.Should().OnlyContain(p => p.End >= p.Start && p.IterationWeeks > 0);
    }

    [Fact]
    public void Generate_RunsEachArtsIntervalsBackToBack()
    {
        // Arrange
        var byArt = Data.PlanningIntervals.GroupBy(p => p.ArtCode).ToList();

        // Act
        var gaps = byArt
            .SelectMany(art => art.OrderBy(p => p.Start).Zip(art.OrderBy(p => p.Start).Skip(1)))
            .Where(pair => pair.Second.Start != pair.First.End.AddDays(1))
            .ToList();

        // Assert
        byArt.Should().HaveCount(_default.Org.Structure.ValueStreams.Sum(v => v.Arts.Count));
        gaps.Should().BeEmpty();
    }

    [Fact]
    public void Generate_PlansEveryArtOnOneCalendar()
    {
        // Arrange
        var cadences = Data.PlanningIntervals.GroupBy(p => p.ArtCode).Select(art => art.Select(p => p.Start).ToList()).ToList();

        // Act & Assert
        cadences.Should().AllSatisfy(c => c.Should().Equal(cadences[0]));
    }

    [Theory]
    [InlineData(2025, 14)]
    [InlineData(2027, 15)]
    public void Generate_RunsFourQuarterlyIntervalsFromTheLastMondayOfEachJanuary(int year, int lastQuarterWeeks)
    {
        // Arrange — six, seven, six and seven two-week iterations, so each interval starts at about the same
        // time every year. 2027 is a 53-week planning year (Jan 25 2027 to Jan 30 2028), and the spare week
        // goes to its last interval.
        var context = new GenerationContext { AsOf = new DateOnly(2029, 6, 15), Seed = 1234, HistoryYears = 5 };
        var org = new OrgGenerator(new OrgOptions { ValueStreams = 1, Teams = 3 }, context).Generate();
        var lastMonday = Enumerable.Range(25, 7).Select(d => new DateOnly(year, 1, d)).Single(d => d.DayOfWeek == DayOfWeek.Monday);

        // Act
        var quarters = new PlanningGenerator(org.Structure, new PlanningOptions(), context).Generate()
            .PlanningIntervals
            .Where(p => p.Name.Contains($" PI {year}.", StringComparison.Ordinal))
            .OrderBy(p => p.Start)
            .ToList();

        // Assert
        quarters.Select(p => p.Name[^1..]).Should().Equal("1", "2", "3", "4");
        quarters[0].Start.Should().Be(lastMonday);
        quarters.Select(p => (p.End.DayNumber - p.Start.DayNumber + 1) / 7).Take(3).Should().Equal(12, 14, 12);
        ((quarters[3].End.DayNumber - quarters[3].Start.DayNumber + 1) / 7).Should().Be(lastQuarterWeeks);

        var nextYear = quarters[3].End.AddDays(1);
        nextYear.DayOfWeek.Should().Be(DayOfWeek.Monday);
        (nextYear.Year, nextYear.Month).Should().Be((year + 1, 1));
        nextYear.Day.Should().BeGreaterThanOrEqualTo(25);
    }

    [Fact]
    public void Generate_RostersEachIntervalWithItsArtAndTheArtsTeams()
    {
        // Arrange
        var arts = _default.Org.Structure.ValueStreams.SelectMany(v => v.Arts).ToDictionary(a => a.TeamCode!);

        // Act & Assert
        Data.PlanningIntervals.Should().OnlyContain(p =>
            p.TeamCodes.Split(';', StringSplitOptions.None).ToHashSet().SetEquals(
                arts[p.ArtCode!].Teams.Select(t => t.TeamCode).Append(p.ArtCode!)));
    }

    [Fact]
    public void Generate_KeepsOneIntervalInProgressTodayAndOnlyOneAhead()
    {
        // Arrange
        var byArt = Data.PlanningIntervals.GroupBy(p => p.ArtCode).ToList();

        // Act & Assert
        byArt.Should().OnlyContain(art => art.Count(p => p.Start <= _asOf && p.End >= _asOf) == 1);
        byArt.Should().OnlyContain(art => art.Count(p => p.Start > _asOf) == 1);
        Data.PlanningIntervals.Should().OnlyContain(p => p.Start >= Context().WindowStart || p.End >= _asOf);
    }

    [Fact]
    public void Generate_PlacesEveryObjectiveOnTheRosterOfItsInterval()
    {
        // Arrange — the import does not check the roster, so nothing but this keeps an objective off an
        // interval its team never ran
        var rosters = Data.PlanningIntervals.ToDictionary(p => p.Name, p => p.TeamCodes.Split(';').ToHashSet());

        // Act & Assert
        Data.Objectives.Should().OnlyContain(o => rosters[o.PlanningIntervalName].Contains(o.TeamCode));
    }

    [Fact]
    public void Generate_GivesObjectivesOnlyToTeamsNeverToTheArt()
    {
        // Arrange
        var arts = _default.Org.Structure.ValueStreams.SelectMany(v => v.Arts).Select(a => a.TeamCode).ToHashSet();

        // Act & Assert
        Data.Objectives.Should().NotContain(o => arts.Contains(o.TeamCode));
    }

    [Fact]
    public void Generate_WritesEveryObjectiveTheImportAccepts()
    {
        // Arrange
        var objectives = Data.Objectives;

        // Act & Assert
        objectives.Should().OnlyContain(o => o.Name.Length > 0 && o.Name.Length <= 256);
        objectives.Should().OnlyContain(o => o.Description.Length <= 1024);
        objectives.Should().OnlyContain(o => o.Progress >= 0 && o.Progress <= 100);
        objectives.Should().OnlyContain(o => o.StartDate < o.TargetDate);
        objectives.Should().OnlyContain(o => _closedStatuses.Contains(o.Status) == (o.ClosedAt != null));
        objectives.Select(o => o.ImportId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Generate_KeepsEveryObjectiveInsideItsInterval()
    {
        // Arrange
        var intervals = Data.PlanningIntervals.ToDictionary(p => p.Name);

        // Act & Assert
        Data.Objectives.Should().OnlyContain(o =>
            o.StartDate >= intervals[o.PlanningIntervalName].Start && o.TargetDate <= intervals[o.PlanningIntervalName].End);
        Data.Objectives.Where(o => o.ClosedAt != null).Should().OnlyContain(o =>
            DateOnly.FromDateTime(o.ClosedAt!.Value.UtcDateTime) >= o.StartDate
            && DateOnly.FromDateTime(o.ClosedAt.Value.UtcDateTime) <= intervals[o.PlanningIntervalName].End);
    }

    [Fact]
    public void Generate_ClosesEveryObjectiveOfAnIntervalThatHasEnded()
    {
        // Arrange
        var ended = Data.PlanningIntervals.Where(p => p.End < _asOf).Select(p => p.Name).ToHashSet();

        // Act
        var past = Data.Objectives.Where(o => ended.Contains(o.PlanningIntervalName)).ToList();

        // Assert
        past.Should().NotBeEmpty();
        past.Should().OnlyContain(o => _closedStatuses.Contains(o.Status));
    }

    [Fact]
    public void Generate_LeavesTheCurrentIntervalInFlightAndTheNextUncommitted()
    {
        // Arrange
        var current = Data.PlanningIntervals.Where(p => p.Start <= _asOf && p.End >= _asOf).Select(p => p.Name).ToHashSet();
        var upcoming = Data.PlanningIntervals.Where(p => p.Start > _asOf).Select(p => p.Name).ToHashSet();

        // Act
        var inFlight = Data.Objectives.Where(o => current.Contains(o.PlanningIntervalName)).ToList();

        // Assert — nothing is closed on a day that has not happened
        inFlight.Should().Contain(o => o.Status == "In Progress");
        inFlight.Where(o => o.ClosedAt != null).Should().OnlyContain(o => o.ClosedAt < StartOfToday);
        Data.Objectives.Should().NotContain(o => upcoming.Contains(o.PlanningIntervalName));
    }

    [Fact]
    public void Generate_ImprovesPredictabilityAcrossTheHistory()
    {
        // Arrange — the share of committed team objectives completed, earlier half against later half; a flat
        // line demonstrates nothing on the predictability views. A larger org, so teams average out.
        var (org, data) = Generate(orgOptions: new OrgOptions { ValueStreams = 4, Teams = 40 });
        var leaves = org.Structure.ValueStreams.SelectMany(v => v.Arts).SelectMany(a => a.Teams).Select(t => t.TeamCode).ToHashSet();
        var intervals = data.PlanningIntervals.ToDictionary(p => p.Name);
        var midpoint = _asOf.AddYears(-1);

        var committed = data.Objectives
            .Where(o => leaves.Contains(o.TeamCode) && !o.IsStretch && intervals[o.PlanningIntervalName].End < _asOf)
            .ToList();

        static double Predictability(IEnumerable<PlanningIntervalObjectiveModel> objectives)
        {
            var list = objectives.ToList();
            return list.Count(o => o.Status == "Completed") / (double)list.Count;
        }

        // Act
        var earlier = Predictability(committed.Where(o => intervals[o.PlanningIntervalName].Start < midpoint));
        var later = Predictability(committed.Where(o => intervals[o.PlanningIntervalName].Start >= midpoint));

        // Assert
        later.Should().BeGreaterThan(earlier);
    }

    [Fact]
    public void Generate_ReportsEveryRiskInThePastBySomeoneOnTheTeam()
    {
        // Arrange — the import refuses a report or a close dated after the moment it runs
        // — someone who held a position on the team, whether or not they still do
        var structure = _default.Org.Structure;
        var teams = structure.ValueStreams.SelectMany(v => v.Arts).SelectMany(a => a.Teams).ToDictionary(t => t.TeamCode);
        string TodaysHolder(string employeeNumber) => structure.Positions![employeeNumber][^1].EmployeeNumber;

        Func<string, string, bool> onTeam = (teamCode, employeeNumber) =>
            teams[teamCode].MemberEmployeeNumbers.Contains(TodaysHolder(employeeNumber))
            || teams[teamCode].EngineeringManagerEmployeeNumber == TodaysHolder(employeeNumber)
            || teams[teamCode].ProductOwnerEmployeeNumber == TodaysHolder(employeeNumber);

        // Act & Assert
        Data.Risks.Should().OnlyContain(r => r.ReportedAt < StartOfToday);
        Data.Risks.Should().OnlyContain(r => onTeam(r.TeamCode, r.ReportedByEmployeeNumber));
        Data.Risks.Should().OnlyContain(r => r.AssigneeEmployeeNumber == null || onTeam(r.TeamCode, r.AssigneeEmployeeNumber));
    }

    [Fact]
    public void Generate_WritesEveryRiskTheImportAccepts()
    {
        // Arrange
        var risks = Data.Risks;

        // Act & Assert
        risks.Should().OnlyContain(r => r.Summary.Length > 0 && r.Summary.Length <= 256);
        risks.Should().OnlyContain(r => r.Description.Length <= 1024 && (r.Response == null || r.Response.Length <= 1024));
        risks.Should().OnlyContain(r => (r.Status == "Closed") == (r.ClosedAt != null));
        risks.Where(r => r.ClosedAt != null).Should().OnlyContain(r => r.ClosedAt > r.ReportedAt && r.ClosedAt < StartOfToday);
        risks.Should().OnlyContain(r => r.Status == "Closed" ? r.FollowUpDate == null : r.FollowUpDate > _asOf);
        risks.Select(r => r.Handle).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Generate_ClosesMostRisksAndLeavesSomeOpen()
    {
        // Arrange & Act
        var closed = Data.Risks.Count(r => r.Status == "Closed");

        // Assert — both halves of the register have something in them, and nothing open has been left
        // untouched since an interval long finished
        closed.Should().BeGreaterThan(Data.Risks.Count / 2);
        Data.Risks.Should().Contain(r => r.Status == "Open");
        Data.Risks.Where(r => r.Status == "Open").Should().OnlyContain(r => r.ReportedAt > StartOfToday.AddDays(-140));
    }

    [Fact]
    public void Generate_ProducesNoRisksWhenTeamsRaiseNone()
    {
        // Arrange & Act
        var (_, data) = Generate(new PlanningOptions { RisksPerTeam = 0 });

        // Assert
        data.Risks.Should().BeEmpty();
        data.Objectives.Should().NotBeEmpty();
    }

    [Fact]
    public void Generate_PlansNothingAheadWithoutARunway()
    {
        // Arrange
        var context = new GenerationContext { AsOf = _asOf, Seed = 1234, RunwayYears = 0 };
        var org = new OrgGenerator(new OrgOptions { ValueStreams = 2, Teams = 8 }, context).Generate();

        // Act
        var data = new PlanningGenerator(org.Structure, new PlanningOptions(), context).Generate();

        // Assert — the interval in progress still runs past today, but none is scheduled after it
        data.PlanningIntervals.Should().NotContain(p => p.Start > _asOf);
    }

    [Fact]
    public void Generate_ProducesNothingForAnOrgWithNoArts()
    {
        // Arrange
        var org = new OrgStructure([new ValueStreamNode("Payments", null, null, null, [])]);

        // Act
        var data = new PlanningGenerator(org, new PlanningOptions(), Context()).Generate();

        // Assert
        data.PlanningIntervals.Should().BeEmpty();
        data.Objectives.Should().BeEmpty();
        data.Risks.Should().BeEmpty();
    }

    [Fact]
    public void Generate_ProducesTheSameDataForTheSameSeedAndDate()
    {
        // Arrange & Act
        var (_, first) = Generate(seed: 99);
        var (_, second) = Generate(seed: 99);

        // Assert
        second.PlanningIntervals.Select(p => p.Name).Should().Equal(first.PlanningIntervals.Select(p => p.Name));
        second.Objectives.Select(o => $"{o.ImportId}|{o.Status}|{o.Name}").Should().Equal(first.Objectives.Select(o => $"{o.ImportId}|{o.Status}|{o.Name}"));
        second.Risks.Select(r => $"{r.Handle}|{r.Summary}").Should().Equal(first.Risks.Select(r => $"{r.Handle}|{r.Summary}"));
    }
}
