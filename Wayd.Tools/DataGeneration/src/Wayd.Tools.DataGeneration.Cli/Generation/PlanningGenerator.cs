using System.Globalization;
using Bogus;
using static Wayd.Tools.DataGeneration.Cli.Generation.PlanningVocabulary;

namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// Generates the planning history of each ART: back-to-back planning intervals rostered with the ART and its
/// teams, the objectives each team committed to in them, and the risks it raised along the way.
/// <para>
/// Intervals are run per ART, but every ART plans on the company's quarterly calendar, so their intervals
/// share dates and differ by name. A planning year starts on the last Monday of January — companies rarely
/// plan over the holidays, or before the year's funding is settled — and holds four
/// intervals of whole iterations — six, seven, six and seven two-week iterations fill 52 weeks — so each
/// interval starts at about the same time every year. Past intervals are finished: every objective closed,
/// most risks closed. The current one is in flight, its objectives as far along as the days elapsed suggest.
/// The next one is scheduled with its roster and nothing committed yet, since objectives are set at planning.
/// </para>
/// <para>
/// Teams keep more of their commitments as the history approaches today, so predictability trends upward
/// rather than sitting flat.
/// </para>
/// </summary>
public sealed class PlanningGenerator
{
    /// <summary>The area name this generator draws its seed under.</summary>
    public const string AreaName = "planning";

    private readonly OrgStructure _org;
    private readonly ProductCatalog _catalog;
    private readonly PlanningOptions _options;
    private readonly GenerationContext _context;
    private readonly Faker _faker;

    private readonly List<PlanningIntervalModel> _intervals = [];
    private readonly List<PlanningIntervalObjectiveModel> _objectives = [];
    private readonly List<RiskModel> _risks = [];

    public PlanningGenerator(OrgStructure org, PlanningOptions options, GenerationContext context)
    {
        _org = org;
        _options = options;
        _context = context;

        // Derived rather than passed in, like the PPM generator's copy, so objectives name the same components
        // the catalog holds whether or not the catalog itself is seeded.
        _catalog = ProductCatalog.From(org, context);

        // Derived from the area name, so adding another generator does not shift this one's data.
        _faker = new Faker { Random = new Randomizer(context.SeedFor(AreaName)) };
    }

    private DateOnly HistoryStart => _context.WindowStart > _context.FoundedOn ? _context.WindowStart : _context.FoundedOn;

    private DateOnly Today => _context.AsOf;

    private int IterationDays => _options.IterationWeeks * 7;

    /// <summary>The quarters that take a year's spare iterations, in the order they take them.</summary>
    private static readonly int[] _longerQuarters = [1, 3, 0, 2];

    public GeneratedPlanning Generate()
    {
        var cadence = Cadence();

        foreach (var art in _org.ValueStreams.SelectMany(v => v.Arts))
            BuildArt(art, cadence);

        return new GeneratedPlanning(_intervals, _objectives, _risks);
    }

    /// <summary>
    /// One slot on the planning calendar. <see cref="DevelopmentIterations"/> counts the iterations before the
    /// Innovation and Planning one, which Wayd makes of whatever iteration reaches the interval's end.
    /// </summary>
    private sealed record Slot(int Year, int Number, DateOnly Start, DateOnly End, int DevelopmentIterations);

    private sealed record Interval(string Name, DateOnly Start, DateOnly End, int DevelopmentIterations);

    // ---- Cadence ------------------------------------------------------------------------------

    /// <summary>
    /// Every interval on the calendar a seed covers: those starting within the history, the one in progress
    /// today, and the next one after it when the runway reaches that far.
    /// </summary>
    private List<Slot> Cadence()
    {
        List<Slot> calendar = [];
        for (var year = HistoryStart.Year - 1; year <= Today.Year + 1; year++)
            calendar.AddRange(PlanningYear(year));

        var current = calendar.FindIndex(s => s.Start <= Today && s.End >= Today);

        return [.. calendar.Where((slot, index) =>
            (index < current && slot.Start >= HistoryStart)
            || index == current
            || (index == current + 1 && slot.End <= _context.WindowEnd))];
    }

    /// <summary>
    /// A planning year's four intervals, running back to back from its last Monday of January to the day
    /// before the next year's.
    /// </summary>
    /// <remarks>
    /// Whole iterations are shared out as evenly as they go, the spares landing on the second and fourth
    /// intervals. A year is 52 weeks or, now and then, 53; weeks that do not make a whole iteration go to the
    /// last interval, whose final iteration Wayd then cuts short.
    /// </remarks>
    private IEnumerable<Slot> PlanningYear(int year)
    {
        var start = LastMondayOfJanuary(year);
        var nextYear = LastMondayOfJanuary(year + 1);
        var iterations = (nextYear.DayNumber - start.DayNumber) / IterationDays;

        for (var quarter = 0; quarter < 4; quarter++)
        {
            var count = iterations / 4 + (Array.IndexOf(_longerQuarters, quarter) < iterations % 4 ? 1 : 0);
            var end = quarter == 3 ? nextYear.AddDays(-1) : start.AddDays(count * IterationDays - 1);
            var truncated = (end.DayNumber - start.DayNumber + 1) % IterationDays != 0;

            yield return new Slot(year, quarter + 1, start, end, truncated ? count : count - 1);

            start = end.AddDays(1);
        }
    }

    private void BuildArt(ArtNode art, IReadOnlyList<Slot> cadence)
    {
        // How reliably each team delivers what it commits to. Fixed per team, so predictability differs
        // between teams and not just between intervals.
        var reliability = art.Teams.ToDictionary(t => t.TeamCode, _ => _faker.Random.Double(0.72, 1.0), StringComparer.OrdinalIgnoreCase);

        string[] roster = [art.TeamCode, .. art.Teams.Select(t => t.TeamCode)];

        foreach (var slot in cadence)
        {
            var interval = AddInterval(art, slot, roster);

            // Objectives are set at planning, which has not happened for an interval that has not started.
            if (interval.Start > Today)
                continue;

            foreach (var team in art.Teams)
            {
                var count = Math.Max(1, _options.ObjectivesPerTeam + _faker.Random.Int(-1, 1));
                BuildObjectives(interval, team.TeamCode, count, reliability[team.TeamCode], used => TeamObjectiveName(team.TeamCode, interval.Start, used));
                BuildRisks(interval, team, art);
            }
        }
    }

    private Interval AddInterval(ArtNode art, Slot slot, IReadOnlyList<string> roster)
    {
        var name = $"{art.Name} PI {slot.Year}.{slot.Number}";

        _intervals.Add(new PlanningIntervalModel
        {
            Name = name,
            Description = $"The {art.Name}'s planning interval from {slot.Start.ToString("MMMM d", CultureInfo.InvariantCulture)} to {slot.End.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture)}.",
            Start = slot.Start,
            End = slot.End,
            IterationWeeks = _options.IterationWeeks,
            IterationPrefix = $"{slot.Year % 100:D2}.{slot.Number}.",
            ArtCode = art.TeamCode,
            TeamCodes = string.Join(';', roster),
        });

        return new Interval(name, slot.Start, slot.End, slot.DevelopmentIterations);
    }

    // ---- Objectives ---------------------------------------------------------------------------

    private void BuildObjectives(Interval interval, string teamCode, int count, double reliability, Func<HashSet<string>, string> name)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Half the teams that plan three or more hold the last back as a stretch objective.
        var stretch = count >= 3 && _faker.Random.Bool();

        for (var order = 1; order <= count; order++)
        {
            var isStretch = stretch && order == count;
            var (startDate, targetDate) = Window(interval);
            var (status, progress, closedAt) = interval.End < Today
                ? Finished(interval, startDate, targetDate, CompletionChance(interval, reliability, isStretch))
                : InFlight(startDate, targetDate, CompletionChance(interval, reliability, isStretch));

            _objectives.Add(new PlanningIntervalObjectiveModel
            {
                PlanningIntervalName = interval.Name,
                TeamCode = teamCode,
                Name = name(used),
                Description = Pick(ObjectiveMeasures),
                Status = status,
                Progress = progress,
                StartDate = startDate,
                TargetDate = targetDate,
                IsStretch = isStretch,
                ClosedAt = closedAt,
                Order = order,
            });
        }
    }

    /// <summary>
    /// When an objective is worked: from the start of one development iteration to the end of the same or a
    /// later one. The last iteration is Innovation and Planning, so nothing targets it.
    /// </summary>
    private (DateOnly Start, DateOnly Target) Window(Interval interval)
    {
        var developmentIterations = Math.Max(1, interval.DevelopmentIterations);

        // The later of two draws, since more objectives land toward the end of an interval than the start.
        var target = Math.Max(_faker.Random.Int(0, developmentIterations - 1), _faker.Random.Int(0, developmentIterations - 1));
        var start = _faker.Random.Int(0, target);

        return (interval.Start.AddDays(start * IterationDays), interval.Start.AddDays((target + 1) * IterationDays - 1));
    }

    /// <summary>
    /// The chance a team completes an objective: its own reliability, rising from 0.78× at the start of the
    /// history to 0.95× by today, and roughly halved for a stretch objective.
    /// </summary>
    private double CompletionChance(Interval interval, double reliability, bool isStretch)
    {
        var chance = Math.Clamp(reliability * (0.78 + 0.17 * Progress(interval.Start)), 0.4, 0.97);
        return isStretch ? chance * 0.55 : chance;
    }

    /// <summary>An objective of an interval that has ended: completed, missed at the end, or canceled partway.</summary>
    private (string Status, double Progress, DateTimeOffset? ClosedAt) Finished(Interval interval, DateOnly start, DateOnly target, double completionChance)
    {
        if (_faker.Random.Double() < completionChance)
        {
            var completedOn = Between(Later(start.AddDays(1), target.AddDays(-5)), Earlier(target.AddDays(3), interval.End));
            return (Completed, 100, ClosedAt(completedOn));
        }

        if (_faker.Random.Double() < 0.2)
            return (Canceled, Percent(0, 50), ClosedAt(Between(start.AddDays(3), interval.End)));

        return (Missed, Percent(35, 90), ClosedAt(PreviousWorkday(interval.End)));
    }

    /// <summary>
    /// An objective of the interval in progress, as far along as the days since it started suggest. Anything
    /// closed was closed before today.
    /// </summary>
    private (string Status, double Progress, DateTimeOffset? ClosedAt) InFlight(DateOnly start, DateOnly target, double completionChance)
    {
        var elapsedDays = Today.DayNumber - start.DayNumber;
        if (elapsedDays <= 0)
            return (NotStarted, 0, null);

        if (elapsedDays > 7 && _faker.Random.Double() < 0.03)
            return (Canceled, Percent(0, 40), ClosedAt(Between(start.AddDays(3), Today.AddDays(-1))));

        if (target < Today)
        {
            return _faker.Random.Double() < completionChance
                ? (Completed, 100, ClosedAt(Between(Later(start.AddDays(1), target.AddDays(-5)), Earlier(target.AddDays(2), Today.AddDays(-1)))))
                : (InProgress, Percent(60, 95), null);
        }

        var expected = elapsedDays / (double)(target.DayNumber - start.DayNumber);

        if (expected > 0.6 && elapsedDays >= 2 && _faker.Random.Double() < 0.15 * completionChance)
            return (Completed, 100, ClosedAt(Between(Later(start.AddDays(1), Today.AddDays(-7)), Today.AddDays(-1))));

        return (InProgress, Math.Clamp(Math.Round(expected * 100 * _faker.Random.Double(0.6, 1.15) / 5) * 5, 5, 95), null);
    }

    private string TeamObjectiveName(string teamCode, DateOnly on, HashSet<string> used) => Unique(used, () =>
    {
        var components = CustomerFacing(teamCode, on);
        if (components.Count == 0 || _faker.Random.Double() < 0.2)
            return Format(Pick(TeamObjectives), Pick(Features));

        var component = _faker.PickRandom(components).Name;
        if (_faker.Random.Double() < 0.6)
            return Format(Pick(ComponentObjectives), component, Pick(Features));

        var template = Pick(ComponentImprovements);
        return Format(template, component, template.StartsWith("Migrate", StringComparison.Ordinal) ? Pick(Platforms) : Pick(Improvements));
    });

    /// <summary>
    /// What a team runs that customers use. Libraries and tools are left out: an objective to launch a feature
    /// for a client SDK's customers does not read as anything a team would plan.
    /// </summary>
    private List<ComponentPlan> CustomerFacing(string teamCode, DateOnly on) =>
        [.. _catalog.ComponentsOf(teamCode, on).Where(c => c.Kind is not (ComponentKind.Library or ComponentKind.Tool))];

    /// <summary>A name not already used by the same team in the same interval, if a few draws can find one.</summary>
    private static string Unique(HashSet<string> used, Func<string> draw)
    {
        var name = draw();
        for (var attempt = 0; attempt < 5 && used.Contains(name); attempt++)
            name = draw();

        used.Add(name);
        return name;
    }

    // ---- Risks --------------------------------------------------------------------------------

    /// <summary>
    /// The risks one team raised during an interval, each reported on a day that has passed. Most are closed
    /// within a few weeks; the rest stay open, owned or accepted, with a follow-up ahead.
    /// </summary>
    private void BuildRisks(Interval interval, TeamNode team, ArtNode art)
    {
        var lastDay = Earlier(interval.End, Today.AddDays(-1));
        if (lastDay < interval.Start)
            return;

        List<string> people = [.. team.MemberEmployeeNumbers];
        foreach (var lead in new[] { team.EngineeringManagerEmployeeNumber, team.ProductOwnerEmployeeNumber })
        {
            if (lead is not null && !people.Contains(lead, StringComparer.OrdinalIgnoreCase))
                people.Add(lead);
        }

        if (people.Count == 0)
            return;

        var expected = _options.RisksPerTeam * _faker.Random.Double(0.5, 1.5);
        var count = (int)Math.Floor(expected) + (_faker.Random.Double() < expected - Math.Floor(expected) ? 1 : 0);

        for (var i = 0; i < count; i++)
        {
            var reportedOn = Between(interval.Start, lastDay);
            // A few stay open past when they would have closed, but not for months: a register full of risks
            // nobody has touched since an interval long finished reads as abandoned, not managed.
            var closeOn = reportedOn.AddDays(_faker.Random.Int(3, 45));
            var closes = closeOn < Today && (_faker.Random.Double() < 0.88 || Today.DayNumber - closeOn.DayNumber > 90);

            var category = closes
                ? _faker.Random.WeightedRandom([Resolved, Mitigated, Accepted, Owned], [0.4f, 0.35f, 0.2f, 0.05f])
                : _faker.Random.WeightedRandom([Owned, Mitigated, Accepted], [0.5f, 0.3f, 0.2f]);

            _risks.Add(new RiskModel
            {
                Handle = $"RISK-{_risks.Count + 1:D5}",
                TeamCode = team.TeamCode,
                Summary = RiskSummary(team, art, interval.Start),
                Description = Pick(RiskDescriptions),
                ReportedAt = At(reportedOn, _faker.Random.Int(9, 16)),
                ReportedByEmployeeNumber = _faker.PickRandom(people),
                Status = closes ? Closed : Open,
                Category = category,
                Impact = Grade(),
                Likelihood = Grade(),
                AssigneeEmployeeNumber = _faker.Random.Double() < (closes ? 0.6 : 0.85) ? _faker.PickRandom(people) : null,
                FollowUpDate = closes ? null : Workday(Today.AddDays(_faker.Random.Int(3, 21))),
                Response = closes || _faker.Random.Bool() ? Pick(RiskResponses[category]) : null,
                ClosedAt = closes ? At(closeOn, _faker.Random.Int(10, 17)) : null,
            });
        }
    }

    private string RiskSummary(TeamNode team, ArtNode art, DateOnly on)
    {
        var roll = _faker.Random.Double();

        var neighbours = art.Teams.Where(t => t.TeamCode != team.TeamCode).ToList();
        if (roll < 0.35 && neighbours.Count > 0)
            return Format(Pick(DependencyRisks), _faker.PickRandom(neighbours).Name);

        var components = CustomerFacing(team.TeamCode, on);
        if (roll < 0.7 && components.Count > 0)
            return Format(Pick(ComponentRisks), _faker.PickRandom(components).Name);

        return Pick(GeneralRisks);
    }

    private string Grade() => _faker.Random.WeightedRandom([Low, Medium, High], [0.35f, 0.45f, 0.2f]);

    // ---- Timeline -----------------------------------------------------------------------------

    /// <summary>How far through the history a date sits, from 0 at its start to 1 at today and after.</summary>
    private double Progress(DateOnly date)
    {
        var span = Today.DayNumber - HistoryStart.DayNumber;
        return span <= 0 ? 1 : Math.Clamp((date.DayNumber - HistoryStart.DayNumber) / (double)span, 0, 1);
    }

    /// <summary>A day in the range, or its start when the range is empty.</summary>
    private DateOnly Between(DateOnly from, DateOnly to) =>
        to <= from ? from : from.AddDays(_faker.Random.Int(0, to.DayNumber - from.DayNumber));

    private DateTimeOffset ClosedAt(DateOnly day) => At(day, _faker.Random.Int(15, 18));

    private DateTimeOffset At(DateOnly day, int hour) =>
        new(day.ToDateTime(new TimeOnly(hour, _faker.Random.Int(0, 59))), TimeSpan.Zero);

    private double Percent(int min, int max) => Math.Round(_faker.Random.Int(min, max) / 5.0) * 5;

    private static DateOnly LastMondayOfJanuary(int year)
    {
        var last = new DateOnly(year, 1, 31);
        return last.AddDays(-(((int)last.DayOfWeek + 6) % 7));
    }

    private static DateOnly Workday(DateOnly date) => date.DayOfWeek switch
    {
        DayOfWeek.Saturday => date.AddDays(2),
        DayOfWeek.Sunday => date.AddDays(1),
        _ => date,
    };

    private static DateOnly PreviousWorkday(DateOnly date) => date.DayOfWeek switch
    {
        DayOfWeek.Saturday => date.AddDays(-1),
        DayOfWeek.Sunday => date.AddDays(-2),
        _ => date,
    };

    private static DateOnly Earlier(DateOnly a, DateOnly b) => a < b ? a : b;

    private static DateOnly Later(DateOnly a, DateOnly b) => a > b ? a : b;

    private static string Format(string template, params object[] values) =>
        string.Format(CultureInfo.InvariantCulture, template, values);

    private string Pick(string[] pool) => _faker.PickRandom(pool);
}
