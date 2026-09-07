using Wayd.Tools.DataGeneration.Cli.Csv;
using Wayd.Tools.DataGeneration.Cli.Generation;

namespace Wayd.Tools.DataGeneration.Cli.Seeding.Areas;

/// <summary>The stable names the PPM areas are known by, so dependencies read as references.</summary>
public static class PpmArea
{
    public const string Settings = "ppm.settings";
    public const string Themes = "ppm.strategic-themes";
    public const string Portfolios = "ppm.portfolios";
    public const string Programs = "ppm.programs";
    public const string Projects = "ppm.projects";
    public const string ProjectTasks = "ppm.project-tasks";
    public const string ProjectStages = "ppm.project-stages";
    public const string Initiatives = "ppm.strategic-initiatives";
    public const string Finalize = "ppm.finalize";
}

/// <summary>
/// A PPM area, which has nothing to do when the seed was asked to skip PPM.
/// </summary>
/// <remarks>
/// The model is read here rather than passed in because an area is asked whether it should run before it
/// is asked to run, and both questions need the same answer about whether PPM was generated at all.
/// </remarks>
public abstract class PpmSeedArea(string name, params string[] dependsOn) : SeedArea(name, dependsOn)
{
    protected GeneratedPpm Ppm(SeedContext context) =>
        context.Ppm ?? throw new SeedException($"Area '{Name}' ran without a generated PPM dataset.");

    /// <summary>Semicolon-separated, matching the CsvList helper the import endpoints parse with.</summary>
    protected static string? Join(IEnumerable<string> values)
    {
        var joined = string.Join(';', values);
        return string.IsNullOrEmpty(joined) ? null : joined;
    }

    /// <summary>Splits a generated multi-value column back into its parts.</summary>
    protected static IReadOnlyList<string> Split(string? value) =>
        string.IsNullOrWhiteSpace(value) ? [] : value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

/// <summary>
/// Bootstraps the settings a project needs: the expenditure categories and the lifecycle.
/// </summary>
/// <remarks>
/// These are not imports — they are created through the settings API, and a project references them by id,
/// so their ids have to be in hand before the project file can be written.
/// </remarks>
public sealed class PpmSettingsArea() : PpmSeedArea(PpmArea.Settings)
{
    public override bool ShouldRun(SeedContext context) => context.Ppm is not null;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        var ppm = Ppm(context);

        context.Log($"Ensuring {ppm.ExpenditureCategories.Count} expenditure categories exist...");
        context.ExpenditureCategoryIds = await context.Client.EnsureExpenditureCategories(ppm.ExpenditureCategories, cancellationToken);

        context.Log($"Ensuring the '{ppm.Lifecycle.Name}' project lifecycle exists and is active...");
        context.ProjectLifecycleId = await context.Client.EnsureProjectLifecycle(ppm.Lifecycle, cancellationToken);
    }
}

/// <summary>Loads the strategic themes programs and projects are tagged with.</summary>
public sealed class StrategicThemesArea() : PpmSeedArea(PpmArea.Themes)
{
    public override bool ShouldRun(SeedContext context) => context.Ppm?.StrategicThemes.Count > 0;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        var themes = Ppm(context).StrategicThemes;
        context.Log($"Importing {themes.Count} strategic themes...");

        var rows = themes.Select(t => new StrategicThemeCsvRow
        {
            ImportId = t.Name,
            Name = t.Name,
            Description = t.Description,
            State = t.State,
        });

        var run = await context.Client.ImportStrategicThemes(CsvFile.ToBytes(rows), cancellationToken);
        context.Publish(Name, run.CreatedIdsByImportId);
    }
}

/// <summary>Loads the portfolios everything else in PPM hangs off. People are named by employee number.</summary>
public sealed class PortfoliosArea() : PpmSeedArea(PpmArea.Portfolios, OrganizationArea.Employees)
{
    public override bool ShouldRun(SeedContext context) => context.Ppm?.Portfolios.Count > 0;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        var portfolios = Ppm(context).Portfolios;
        context.Log($"Importing {portfolios.Count} portfolios...");

        var rows = portfolios.Select(p => new PortfolioCsvRow
        {
            ImportId = p.Name,
            Name = p.Name,
            Description = p.Description,
            Status = p.Status,
            CreatedOn = p.CreatedOn,
            ActivatedOn = p.ActivatedOn,
            Sponsors = p.Sponsors,
            Owners = p.Owners,
            Managers = p.Managers,
        });

        var run = await context.Client.ImportPortfolios(CsvFile.ToBytes(rows), cancellationToken);
        context.Publish(Name, run.CreatedIdsByImportId);
    }
}

/// <summary>Loads the programs, resolving each one's portfolio and themes to the ids those runs created.</summary>
public sealed class ProgramsArea() : PpmSeedArea(
    PpmArea.Programs, PpmArea.Portfolios, PpmArea.Themes, OrganizationArea.Employees)
{
    public override bool ShouldRun(SeedContext context) => context.Ppm?.Programs.Count > 0;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        var programs = Ppm(context).Programs;
        context.Log($"Importing {programs.Count} programs...");

        var rows = programs.Select(p => new ProgramCsvRow
        {
            ImportId = p.Name,
            Name = p.Name,
            Description = p.Description,
            PortfolioId = context.Id(PpmArea.Portfolios, p.PortfolioName),
            Status = p.Status,
            Start = p.Start,
            End = p.End,
            CreatedOn = p.CreatedOn,
            ActivatedOn = p.ActivatedOn,
            StrategicThemes = Join(context.Ids(PpmArea.Themes, Split(p.StrategicThemes)).Select(id => id.ToString())),
            Sponsors = p.Sponsors,
            Owners = p.Owners,
            Managers = p.Managers,
        });

        var run = await context.Client.ImportPrograms(CsvFile.ToBytes(rows), cancellationToken);
        context.Publish(Name, run.CreatedIdsByImportId);
    }
}

/// <summary>
/// Loads the projects. This is where the most references converge: portfolio, program, expenditure
/// category, lifecycle and themes are all ids by the time the file is written.
/// </summary>
public sealed class ProjectsArea() : PpmSeedArea(
    PpmArea.Projects, PpmArea.Portfolios, PpmArea.Programs, PpmArea.Themes, PpmArea.Settings, OrganizationArea.Employees)
{
    public override bool ShouldRun(SeedContext context) => context.Ppm?.Projects.Count > 0;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        var projects = Ppm(context).Projects;
        context.Log($"Importing {projects.Count} projects...");

        var rows = projects.Select(p => new ProjectCsvRow
        {
            ImportId = p.Key,
            Name = p.Name,
            Description = p.Description,
            Key = p.Key,
            PortfolioId = context.Id(PpmArea.Portfolios, p.PortfolioName),
            ExpenditureCategoryId = CategoryId(context, p.ExpenditureCategoryName),
            Status = p.Status,
            ProgramId = p.ProgramName is null ? null : context.Id(PpmArea.Programs, p.ProgramName),

            // Every generated project is assigned the one lifecycle the settings area activated; a project
            // needs it before it can be approved, and its stages are copied from it.
            ProjectLifecycleId = p.ProjectLifecycleName is null ? null : context.ProjectLifecycleId,

            BusinessCase = p.BusinessCase,
            ExpectedBenefits = p.ExpectedBenefits,
            Start = p.Start,
            End = p.End,
            CreatedOn = p.CreatedOn,
            ActivatedOn = p.ActivatedOn,
            ClosedOn = p.ClosedOn,
            StrategicThemes = Join(context.Ids(PpmArea.Themes, Split(p.StrategicThemes)).Select(id => id.ToString())),
            Sponsors = p.Sponsors,
            Owners = p.Owners,
            Managers = p.Managers,
            Members = p.Members,
        });

        var run = await context.Client.ImportProjects(CsvFile.ToBytes(rows), cancellationToken);
        context.Publish(Name, run.CreatedIdsByImportId);
    }

    private static int CategoryId(SeedContext context, string name) =>
        context.ExpenditureCategoryIds.TryGetValue(name, out var id)
            ? id
            : throw new SeedException($"No expenditure category named '{name}' was created by the settings bootstrap.");
}

/// <summary>
/// Loads the work breakdown. Projects are still named by key here — that is a natural key the import
/// keeps — so the only reference that had to change is a task's parent, which now names the parent row.
/// </summary>
public sealed class ProjectTasksArea() : PpmSeedArea(
    PpmArea.ProjectTasks, PpmArea.Projects, OrganizationArea.Employees)
{
    public override bool ShouldRun(SeedContext context) => context.Ppm?.ProjectTasks.Count > 0;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        var tasks = Ppm(context).ProjectTasks;
        context.Log($"Importing {tasks.Count} project tasks...");

        var rows = tasks.Select(t => new ProjectTaskCsvRow
        {
            ImportId = TaskImportId(t.ProjectKey, t.Name),
            ProjectKey = t.ProjectKey,
            Name = t.Name,
            Description = t.Description,
            StageName = t.StageName,
            ParentImportId = t.ParentTaskName is null ? null : TaskImportId(t.ProjectKey, t.ParentTaskName),
            Type = t.Type,
            Status = t.Status,
            Priority = t.Priority,
            Progress = t.Progress,
            PlannedStart = t.PlannedStart,
            PlannedEnd = t.PlannedEnd,
            PlannedDate = t.PlannedDate,
            EstimatedEffortHours = t.EstimatedEffortHours,
            Assignees = t.Assignees,
        });

        var run = await context.Client.ImportProjectTasks(CsvFile.ToBytes(rows), cancellationToken);
        context.Publish(Name, run.CreatedIdsByImportId);
    }

    /// <summary>
    /// A task name is unique only within its project, so the key carries both — one file covers many
    /// projects, and two of them may each hold a "Discovery".
    /// </summary>
    private static string TaskImportId(string projectKey, string taskName) => $"{projectKey}|{taskName}";
}

/// <summary>
/// Sets each stage's status. Runs after the tasks because the generator computed the status from them, and
/// a stage read as complete while its tasks are still open would contradict what was just loaded.
/// </summary>
public sealed class ProjectStagesArea() : PpmSeedArea(
    PpmArea.ProjectStages, PpmArea.Projects, PpmArea.ProjectTasks)
{
    public override bool ShouldRun(SeedContext context) => context.Ppm?.ProjectStages.Count > 0;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        var stages = Ppm(context).ProjectStages;
        context.Log($"Setting {stages.Count} project stage statuses...");

        var rows = stages.Select(s => new ProjectStageCsvRow
        {
            ImportId = $"{s.ProjectKey}|{s.StageName}",
            ProjectKey = s.ProjectKey,
            StageName = s.StageName,
            Status = s.Status,
        });

        var run = await context.Client.ImportProjectStages(CsvFile.ToBytes(rows), cancellationToken);
        context.Publish(Name, run.CreatedIdsByImportId);
    }
}

/// <summary>
/// Loads the initiatives and their KPIs in one call, since a closed initiative accepts neither new
/// projects nor new KPIs — so both have to land before it is driven to its final status.
/// </summary>
public sealed class StrategicInitiativesArea() : PpmSeedArea(
    PpmArea.Initiatives, PpmArea.Portfolios, PpmArea.Projects, OrganizationArea.Employees)
{
    public override bool ShouldRun(SeedContext context) => context.Ppm?.StrategicInitiatives.Count > 0;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        var ppm = Ppm(context);
        context.Log($"Importing {ppm.StrategicInitiatives.Count} strategic initiatives and {ppm.StrategicInitiativeKpis.Count} KPIs...");

        var rows = ppm.StrategicInitiatives.Select(i => new StrategicInitiativeCsvRow
        {
            ImportId = i.Name,
            Name = i.Name,
            Description = i.Description,
            PortfolioId = context.Id(PpmArea.Portfolios, i.PortfolioName),
            Status = i.Status,
            Start = i.Start,
            End = i.End,
            ProjectKeys = i.ProjectKeys,
            Sponsors = i.Sponsors,
            Owners = i.Owners,
        });

        // The KPI file names its initiative by that row's import id, which the seed sets to the initiative
        // name — so this is the same value the generator already carried, under a column that now says
        // which file it points into.
        var kpiRows = ppm.StrategicInitiativeKpis.Select(k => new StrategicInitiativeKpiCsvRow
        {
            StrategicInitiativeImportId = k.StrategicInitiativeName,
            Name = k.Name,
            Description = k.Description,
            TargetValue = k.TargetValue,
            StartingValue = k.StartingValue,
            Prefix = k.Prefix,
            Suffix = k.Suffix,
            TargetDirection = k.TargetDirection,
        }).ToList();

        var run = await context.Client.ImportStrategicInitiatives(
            CsvFile.ToBytes(rows),
            kpiRows.Count > 0 ? CsvFile.ToBytes(kpiRows) : null,
            cancellationToken);

        context.Publish(Name, run.CreatedIdsByImportId);
    }
}

/// <summary>
/// Closes the historical programs and portfolios, last.
/// </summary>
/// <remarks>
/// The domain only lets work be added to an active program or portfolio, but only lets one close once
/// everything inside it is closed — so historical items are imported active and finished here. The row
/// names its target by id, read per Type.
/// </remarks>
public sealed class PpmFinalizeArea() : PpmSeedArea(
    PpmArea.Finalize, PpmArea.Portfolios, PpmArea.Programs, PpmArea.Projects, PpmArea.Initiatives)
{
    public override bool ShouldRun(SeedContext context) => context.Ppm?.Finalizations.Count > 0;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        var finalizations = Ppm(context).Finalizations;
        context.Log($"Finalizing {finalizations.Count} programs/portfolios...");

        var rows = finalizations.Select(f => new PpmFinalizationCsvRow
        {
            ImportId = $"{f.Type}|{f.Name}",
            Type = f.Type,
            Id = TargetId(context, f),
            Status = f.Status,
            EndDate = f.EndDate,
        });

        var run = await context.Client.ImportPpmFinalizations(CsvFile.ToBytes(rows), cancellationToken);
        context.Publish(Name, run.CreatedIdsByImportId);
    }

    /// <summary>
    /// The program or portfolio the row closes. One Id column read per Type replaced a name plus the
    /// portfolio name that used to be needed to disambiguate a program.
    /// </summary>
    private static Guid TargetId(SeedContext context, PpmFinalizationModel row) =>
        string.Equals(row.Type, "Program", StringComparison.OrdinalIgnoreCase)
            ? context.Id(PpmArea.Programs, row.Name)
            : context.Id(PpmArea.Portfolios, row.Name);
}
