namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// The KPIs a generated team or ART can carry, and the categories a scorecard groups them by.
/// </summary>
/// <remarks>
/// Each definition says where a typical team starts, how its goal is stated and how its measurements are windowed,
/// so a seeded scorecard shows every way a target can be entered — a value to reach, a percentage change, with and
/// without an at-risk line — and every kind of window: per period, per period read as a rolling average,
/// cumulative, rolling and point in time.
/// </remarks>
public static class KpiVocabulary
{
    public const string Flow = "Flow";
    public const string Quality = "Quality";
    public const string People = "People";

    /// <summary>The window kinds, spelled as the import takes them.</summary>
    public const string PerPeriod = "PerPeriod";
    public const string Cumulative = "Cumulative";
    public const string Rolling = "Rolling";
    public const string PointInTime = "PointInTime";

    /// <summary>The categories created for every seed, in scorecard order.</summary>
    public static readonly IReadOnlyList<string> Categories = [Flow, Quality, People];

    /// <summary>What each category is for, shown in settings.</summary>
    public static readonly IReadOnlyDictionary<string, string> CategoryDescriptions = new Dictionary<string, string>
    {
        [Flow] = "How work moves: throughput, cycle time and predictability.",
        [Quality] = "What escapes to production and how often a change fails.",
        [People] = "How the team itself is doing.",
    };

    /// <summary>
    /// One kind of KPI. <see cref="Baseline"/> and <see cref="Spread"/> describe where a typical team starts;
    /// the target is <see cref="TargetValue"/> as a percentage change when <see cref="Relative"/>, otherwise a
    /// share of the way past the baseline worked out per team, so every team's goal holds against its own
    /// starting point.
    /// </summary>
    /// <param name="Improvement">
    /// For an absolute target, how far past the baseline it sits as a fraction of the baseline — 0.25 aims a
    /// quarter better than where the team started.
    /// </param>
    /// <param name="AtRiskShare">How far of the way to target, as a percentage, the at-risk line sits; null for none.</param>
    /// <param name="Decimals">How many decimals a value is recorded with.</param>
    /// <param name="Min">The lowest value a measurement can take.</param>
    /// <param name="Max">The highest value a measurement can take.</param>
    /// <param name="SampleSize">How many items a month's value is typically worked out from, or null when it is a count.</param>
    /// <param name="Window">How its measurements are windowed: one of the window kinds above.</param>
    /// <param name="RollingDays">For a <see cref="Rolling"/> window, how many days each value covers.</param>
    /// <param name="RollingAverageOf">For a per-period KPI read as a rolling average, how many values it averages.</param>
    public sealed record KpiDefinition(
        string Name,
        string Description,
        string Category,
        string? Prefix,
        string? Suffix,
        bool Increase,
        double Baseline,
        double Spread,
        bool Relative,
        double TargetValue,
        double Improvement,
        double? AtRiskShare,
        string ExpressedAs,
        int Decimals,
        double Min,
        double Max,
        int? SampleSize,
        string Window = PerPeriod,
        int? RollingDays = null,
        int? RollingAverageOf = null);

    /// <summary>What a delivery team can be measured on.</summary>
    public static readonly IReadOnlyList<KpiDefinition> TeamKpis =
    [
        new("Throughput", "Work items finished per month, read over the last three months.", Flow, null, "items", true,
            18, 6, true, 25, 0, 75, "ChangeFromBaseline", 0, 0, 200, null, RollingAverageOf: 3),
        new("Cycle Time", "Median days from starting a work item to finishing it, over the last 90 days.", Flow, null, "days", false,
            9, 3, false, 0, 0.3, 70, "Value", 1, 0.5, 60, 18, Rolling, RollingDays: 90),
        new("Escaped Defects", "Bugs found in production that the team introduced, per month.", Quality, null, "bugs", false,
            5, 2, false, 0, 0.5, null, "Value", 0, 0, 50, null),
        new("Change Failure Rate", "Share of production changes that fail or are rolled back.", Quality, null, "%", false,
            18, 5, false, 0, 0.4, 60, "Value", 1, 0, 100, 30),
        new("Sprint Predictability", "Share of committed work delivered in the sprint.", Flow, null, "%", true,
            70, 8, false, 0, 0.18, 70, "ProgressToTarget", 0, 0, 100, 25),
        new("Team Engagement", "Average answer to the monthly pulse survey, out of five.", People, null, "/ 5", true,
            3.4, 0.3, false, 0, 0.15, null, "Value", 1, 1, 5, 7),
        new("Unplanned Work", "Share of the team's capacity spent on work nobody planned, since the goal was set.", Flow, null, "%", false,
            30, 8, true, 30, 0, null, "ChangeFromBaseline", 0, 0, 100, null, Cumulative),
        new("Open Bugs", "Bugs open against the team's work, counted twice a month.", Quality, null, "bugs", false,
            24, 8, false, 0, 0.4, 60, "Value", 0, 0, 300, null, PointInTime),
    ];

    /// <summary>What an ART can be measured on.</summary>
    public static readonly IReadOnlyList<KpiDefinition> ArtKpis =
    [
        new("Feature Lead Time", "Median days from a feature being accepted into the backlog to it reaching production, over the last 90 days.", Flow, null, "days", false,
            60, 15, false, 0, 0.3, 70, "Value", 0, 5, 365, 12, Rolling, RollingDays: 90),
        new("PI Predictability", "Business value delivered against business value planned, across the ART's teams.", Flow, null, "%", true,
            72, 6, false, 0, 0.15, 80, "ProgressToTarget", 0, 0, 100, null),
        new("Escaped Defects", "Bugs found in production that the ART's teams introduced, per month.", Quality, null, "bugs", false,
            22, 6, true, 40, 0, null, "Value", 0, 0, 300, null),
    ];
}
