using Wayd.Tools.DataGeneration.Cli.Csv;
using Wayd.Tools.DataGeneration.Cli.Generation;
using static Wayd.Tools.DataGeneration.Cli.Generation.PlanningVocabulary;

namespace Wayd.Tools.DataGeneration.Cli.Seeding.Areas;

/// <summary>The stable names the Planning areas are known by, so dependencies read as references.</summary>
public static class PlanningArea
{
    public const string PlanningIntervals = "planning.planning-intervals";
    public const string Objectives = "planning.planning-interval-objectives";
    public const string Risks = "planning.risks";
}

/// <summary>
/// A Planning area, which has nothing to do when the seed was asked to skip Planning.
/// </summary>
/// <remarks>
/// Every Planning import resolves its teams against Planning's own copy of them, which Organization fills
/// asynchronously after the teams import — so these areas also wait on staffing, which lands several runs
/// later and leaves that copy time to catch up.
/// </remarks>
public abstract class PlanningSeedArea(string name, params string[] dependsOn) : SeedArea(name, dependsOn)
{
    protected GeneratedPlanning Data(SeedContext context) =>
        context.Planning ?? throw new SeedException($"Area '{Name}' ran without a generated Planning dataset.");
}

/// <summary>Loads the planning intervals, each with its roster: the ART and its teams, as ids.</summary>
public sealed class PlanningIntervalsArea() : PlanningSeedArea(
    PlanningArea.PlanningIntervals, OrganizationArea.Teams, OrganizationArea.Staffing)
{
    public override string BatchedImport => "planning.planning-intervals";

    public override bool ShouldRun(SeedContext context) => context.Planning?.PlanningIntervals.Count > 0;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        var intervals = Data(context).PlanningIntervals;

        var rows = intervals.Select(p => new PlanningIntervalCsvRow
        {
            ImportId = p.Name,
            Name = p.Name,
            Description = p.Description,
            Start = p.Start,
            End = p.End,
            IterationWeeks = p.IterationWeeks,
            IterationPrefix = p.IterationPrefix,
            TeamIds = string.Join(';', context.Ids(OrganizationArea.Teams, p.TeamCodes.Split(';'))),
        }).ToList();

        var batches = Batch(context, rows, r => r.ImportId);
        context.Log($"Importing {rows.Count} planning intervals in {batches.Count} batch(es)...");

        var created = await ImportBatches(context, "planning intervals", batches,
            batch => context.Client.ImportPlanningIntervals(CsvFile.ToBytes(batch), cancellationToken));

        context.Publish(Name, created);
    }
}

/// <summary>Loads every team's objectives, resolving the interval and team each row names.</summary>
public sealed class PlanningIntervalObjectivesArea() : PlanningSeedArea(
    PlanningArea.Objectives, PlanningArea.PlanningIntervals, OrganizationArea.Teams)
{
    public override string BatchedImport => "planning.planning-interval-objectives";

    public override bool ShouldRun(SeedContext context) => context.Planning?.Objectives.Count > 0;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        var objectives = Data(context).Objectives;

        var rows = objectives.Select(o => new PlanningIntervalObjectiveCsvRow
        {
            ImportId = o.ImportId,
            PlanningIntervalId = context.Id(PlanningArea.PlanningIntervals, o.PlanningIntervalName),
            TeamId = context.Id(OrganizationArea.Teams, o.TeamCode),
            Name = o.Name,
            Description = o.Description,
            StatusId = ObjectiveStatusIds[o.Status],
            Progress = o.Progress,
            StartDate = o.StartDate,
            TargetDate = o.TargetDate,
            IsStretch = o.IsStretch,
            ClosedDateUtc = o.ClosedAt is { } closed ? PlanningCsv.Timestamp(closed) : null,
            Order = o.Order,
        }).ToList();

        var batches = Batch(context, rows, r => r.PlanningIntervalId);
        context.Log($"Importing {rows.Count} planning interval objectives in {batches.Count} batch(es)...");

        var created = await ImportBatches(context, "objectives", batches,
            batch => context.Client.ImportPlanningIntervalObjectives(CsvFile.ToBytes(batch), cancellationToken));

        context.Publish(Name, created);
    }
}

/// <summary>Loads the risks, resolving each one's team and the people who reported and own it.</summary>
public sealed class RisksArea() : PlanningSeedArea(
    PlanningArea.Risks, OrganizationArea.Teams, OrganizationArea.Employees, OrganizationArea.Staffing)
{
    public override string BatchedImport => "planning.risks";

    public override bool ShouldRun(SeedContext context) => context.Planning?.Risks.Count > 0;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        var risks = Data(context).Risks;

        var rows = risks.Select(r => new RiskCsvRow
        {
            ImportId = r.Handle,
            TeamId = context.Id(OrganizationArea.Teams, r.TeamCode),
            Summary = r.Summary,
            Description = r.Description,
            ReportedOnUtc = PlanningCsv.Timestamp(r.ReportedAt),
            ReportedById = context.Id(OrganizationArea.Employees, r.ReportedByEmployeeNumber),
            StatusId = RiskStatusIds[r.Status],
            CategoryId = RiskCategoryIds[r.Category],
            ImpactId = RiskGradeIds[r.Impact],
            LikelihoodId = RiskGradeIds[r.Likelihood],
            AssigneeId = r.AssigneeEmployeeNumber is null ? null : context.Id(OrganizationArea.Employees, r.AssigneeEmployeeNumber),
            FollowUpDate = r.FollowUpDate,
            Response = r.Response,
            ClosedDateUtc = r.ClosedAt is { } closed ? PlanningCsv.Timestamp(closed) : null,
        }).ToList();

        var batches = Batch(context, rows, r => r.TeamId);
        context.Log($"Importing {rows.Count} risks in {batches.Count} batch(es)...");

        var created = await ImportBatches(context, "risks", batches,
            batch => context.Client.ImportRisks(CsvFile.ToBytes(batch), cancellationToken));

        context.Publish(Name, created);
    }
}
