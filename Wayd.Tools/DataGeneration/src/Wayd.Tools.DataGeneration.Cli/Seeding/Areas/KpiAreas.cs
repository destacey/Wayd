using System.Globalization;
using Wayd.Tools.DataGeneration.Cli.Csv;
using Wayd.Tools.DataGeneration.Cli.Generation;

namespace Wayd.Tools.DataGeneration.Cli.Seeding.Areas;

/// <summary>The stable names the KPI areas are known by, so dependencies read as references.</summary>
public static class KpiArea
{
    public const string FeatureFlag = "kpis.feature-flag";
    public const string Categories = "kpis.categories";
    public const string Kpis = "kpis.kpis";

    /// <summary>The feature flag every KPI endpoint is gated on.</summary>
    public const string FeatureFlagName = "kpis";

    /// <summary>The subject type a team's or a team of teams' KPIs are stored under.</summary>
    public const string TeamSubjectType = "organization.team";
}

/// <summary>A KPI area, which has nothing to do when the seed was asked to skip KPIs.</summary>
public abstract class KpiSeedArea(string name, params string[] dependsOn) : SeedArea(name, dependsOn)
{
    protected GeneratedKpis Data(SeedContext context) =>
        context.Kpis ?? throw new SeedException($"Area '{Name}' ran without a generated KPI dataset.");
}

/// <summary>
/// Switches KPIs on. Every KPI endpoint answers 404 while the flag is off, and the flag is seeded off.
/// </summary>
public sealed class KpiFeatureFlagArea() : KpiSeedArea(KpiArea.FeatureFlag)
{
    public override bool ShouldRun(SeedContext context) => context.Kpis is not null;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        var enabled = await context.Client.EnsureFeatureFlagEnabled(KpiArea.FeatureFlagName, cancellationToken);

        context.Log(enabled
            ? $"Enabled the '{KpiArea.FeatureFlagName}' feature flag."
            : $"The '{KpiArea.FeatureFlagName}' feature flag is already enabled.");
    }
}

/// <summary>
/// Creates the KPI categories through the ordinary endpoint — create-or-get by name, like the expenditure
/// categories — since they are the organization's settings rather than records anyone imports.
/// </summary>
public sealed class KpiCategoriesArea() : KpiSeedArea(KpiArea.Categories, KpiArea.FeatureFlag)
{
    public override bool ShouldRun(SeedContext context) => context.Kpis?.Kpis.Count > 0;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        var categories = Data(context).Categories;

        context.Log($"Ensuring {categories.Count} KPI categories exist...");
        var ids = await context.Client.EnsureKpiCategories(
            categories.Select(c => (c, KpiVocabulary.CategoryDescriptions[c])), cancellationToken);

        context.Publish(Name, ids);
    }
}

/// <summary>
/// Loads every team's and ART's KPIs with their monthly history and any checkpoint plan, resolving each one's record
/// and category. A KPI travels with its measurements and plan, so a batch never splits one from them. A point-in-time moment is
/// written with its offset, which the import requires.
/// </summary>
public sealed class KpisArea() : KpiSeedArea(KpiArea.Kpis, KpiArea.Categories, OrganizationArea.Teams)
{
    public override string BatchedImport => "kpis.kpis";

    public override bool ShouldRun(SeedContext context) => context.Kpis?.Kpis.Count > 0;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        var data = Data(context);

        var history = data.Measurements
            .GroupBy(m => m.KpiHandle, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var plans = data.Checkpoints
            .GroupBy(c => c.KpiHandle, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var rows = data.Kpis.Select(k => new KpiCsvRow
        {
            ImportId = k.Handle,
            SubjectType = KpiArea.TeamSubjectType,
            SubjectId = context.Id(OrganizationArea.Teams, k.TeamCode),
            Name = k.Name,
            Description = k.Description,
            CategoryId = context.Id(KpiArea.Categories, k.Category),
            Prefix = k.Prefix,
            Suffix = k.Suffix,
            ExpressedAs = k.ExpressedAs,
            Direction = k.Direction,
            BaselineValue = k.BaselineValue,
            BaselineWindowStart = k.BaselineWindowStart,
            BaselineWindowEnd = k.BaselineWindowEnd,
            TargetKind = k.TargetKind,
            TargetValue = k.TargetValue,
            AtRiskKind = k.AtRiskKind,
            AtRiskValue = k.AtRiskValue,
            IsClosed = k.IsClosed,
            WindowKind = k.WindowKind,
            WindowAnchorDate = k.WindowAnchorDate,
            WindowRollingCount = k.WindowRollingCount,
            WindowRollingUnit = k.WindowRollingUnit,
            RollingAverageOf = k.RollingAverageOf,
        }).ToList();

        var batches = Batch(context, rows, r => r.ImportId);
        context.Log($"Importing {rows.Count} KPIs, {data.Measurements.Count} measurements and {data.Checkpoints.Count} checkpoints in {batches.Count} batch(es)...");

        var created = await ImportBatches(context, "KPIs", batches, batch =>
        {
            var measurements = batch
                .SelectMany(k => history.TryGetValue(k.ImportId, out var values) ? values : [])
                .Select(m => new KpiMeasurementCsvRow
                {
                    KpiImportId = m.KpiHandle,
                    PeriodStart = m.PeriodStart,
                    PeriodEnd = m.PeriodEnd,
                    AsOf = m.AsOf,
                    MeasuredAt = m.MeasuredAt?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                    Value = m.Value,
                    SampleSize = m.SampleSize,
                });

            var checkpoints = batch
                .SelectMany(k => plans.TryGetValue(k.ImportId, out var plan) ? plan : [])
                .Select(c => new KpiCheckpointCsvRow
                {
                    KpiImportId = c.KpiHandle,
                    Date = c.Date,
                    Label = c.Label,
                    PlannedValue = c.PlannedValue,
                    AtRiskValue = c.AtRiskValue,
                });

            return context.Client.ImportKpis(
                CsvFile.ToBytes(batch), CsvFile.ToBytes(measurements), CsvFile.ToBytes(checkpoints), cancellationToken);
        });

        context.Publish(Name, created);
    }
}
