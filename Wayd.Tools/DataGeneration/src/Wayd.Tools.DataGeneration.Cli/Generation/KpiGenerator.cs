using Bogus;
using static Wayd.Tools.DataGeneration.Cli.Generation.KpiVocabulary;

namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// Generates the KPIs each delivery team and each ART is measured on, with a monthly history from when the
/// current team structure formed — or the history window opened, whichever is later — to the last whole month.
/// <para>
/// A KPI's baseline is the average of its first three months, taken over those months, and its target is set
/// against that team's own baseline, so every goal holds whatever the team started from. Each KPI then moves
/// toward its target at its own pace with month-to-month noise: some teams overshoot, most get part of the way,
/// so a scorecard shows healthy, at-risk and unhealthy KPIs side by side. A few KPIs were dropped partway and
/// arrive closed, their history kept.
/// </para>
/// <para>
/// Each month's value is recorded the way its KPI's window takes it: for the month itself when per period; as of the
/// month's last day when rolling or cumulative, a cumulative KPI counting from the month after its baseline; and as
/// two readings, on the 1st and the 15th, when point in time.
/// </para>
/// <para>
/// Some KPIs carry a quarterly checkpoint plan from the end of their baseline to a quarter past the last month,
/// stepping evenly from the baseline to the target, with an at-risk value on each checkpoint when the KPI has an
/// at-risk threshold. The plan draws from a random stream of its own, so the rest of the data is the same with or
/// without it.
/// </para>
/// <para>
/// When PPM was generated, every active or completed portfolio, program and project carries
/// <see cref="KpisPerPpmRecord"/> KPIs too, measured monthly from the month after it was activated to the month it
/// closed, or the last whole month; a record live too briefly carries none. They draw from a random stream of their
/// own, so the teams' KPIs are the same with or without PPM, and they add the delivery and benefits categories.
/// Every active or completed strategic initiative is measured the same way from its start, from a stream of its own
/// again, so the portfolios', programs' and projects' KPIs are the same with or without it.
/// </para>
/// </summary>
public sealed class KpiGenerator
{
    /// <summary>The area name this generator draws its seed under.</summary>
    public const string AreaName = "kpis";

    /// <summary>The months a baseline is taken over.</summary>
    private const int BaselineMonths = 3;

    /// <summary>A KPI needs its baseline and at least a few months after it to be worth showing.</summary>
    private const int MinimumMonths = BaselineMonths + 3;

    /// <summary>The share of KPIs that were dropped partway and arrive closed.</summary>
    private const double ClosedFraction = 0.12;

    /// <summary>The hour, in UTC, a point-in-time reading is taken: mid-morning in the Americas, afternoon in Europe.</summary>
    private const int ReadingHourUtc = 15;

    /// <summary>The share of KPIs that carry a checkpoint plan.</summary>
    private const double PlannedFraction = 0.4;

    /// <summary>How far short of each planned value its at-risk value sits, as a share of the whole planned move.</summary>
    private const double AtRiskMargin = 0.15;

    /// <summary>How many KPIs each portfolio, program and project carries.</summary>
    public const int KpisPerPpmRecord = 2;

    private readonly OrgStructure _org;
    private readonly GeneratedPpm? _ppm;
    private readonly KpiOptions _options;
    private readonly GenerationContext _context;
    private readonly Faker _faker;
    private readonly Faker _planFaker;
    private readonly Faker _ppmFaker;
    private readonly Faker _initiativeFaker;

    private readonly List<KpiModel> _kpis = [];
    private readonly List<KpiMeasurementModel> _measurements = [];
    private readonly List<KpiCheckpointModel> _checkpoints = [];

    /// <param name="ppm">The generated PPM records, whose KPIs are generated too; null when PPM was not generated.</param>
    public KpiGenerator(OrgStructure org, KpiOptions options, GenerationContext context, GeneratedPpm? ppm = null)
    {
        _org = org;
        _ppm = ppm;
        _options = options;
        _context = context;

        // Derived from the area name, so adding another generator does not shift this one's data.
        _faker = new Faker { Random = new Randomizer(context.SeedFor(AreaName)) };
        _planFaker = new Faker { Random = new Randomizer(context.SeedFor($"{AreaName}.checkpoints")) };
        _ppmFaker = new Faker { Random = new Randomizer(context.SeedFor($"{AreaName}.ppm")) };
        _initiativeFaker = new Faker { Random = new Randomizer(context.SeedFor($"{AreaName}.initiatives")) };
    }

    public GeneratedKpis Generate()
    {
        var months = Months();
        if (months.Count < MinimumMonths || _options.KpisPerTeam <= 0)
            return new GeneratedKpis(Categories, [], [], []);

        foreach (var valueStream in _org.ValueStreams)
        {
            foreach (var art in valueStream.Arts)
            {
                if (art.TeamCode is { } artCode)
                    Measure(KpiSubject.Team, artCode, ArtKpis, Math.Max(1, _options.KpisPerTeam / 2), months, _faker);

                foreach (var team in art.Teams)
                    Measure(KpiSubject.Team, team.TeamCode, TeamKpis, _options.KpisPerTeam, months, _faker);
            }
        }

        if (_ppm is { } ppm)
            MeasurePpm(ppm, months);

        IReadOnlyList<string> categories = _kpis.Any(k => k.Subject != KpiSubject.Team)
            ? [.. Categories, .. PpmCategories]
            : Categories;

        return new GeneratedKpis(categories, _kpis, _measurements, _checkpoints);
    }

    /// <summary>
    /// The KPIs of every portfolio, program, project and strategic initiative that went live, over the months it was
    /// live. A canceled record, and one never activated, carries none.
    /// </summary>
    private void MeasurePpm(GeneratedPpm ppm, List<(DateOnly Start, DateOnly End)> months)
    {
        foreach (var portfolio in ppm.Portfolios)
        {
            // A portfolio records no close date, so only an active one is measured to the last month.
            if (portfolio.ActivatedOn is { } activated && portfolio.Status == "Active")
                Measure(KpiSubject.Portfolio, portfolio.Name, PortfolioKpis, KpisPerPpmRecord, Live(months, activated, null), _ppmFaker);
        }

        foreach (var program in ppm.Programs)
        {
            if (program.ActivatedOn is { } activated && program.Status is "Active" or "Completed")
                Measure(KpiSubject.Program, program.Name, ProgramKpis, KpisPerPpmRecord,
                    Live(months, activated, program.Status == "Completed" ? program.End : null), _ppmFaker);
        }

        foreach (var project in ppm.Projects)
        {
            if (project.ActivatedOn is { } activated && project.Status is "Active" or "Completed")
                Measure(KpiSubject.Project, project.Key, ProjectKpis, KpisPerPpmRecord,
                    Live(months, activated, project.Status == "Completed" ? project.ClosedOn ?? project.End : null), _ppmFaker);
        }

        // An initiative records no activation date, so it is measured from its start.
        foreach (var initiative in ppm.StrategicInitiatives)
        {
            if (initiative.Status is "Active" or "Completed")
                Measure(KpiSubject.StrategicInitiative, initiative.Name, InitiativeKpis, KpisPerPpmRecord,
                    Live(months, initiative.Start, initiative.Status == "Completed" ? initiative.End : null), _initiativeFaker);
        }
    }

    /// <summary>The whole months after the one <paramref name="from"/> falls in, through the one <paramref name="until"/> falls in.</summary>
    private static List<(DateOnly Start, DateOnly End)> Live(List<(DateOnly Start, DateOnly End)> months, DateOnly from, DateOnly? until) =>
        [.. months.Where(m => m.Start > from && (until is not { } end || m.Start <= end))];

    /// <summary>
    /// Every whole calendar month from the later of the history window's start and the team structure's
    /// forming to the month before today's.
    /// </summary>
    private List<(DateOnly Start, DateOnly End)> Months()
    {
        var from = new[] { _context.WindowStart, _context.AsOf.AddYears(-_context.TeamStructureAgeYears), _context.FoundedOn }.Max();
        var first = new DateOnly(from.Year, from.Month, 1);
        if (first < from)
            first = first.AddMonths(1);

        var lastEnd = new DateOnly(_context.AsOf.Year, _context.AsOf.Month, 1).AddDays(-1);

        List<(DateOnly, DateOnly)> months = [];
        for (var start = first; start.AddMonths(1).AddDays(-1) <= lastEnd; start = start.AddMonths(1))
            months.Add((start, start.AddMonths(1).AddDays(-1)));

        return months;
    }

    private void Measure(KpiSubject subject, string subjectHandle, IReadOnlyList<KpiDefinition> catalogue, int count, List<(DateOnly Start, DateOnly End)> months, Faker faker)
    {
        if (months.Count < MinimumMonths)
            return;

        foreach (var definition in faker.Random.Shuffle(catalogue).Take(Math.Min(count, catalogue.Count)))
            Add(subject, subjectHandle, definition, months, faker);
    }

    private void Add(KpiSubject subject, string subjectHandle, KpiDefinition d, List<(DateOnly Start, DateOnly End)> months, Faker faker)
    {
        // A team's handle is its code alone; a PPM record's names its kind too, since a program and a team can share a name.
        var handle = subject == KpiSubject.Team ? $"{subjectHandle}:{d.Name}" : $"{subject}:{subjectHandle}:{d.Name}";
        var unit = Math.Pow(10, -d.Decimals);

        // Where this team started, and how noisy it is month to month.
        var start = Clamp(d.Baseline + faker.Random.Double(-1, 1) * d.Spread, d);
        var noise = d.Spread * 0.25;

        var baselineValues = Enumerable.Range(0, BaselineMonths)
            .Select(_ => Round(Clamp(start + faker.Random.Double(-1, 1) * noise, d), d))
            .ToList();

        // A relative target from zero resolves to zero, so the baseline is kept off it.
        var baseline = Round(Math.Max(baselineValues.Average(), d.Relative ? Math.Max(unit, d.Min + unit) : d.Min), d);

        var (target, resolved) = Target(d, baseline, unit);
        if (target is null)
            return;

        // How far toward the target this team gets by the last month: most part of the way, a few past it.
        var reach = faker.Random.Double(0.2, 1.3);

        // Only a KPI with months to spare can stop partway; a record live for just the minimum keeps every month.
        var isClosed = faker.Random.Bool((float)ClosedFraction) && months.Count - 2 > MinimumMonths;
        var measured = isClosed
            ? faker.Random.Int(MinimumMonths, Math.Max(MinimumMonths, months.Count - 2))
            : months.Count;

        _kpis.Add(new KpiModel(
            handle,
            subject,
            subjectHandle,
            d.Name,
            d.Description,
            d.Category,
            d.Prefix,
            d.Suffix,
            d.ExpressedAs,
            d.Increase ? "Increase" : "Decrease",
            baseline,
            months[0].Start,
            months[BaselineMonths - 1].End,
            d.Relative ? "RelativeChange" : "Absolute",
            target.Value,
            d.AtRiskShare is null ? null : "ProgressToTarget",
            d.AtRiskShare,
            isClosed,
            d.Window,
            d.Window == Cumulative ? months[BaselineMonths].Start : null,
            d.Window == Rolling ? d.RollingDays : null,
            d.Window == Rolling ? "Days" : null,
            d.RollingAverageOf));

        if (_planFaker.Random.Bool((float)PlannedFraction))
            Plan(handle, d, baseline, resolved, unit, months);

        var steps = Math.Max(1, measured - BaselineMonths);
        for (var i = 0; i < measured; i++)
        {
            var value = i < BaselineMonths
                ? baselineValues[i]
                : Round(Clamp(baseline + (resolved - baseline) * reach * (i - BaselineMonths + 1) / steps
                    + faker.Random.Double(-1, 1) * noise, d), d);

            int? sampleSize = d.SampleSize is { } typical
                ? Math.Max(1, (int)Math.Round(typical * faker.Random.Double(0.7, 1.3)))
                : null;

            Record(handle, d, months[i], i, value, sampleSize, noise, faker);
        }
    }

    /// <summary>Records one month's value the way the KPI's window takes it.</summary>
    private void Record(string handle, KpiDefinition d, (DateOnly Start, DateOnly End) month, int index, double value, int? sampleSize, double noise, Faker faker)
    {
        switch (d.Window)
        {
            case Cumulative:
                // Counted from the month after the baseline: an as-of day before the anchor is refused.
                if (index >= BaselineMonths)
                    _measurements.Add(new KpiMeasurementModel(handle, null, null, month.End, null, value, sampleSize));
                break;

            case Rolling:
                _measurements.Add(new KpiMeasurementModel(handle, null, null, month.End, null, value, sampleSize));
                break;

            case PointInTime:
                var mid = Round(Clamp(value + faker.Random.Double(-1, 1) * noise, d), d);
                _measurements.Add(new KpiMeasurementModel(handle, null, null, null, Reading(month.Start), value, sampleSize));
                _measurements.Add(new KpiMeasurementModel(handle, null, null, null, Reading(month.Start.AddDays(14)), mid, sampleSize));
                break;

            default:
                _measurements.Add(new KpiMeasurementModel(handle, month.Start, month.End, null, null, value, sampleSize));
                break;
        }
    }

    /// <summary>
    /// A checkpoint at every quarter's end after the baseline, through the quarter after the last month, stepping
    /// evenly from the baseline to the target; each short of plan by <see cref="AtRiskMargin"/> when the KPI has an
    /// at-risk threshold.
    /// </summary>
    private void Plan(string handle, KpiDefinition d, double baseline, double resolved, double unit, List<(DateOnly Start, DateOnly End)> months)
    {
        var after = months[BaselineMonths - 1].End;
        var until = months[^1].End.AddMonths(3);

        List<DateOnly> quarterEnds = [];
        for (var quarter = QuarterEnd(after.AddDays(1)); quarter <= until; quarter = QuarterEnd(quarter.AddDays(1)))
            quarterEnds.Add(quarter);

        var sign = d.Increase ? 1 : -1;
        var margin = Math.Max(unit, Math.Abs(resolved - baseline) * AtRiskMargin);

        for (var i = 0; i < quarterEnds.Count; i++)
        {
            var date = quarterEnds[i];
            var planned = Round(Clamp(baseline + (resolved - baseline) * (i + 1) / quarterEnds.Count, d), d);

            double? atRisk = null;
            if (d.AtRiskShare is not null)
            {
                // Held inside the KPI's range, so a plan starting at its floor gets no line below zero; rounding or
                // clamping can land it on the planned value, and the domain needs it strictly short, so then none.
                var line = Round(Clamp(planned - sign * margin, d), d);
                if (sign * (planned - line) <= 0)
                    line = Round(Clamp(planned - sign * unit, d), d);
                atRisk = sign * (planned - line) > 0 ? line : null;
            }

            _checkpoints.Add(new KpiCheckpointModel(handle, date, $"Q{(date.Month - 1) / 3 + 1} {date.Year}", planned, atRisk));
        }
    }

    /// <summary>The last day of the calendar quarter <paramref name="day"/> falls in.</summary>
    private static DateOnly QuarterEnd(DateOnly day)
    {
        var firstOfNext = new DateOnly(day.Year, (day.Month - 1) / 3 * 3 + 1, 1).AddMonths(3);
        return firstOfNext.AddDays(-1);
    }

    private static DateTimeOffset Reading(DateOnly day) =>
        new(day.ToDateTime(new TimeOnly(ReadingHourUtc, 0)), TimeSpan.Zero);

    /// <summary>
    /// The target as the import states it, and the value it resolves to; null when this team's baseline leaves
    /// no room to improve within the KPI's range.
    /// </summary>
    private static (double? Target, double Resolved) Target(KpiDefinition d, double baseline, double unit)
    {
        if (d.Relative)
        {
            var resolved = baseline + (d.Increase ? 1 : -1) * Math.Abs(baseline) * d.TargetValue / 100d;
            return (d.TargetValue, resolved);
        }

        var goal = Round(baseline * (1 + (d.Increase ? 1 : -1) * d.Improvement), d);
        goal = Math.Clamp(goal, d.Min, d.Max);

        // Rounding can land the goal on the baseline; the domain needs it strictly past.
        if (d.Increase && goal <= baseline)
            goal = baseline + unit;
        if (!d.Increase && goal >= baseline)
            goal = baseline - unit;

        return goal < d.Min || goal > d.Max ? (null, 0) : (Round(goal, d), Round(goal, d));
    }

    private static double Clamp(double value, KpiDefinition d) => Math.Clamp(value, d.Min, d.Max);

    private static double Round(double value, KpiDefinition d) => Math.Round(value, d.Decimals);
}
