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

    private readonly OrgStructure _org;
    private readonly KpiOptions _options;
    private readonly GenerationContext _context;
    private readonly Faker _faker;

    private readonly List<KpiModel> _kpis = [];
    private readonly List<KpiMeasurementModel> _measurements = [];

    public KpiGenerator(OrgStructure org, KpiOptions options, GenerationContext context)
    {
        _org = org;
        _options = options;
        _context = context;

        // Derived from the area name, so adding another generator does not shift this one's data.
        _faker = new Faker { Random = new Randomizer(context.SeedFor(AreaName)) };
    }

    public GeneratedKpis Generate()
    {
        var months = Months();
        if (months.Count < MinimumMonths || _options.KpisPerTeam <= 0)
            return new GeneratedKpis(Categories, [], []);

        foreach (var valueStream in _org.ValueStreams)
        {
            foreach (var art in valueStream.Arts)
            {
                if (art.TeamCode is { } artCode)
                    Measure(artCode, ArtKpis, Math.Max(1, _options.KpisPerTeam / 2), months);

                foreach (var team in art.Teams)
                    Measure(team.TeamCode, TeamKpis, _options.KpisPerTeam, months);
            }
        }

        return new GeneratedKpis(Categories, _kpis, _measurements);
    }

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

    private void Measure(string teamCode, IReadOnlyList<KpiDefinition> catalogue, int count, List<(DateOnly Start, DateOnly End)> months)
    {
        foreach (var definition in _faker.Random.Shuffle(catalogue).Take(Math.Min(count, catalogue.Count)))
            Add(teamCode, definition, months);
    }

    private void Add(string teamCode, KpiDefinition d, List<(DateOnly Start, DateOnly End)> months)
    {
        var handle = $"{teamCode}:{d.Name}";
        var unit = Math.Pow(10, -d.Decimals);

        // Where this team started, and how noisy it is month to month.
        var start = Clamp(d.Baseline + _faker.Random.Double(-1, 1) * d.Spread, d);
        var noise = d.Spread * 0.25;

        var baselineValues = Enumerable.Range(0, BaselineMonths)
            .Select(_ => Round(Clamp(start + _faker.Random.Double(-1, 1) * noise, d), d))
            .ToList();

        // A relative target from zero resolves to zero, so the baseline is kept off it.
        var baseline = Round(Math.Max(baselineValues.Average(), d.Relative ? Math.Max(unit, d.Min + unit) : d.Min), d);

        var (target, resolved) = Target(d, baseline, unit);
        if (target is null)
            return;

        // How far toward the target this team gets by the last month: most part of the way, a few past it.
        var reach = _faker.Random.Double(0.2, 1.3);

        var isClosed = _faker.Random.Bool((float)ClosedFraction);
        var measured = isClosed
            ? _faker.Random.Int(MinimumMonths, Math.Max(MinimumMonths, months.Count - 2))
            : months.Count;

        _kpis.Add(new KpiModel(
            handle,
            teamCode,
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
            isClosed));

        var steps = Math.Max(1, measured - BaselineMonths);
        for (var i = 0; i < measured; i++)
        {
            var value = i < BaselineMonths
                ? baselineValues[i]
                : Round(Clamp(baseline + (resolved - baseline) * reach * (i - BaselineMonths + 1) / steps
                    + _faker.Random.Double(-1, 1) * noise, d), d);

            int? sampleSize = d.SampleSize is { } typical
                ? Math.Max(1, (int)Math.Round(typical * _faker.Random.Double(0.7, 1.3)))
                : null;

            _measurements.Add(new KpiMeasurementModel(handle, months[i].Start, months[i].End, value, sampleSize));
        }
    }

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
