using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Infrastructure.Auth;
using Wayd.ProjectPortfolioManagement.Application;
using Wayd.ProjectPortfolioManagement.Application.ExpenditureCategories.Commands;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Command;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Dtos;
using Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Commands;
using Wayd.ProjectPortfolioManagement.Application.Projects.Commands;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Commands;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.Web.Api.IntegrationTests.Infrastructure;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Proves the task import keeps a project whole on a real provider: a project with a rejected row saves
/// none of its tasks, while another project in the same file saves all of its own.
/// </summary>
/// <remarks>
/// The unit fakes track nothing, so they cannot show what matters here. The rejected project's aggregate is
/// loaded and has tasks added to it before the rejection, and only a real change tracker shows that
/// discarding it leaves neither those tasks nor the per-project numbers they consumed behind.
/// </remarks>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class ProjectTaskImportTests(WaydSqlServerApiFactory factory)
{
    private const string UserId = "project-task-import-test";
    private const string FirstStage = "Delivery";

    private static readonly LocalDate Created = new(2026, 1, 5);
    private static readonly LocalDate Start = new(2026, 1, 12);
    private static readonly LocalDate End = new(2026, 6, 30);

    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public async Task Import_KeepsOutEveryTaskOfAProjectWithARejectedRow_AndKeepsTheOtherProjects()
    {
        // Arrange — two live projects sharing one lifecycle, so both have the stage the rows name
        var ct = TestContext.Current.CancellationToken;
        var (apollo, gemini) = await TwoProjects(ct);

        // Act — Apollo's second row names a stage the lifecycle does not have, and it is rejected only
        // after its first row has already added a task to the aggregate.
        var runId = await SubmitTasks(ct,
            ("a1", Row(apollo, "Design")),
            ("a2", Row(apollo, "Build") with { StageName = "Nonexistent" }),
            ("a3", Row(apollo, "Test")),
            ("g1", Row(gemini, "Discovery")),
            ("g2", Row(gemini, "Discovery detail") with { ParentImportId = "g1" }));

        var run = await WaitForRun(runId, ct);

        // Assert
        Assert.Equal(ImportProcessStatus.PartiallySucceeded, run.Status);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IProjectPortfolioManagementDbContext>();

        var tasksByProject = await dbContext.Projects
            .AsNoTracking()
            .Where(p => p.Key == apollo || p.Key == gemini)
            .Select(p => new { Key = p.Key.Value, Names = p.Tasks.Select(t => t.Name).ToList() })
            .ToDictionaryAsync(p => p.Key, p => p.Names, ct);

        Assert.Empty(tasksByProject[apollo.Value]);
        Assert.Equal(["Discovery", "Discovery detail"], tasksByProject[gemini.Value].Order());

        // The kept project's tree is intact — the child hangs off the row that made its parent, not off a
        // stage — and its numbering starts at 1, untouched by the rows Apollo discarded.
        var gemeniTasks = await dbContext.ProjectTasks
            .AsNoTracking()
            .Where(t => t.Project!.Key == gemini)
            .Select(t => new { t.Name, t.Number, t.ParentId })
            .OrderBy(t => t.Number)
            .ToListAsync(ct);

        Assert.Equal([1, 2], gemeniTasks.Select(t => t.Number));
        Assert.NotNull(gemeniTasks[1].ParentId);

        var rows = run.Rows.ToDictionary(r => r.ImportId);
        Assert.Contains("Nonexistent", rows["a2"].Error);
        Assert.Equal(
            "Not applied: another row for the same project was rejected (import id 'a2').", rows["a1"].Error);
        Assert.Equal(
            "Not applied: another row for the same project was rejected (import id 'a2').", rows["a3"].Error);
        Assert.Equal(ImportRowStatus.Succeeded, rows["g1"].Status);
        Assert.Equal(ImportRowStatus.Succeeded, rows["g2"].Status);
    }

    [Fact]
    public async Task Import_LetsARejectedProjectBeReimportedFromScratch()
    {
        // Arrange — the rejected project consumed no task numbers, which only a real database can show:
        // the fakes seed the sequence from a list they never wrote to
        var ct = TestContext.Current.CancellationToken;
        var (apollo, _) = await TwoProjects(ct);

        var failed = await WaitForRun(
            await SubmitTasks(ct,
                ("a1", Row(apollo, "Design")),
                ("a2", Row(apollo, "Build") with { StageName = "Nonexistent" })),
            ct);
        Assert.Equal(ImportProcessStatus.Failed, failed.Status);

        // Act — the same file with the stage corrected
        var run = await WaitForRun(
            await SubmitTasks(ct, ("a1", Row(apollo, "Design")), ("a2", Row(apollo, "Build"))),
            ct);

        // Assert
        Assert.Equal(ImportProcessStatus.Succeeded, run.Status);

        using var scope = _factory.Services.CreateScope();
        var numbers = await scope.ServiceProvider.GetRequiredService<IProjectPortfolioManagementDbContext>()
            .ProjectTasks
            .AsNoTracking()
            .Where(t => t.Project!.Key == apollo)
            .OrderBy(t => t.Number)
            .Select(t => t.Number)
            .ToListAsync(ct);

        Assert.Equal([1, 2], numbers);
    }

    private static ImportProjectTaskDto Row(ProjectKey projectKey, string name) =>
        new(
            projectKey,
            name,
            $"{name}, submitted by the project task import test.",
            ProjectTaskType.Task,
            TaskStatus.NotStarted,
            TaskPriority.Medium,
            FirstStage,
            ParentImportId: null,
            ParentTaskId: null,
            Progress: 0m,
            Start,
            End,
            PlannedDate: null,
            EstimatedEffortHours: null,
            AssigneeEmployeeNumbers: []);

    /// <summary>
    /// Two active projects in one portfolio, sharing an active lifecycle so both carry the stage the task
    /// rows name. Built through the portfolio and project imports, since those are the paths that produce
    /// the state a real task file lands against.
    /// </summary>
    private async Task<(ProjectKey Apollo, ProjectKey Gemini)> TwoProjects(CancellationToken ct)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId(UserId);
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var suffix = $"{Guid.NewGuid():N}"[..8];

        var category = await dispatcher.Send(
            new CreateExpenditureCategoryCommand($"Capex {suffix}", "Task import test category", true, true, null), ct);
        Assert.True(category.IsSuccess, category.IsFailure ? category.Error : null);

        var lifecycle = await dispatcher.Send(
            new CreateProjectLifecycleCommand(
                $"Lifecycle {suffix}",
                "Task import test lifecycle",
                [new(FirstStage, "Delivery stage"), new("Closure", "Closure stage")]),
            ct);
        Assert.True(lifecycle.IsSuccess, lifecycle.IsFailure ? lifecycle.Error : null);

        var activated = await dispatcher.Send(new ActivateProjectLifecycleCommand(lifecycle.Value), ct);
        Assert.True(activated.IsSuccess, activated.IsFailure ? activated.Error : null);

        // Imported active rather than created and then activated: activation is delivery-leadership gated,
        // and the import runs as the system actor.
        var portfolioRun = await WaitForRun(
            await Submit(
                scope,
                new ImportProjectPortfoliosCommand([
                    new SubmittedImportRow<ImportProjectPortfolioDto>(
                        "p1",
                        new(
                            $"Portfolio {suffix}",
                            "Task import test portfolio",
                            ProjectPortfolioStatus.Active,
                            Created,
                            Created,
                            [], [], []))
                ])),
            ct);
        Assert.Equal(ImportProcessStatus.Succeeded, portfolioRun.Status);
        var portfolioId = portfolioRun.Rows.Single().CreatedEntityId!.Value;

        var apollo = new ProjectKey($"AP{suffix}"[..10].ToUpperInvariant());
        var gemini = new ProjectKey($"GE{suffix}"[..10].ToUpperInvariant());

        var projectRun = await WaitForRun(
            await Submit(
                scope,
                new ImportProjectsCommand([
                    new SubmittedImportRow<ImportProjectDto>("j1", Project(apollo, portfolioId, category.Value, lifecycle.Value)),
                    new SubmittedImportRow<ImportProjectDto>("j2", Project(gemini, portfolioId, category.Value, lifecycle.Value)),
                ])),
            ct);
        Assert.Equal(ImportProcessStatus.Succeeded, projectRun.Status);

        return (apollo, gemini);
    }

    private static ImportProjectDto Project(ProjectKey key, Guid portfolioId, int categoryId, Guid lifecycleId) =>
        new(
            $"Project {key.Value}",
            "Task import test project",
            key,
            ProjectStatus.Active,
            portfolioId,
            ProgramId: null,
            categoryId,
            lifecycleId,
            BusinessCase: null,
            ExpectedBenefits: null,
            Start,
            End,
            Created,
            ActivatedOn: Start,
            ClosedOn: null,
            StrategicThemeIds: [],
            SponsorEmployeeNumbers: [],
            OwnerEmployeeNumbers: [],
            ManagerEmployeeNumbers: [],
            MemberEmployeeNumbers: []);

    private async Task<Guid> SubmitTasks(CancellationToken ct, params (string ImportId, ImportProjectTaskDto Row)[] rows)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId(UserId);

        return await Submit(
            scope,
            new ImportProjectTasksCommand(
                [.. rows.Select(r => new SubmittedImportRow<ImportProjectTaskDto>(r.ImportId, r.Row))]));
    }

    private static async Task<Guid> Submit(IServiceScope scope, ICommand<Guid> command)
    {
        var submitted = await scope.ServiceProvider.GetRequiredService<IDispatcher>()
            .Send(command, TestContext.Current.CancellationToken);

        Assert.True(submitted.IsSuccess, submitted.IsFailure ? submitted.Error : null);
        return submitted.Value;
    }

    private async Task<ImportProcess> WaitForRun(Guid runId, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);

        while (DateTime.UtcNow < deadline)
        {
            using var scope = _factory.Services.CreateScope();

            var run = await scope.ServiceProvider.GetRequiredService<IImportDbContext>().ImportProcesses
                .AsNoTracking()
                .Include(p => p.Rows)
                .SingleAsync(p => p.Id == runId, ct);

            if (run.IsTerminal)
                return run;

            await Task.Delay(200, ct);
        }

        throw new TimeoutException($"Import run {runId} did not reach a terminal status.");
    }
}
