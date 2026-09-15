using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Activities;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.Events.StatusWorkflows;
using Wayd.Common.Domain.Events.StrategicManagement;
using Wayd.Common.Domain.Models;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Common.Models;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Migrators.MSSQL.Migrations;
using static Wayd.Infrastructure.IntegrationTests.Sut.Persistence.BaselineActivityAssertions;
using Wayd.Infrastructure.Persistence.Activities;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Infrastructure.Persistence.Initialization;
using Wayd.Organization.Domain.Enums;
using Wayd.Organization.Domain.Models;
using Wayd.Planning.Domain.Models.Iterations;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;
using PpmProgram = Wayd.ProjectPortfolioManagement.Domain.Models.Program;
using PpmStrategicTheme = Wayd.ProjectPortfolioManagement.Domain.Models.StrategicTheme;
using StrategicTheme = Wayd.StrategicManagement.Domain.Models.StrategicTheme;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Runs the Backfill-Baseline-Activity migration over records that predate their creation events.
/// </summary>
/// <remarks>
/// The migration writes every payload by hand in T-SQL, so the only proof that a baseline reads back exactly
/// as the serializer would have written it is a real SQL Server, real rows, and a comparison against the
/// entry <see cref="ActivityLogEntryFactory"/> builds for the same event.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class BackfillBaselineActivityMigrationTests(SqlServerDbContextFixture fixture)
{
    private const string MigrationBefore = "20260913214001_Refile-StrategicInitiative-Activity";

    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 9, 30, 0);
    private static readonly LocalDate Today = new(2026, 1, 15);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    private sealed record SeededRecords(
        Guid PortfolioId,
        Guid ProgramId,
        Guid ProjectId,
        Guid InitiativeId,
        Guid ThemeId,
        Guid OpenStartIterationId,
        Guid OpenEndIterationId,
        Guid TeamId,
        Guid TeamOfTeamsId,
        Guid WorkflowId,
        Guid ProductId,
        Guid CreatorEmployeeId,
        Guid StrayProjectEntryId)
    {
        public Guid[] All =>
        [
            PortfolioId, ProgramId, ProjectId, InitiativeId, ThemeId, OpenStartIterationId, OpenEndIterationId,
            TeamId, TeamOfTeamsId, WorkflowId, ProductId,
        ];
    }

    [Fact]
    public async Task Up_BaselinesEveryAggregate_ExactlyAsTheFactoryWouldRecordIt()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        try
        {
            await MigrateToBefore(ct);
            var seeded = await SeedEveryAggregateWithoutHistory(ct);

            // Act
            await using (var context = _fixture.CreateContext())
            {
                await context.Database.MigrateAsync(ct);
            }

            // Assert
            await using var verify = _fixture.CreateContext();
            var creator = seeded.CreatorEmployeeId;

            var projectRow = await SingleRow(verify, seeded.ProjectId, ct);
            projectRow.Id.Should().NotBe(seeded.StrayProjectEntryId, "the older entry the record already had is cleared");
            var project = await verify.Projects.AsNoTracking()
                .Include(p => p.Roles)
                .Include(p => p.StrategicThemeTags)
                .SingleAsync(p => p.Id == seeded.ProjectId, ct);
            project.Roles.Should().HaveCountGreaterThanOrEqualTo(3);
            project.StrategicThemeTags.Should().NotBeEmpty();
            AssertBaseline(projectRow, new ProjectBaselinedEvent(project.Id, project.Key, project.Name, project.Description,
                project.ExpenditureCategoryId, (int)project.Status, project.DateRange, project.PortfolioId, project.ProgramId,
                project.BusinessCase, project.ExpectedBenefits, RoleManager.ToRoleMap(project.Roles),
                [.. project.StrategicThemeTags.Select(t => t.StrategicThemeId)],
                await SystemCreated(verify.Projects, seeded.ProjectId, ct), creator, projectRow.Timestamp));

            var programRow = await SingleRow(verify, seeded.ProgramId, ct);
            var program = await verify.Programs.AsNoTracking()
                .Include(p => p.Roles)
                .Include(p => p.StrategicThemeTags)
                .SingleAsync(p => p.Id == seeded.ProgramId, ct);
            AssertBaseline(programRow, new ProgramBaselinedEvent(program.Id, program.Key, program.Name, program.Description,
                (int)program.Status, program.DateRange, program.PortfolioId, RoleManager.ToRoleMap(program.Roles),
                [.. program.StrategicThemeTags.Select(t => t.StrategicThemeId)],
                await SystemCreated(verify.Programs, seeded.ProgramId, ct), creator, programRow.Timestamp));

            var portfolioRow = await SingleRow(verify, seeded.PortfolioId, ct);
            var portfolio = await verify.Portfolios.AsNoTracking()
                .Include(p => p.Roles)
                .SingleAsync(p => p.Id == seeded.PortfolioId, ct);
            AssertBaseline(portfolioRow, new ProjectPortfolioBaselinedEvent(portfolio.Id, portfolio.Key, portfolio.Name,
                portfolio.Description, (int)portfolio.Status, RoleManager.ToRoleMap(portfolio.Roles),
                await SystemCreated(verify.Portfolios, seeded.PortfolioId, ct), creator, portfolioRow.Timestamp));

            var initiativeRow = await SingleRow(verify, seeded.InitiativeId, ct);
            var initiative = await verify.StrategicInitiatives.AsNoTracking()
                .Include(i => i.Roles)
                .SingleAsync(i => i.Id == seeded.InitiativeId, ct);
            AssertBaseline(initiativeRow, new StrategicInitiativeBaselinedEvent(initiative.PortfolioId, initiative.Id,
                initiative.Key, initiative.Name, initiative.Description, (int)initiative.Status, initiative.DateRange,
                RoleManager.ToRoleMap(initiative.Roles),
                await SystemCreated(verify.StrategicInitiatives, seeded.InitiativeId, ct), creator, initiativeRow.Timestamp));

            var themeRow = await SingleRow(verify, seeded.ThemeId, ct);
            var theme = await verify.StrategicThemes.AsNoTracking().SingleAsync(t => t.Id == seeded.ThemeId, ct);
            AssertBaseline(themeRow, new StrategicThemeBaselinedEvent(theme.Id, theme.Key, theme.Name, theme.Description,
                theme.State, await SystemCreated(verify.StrategicThemes, seeded.ThemeId, ct), creator, themeRow.Timestamp));

            foreach (var iterationId in new[] { seeded.OpenStartIterationId, seeded.OpenEndIterationId })
            {
                var iterationRow = await SingleRow(verify, iterationId, ct);
                var iteration = await verify.Iterations.AsNoTracking().SingleAsync(i => i.Id == iterationId, ct);
                AssertBaseline(iterationRow, new IterationBaselinedEvent(iteration.Id, iteration.Key, iteration.Name,
                    iteration.Type, iteration.State, iteration.DateRange, iteration.TeamId,
                    await SystemCreated(verify.Iterations, iterationId, ct), creator, iterationRow.Timestamp));
            }

            foreach (var teamId in new[] { seeded.TeamId, seeded.TeamOfTeamsId })
            {
                var teamRow = await SingleRow(verify, teamId, ct);
                var team = await verify.BaseTeams.AsNoTracking().SingleAsync(t => t.Id == teamId, ct);
                AssertBaseline(teamRow, new TeamBaselinedEvent(team.Id, team.Key, team.Code, team.Name, team.Description,
                    team.Type, team.ActiveDate, team.InactiveDate, team.IsActive,
                    await SystemCreated(verify.BaseTeams, teamId, ct), creator, teamRow.Timestamp));
            }

            var workflowRow = await SingleRow(verify, seeded.WorkflowId, ct);
            var workflow = await verify.StatusWorkflows.AsNoTracking()
                .Include(w => w.Statuses)
                .SingleAsync(w => w.Id == seeded.WorkflowId, ct);
            workflow.Statuses.Should().HaveCountGreaterThanOrEqualTo(2);
            AssertBaseline(workflowRow, new WorkflowBaselinedEvent(workflow.Id, workflow.Key, workflow.Name, workflow.Description,
                workflow.OwnerType, workflow.IsSystem, sourceWorkflowId: null,
                [.. workflow.Statuses.Select(s => new WorkflowStatusValues(s.Id, s.Name, s.Description, s.Category, s.Alias, s.Order))],
                await SystemCreated(verify.StatusWorkflows, seeded.WorkflowId, ct), creator, workflowRow.Timestamp));

            var productRow = await SingleRow(verify, seeded.ProductId, ct);
            var product = await verify.Products.AsNoTracking().SingleAsync(p => p.Id == seeded.ProductId, ct);
            AssertBaseline(productRow, new ProductBaselinedEvent(product.Id, product.Key, product.Name, product.Description,
                product.ProductTypeId, product.ParentId, product.StatusId, product.StatusCategory,
                await SystemCreated(verify.Products, seeded.ProductId, ct), creator, productRow.Timestamp));
        }
        finally
        {
            await RestoreLatest(ct);
        }
    }

    [Fact]
    public async Task Up_RunAgain_WritesNothing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        try
        {
            await MigrateToBefore(ct);
            var seeded = await SeedEveryAggregateWithoutHistory(ct);

            await using (var context = _fixture.CreateContext())
            {
                await context.Database.MigrateAsync(ct);
            }

            var before = await Snapshot(seeded.All, ct);
            before.Should().HaveCount(seeded.All.Length);

            // Act
            // Sent as a plain command, as the migration sends it: ExecuteSqlRaw runs the text through
            // string.Format even with no parameters, and the payload templates are full of braces.
            await using (var context = _fixture.CreateContext())
            {
                await context.Database.OpenConnectionAsync(ct);
                await using var command = context.Database.GetDbConnection().CreateCommand();
                command.CommandText = BackfillBaselineActivity.UpSql;
                await command.ExecuteNonQueryAsync(ct);
            }

            // Assert
            var after = await Snapshot(seeded.All, ct);
            after.Should().BeEquivalentTo(before);
        }
        finally
        {
            await RestoreLatest(ct);
        }
    }

    [Fact]
    public async Task Up_LeavesARecordThatHasACreationEntry()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        try
        {
            await MigrateToBefore(ct);

            Guid themeId;
            await using (var context = _fixture.CreateContext())
            {
                var theme = StrategicTheme.Create($"Atlas Kept {UniqueCode("", 8)}", "Atlas theme", StrategicThemeState.Proposed, EventActor.System, Now);
                context.StrategicThemes.Add(theme);
                await context.SaveChangesAsync(ct);
                themeId = theme.Id;

                context.ActivityLogs.Add(new ActivityLogEntry(
                    Guid.CreateVersion7(), nameof(StrategicThemeDetailsUpdatedEvent), ActivityCategory.Updated, "StrategicManagement",
                    "StrategicTheme", themeId, EventActor.System, Now.Plus(Duration.FromHours(1)), ordinal: 0, correlationId: null,
                    "{}", "Strategic Theme Details Updated"));
                await context.SaveChangesAsync(ct);
            }

            var before = await Snapshot([themeId], ct);
            before.Should().Contain(r => r.EventType == nameof(StrategicThemeCreatedEvent));
            before.Should().HaveCount(2);

            // Act
            await using (var context = _fixture.CreateContext())
            {
                await context.Database.MigrateAsync(ct);
            }

            // Assert
            var after = await Snapshot([themeId], ct);
            after.Should().BeEquivalentTo(before);
            after.Should().NotContain(r => r.Category == ActivityCategory.Baseline);
        }
        finally
        {
            await RestoreLatest(ct);
        }
    }

    [Fact]
    public async Task Up_SkipsSoftDeletedTeams()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        try
        {
            await MigrateToBefore(ct);

            Guid teamId;
            await using (var context = _fixture.CreateContext())
            {
                var team = Team.Create($"Atlas Retired {UniqueCode("", 8)}", new TeamCode(UniqueCode("R", 9)), null, new LocalDate(2024, 1, 2),
                    Methodology.Scrum, SizingMethod.StoryPoints, EventActor.System, Now);
                context.Teams.Add(team);
                await context.SaveChangesAsync(ct);
                teamId = team.Id;

                context.Teams.Remove(team);
                await context.SaveChangesAsync(ct);

                await context.ActivityLogs.Where(a => a.AggregateId == teamId).ExecuteDeleteAsync(ct);
            }

            await using (var check = _fixture.CreateContext())
            {
                var isDeleted = await check.Teams.IgnoreQueryFilters()
                    .Where(t => t.Id == teamId)
                    .Select(t => t.IsDeleted)
                    .SingleAsync(ct);
                isDeleted.Should().BeTrue("the removal is a soft delete");
            }

            // Act
            await using (var context = _fixture.CreateContext())
            {
                await context.Database.MigrateAsync(ct);
            }

            // Assert
            var after = await Snapshot([teamId], ct);
            after.Should().BeEmpty();
        }
        finally
        {
            await RestoreLatest(ct);
        }
    }

    [Fact]
    public async Task Down_RemovesTheBaselines()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        try
        {
            await MigrateToBefore(ct);

            Guid themeId;
            await using (var context = _fixture.CreateContext())
            {
                var theme = StrategicTheme.Create($"Atlas Reverted {UniqueCode("", 8)}", "Atlas theme", StrategicThemeState.Active, EventActor.System, Now);
                context.StrategicThemes.Add(theme);
                await context.SaveChangesAsync(ct);
                themeId = theme.Id;

                await context.ActivityLogs.Where(a => a.AggregateId == themeId).ExecuteDeleteAsync(ct);
                await context.Database.MigrateAsync(ct);
            }

            var baselined = await Snapshot([themeId], ct);
            baselined.Should().ContainSingle().Which.Category.Should().Be(ActivityCategory.Baseline);

            // Act
            await MigrateToBefore(ct);

            // Assert
            var after = await Snapshot([themeId], ct);
            after.Should().BeEmpty();
        }
        finally
        {
            await RestoreLatest(ct);
        }
    }

    private async Task MigrateToBefore(CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(MigrationBefore, ct);
    }

    private async Task RestoreLatest(CancellationToken ct)
    {
        // Leave the shared database at the latest migration for the tests that follow.
        await using var restore = _fixture.CreateContext();
        await restore.Database.MigrateAsync(ct);
    }

    /// <summary>
    /// Creates one record of every baselined aggregate through the domain, then removes the entries saving them
    /// wrote, as for records that existed before their events did. The project keeps one older entry of another
    /// kind, which the migration has to clear.
    /// </summary>
    private async Task<SeededRecords> SeedEveryAggregateWithoutHistory(CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();

        var ada = NewEmployee("Ada", "Lovelace");
        var grace = NewEmployee("Grace", "Hopper");
        var alan = NewEmployee("Alan", "Turing");
        context.Employees.AddRange(ada, grace, alan);
        await context.SaveChangesAsync(ct);

        await LinkFixtureUserTo(context, ada.Id, ct);

        // Several of these names are unique per table, and the shared database keeps every earlier run's rows.
        var suffix = UniqueCode("", 8);

        var category = ExpenditureCategory.Create($"Atlas Spend {suffix}", "Atlas spend", isCapitalizable: true, requiresDepreciation: false);
        context.ExpenditureCategories.Add(category);

        var theme = StrategicTheme.Create($"Atlas Theme {suffix}", "Atlas \"strategic\" theme", StrategicThemeState.Active, EventActor.System, Now);
        context.StrategicThemes.Add(theme);
        await context.SaveChangesAsync(ct);

        context.PpmStrategicThemes.Add(new PpmStrategicTheme(theme, Now));

        var portfolio = ProjectPortfolio.Create($"Atlas Portfolio {suffix}", "Atlas portfolio",
            new Dictionary<ProjectPortfolioRole, HashSet<Guid>> { [ProjectPortfolioRole.Owner] = [ada.Id] }, EventActor.System, Now);
        context.Portfolios.Add(portfolio);
        await context.SaveChangesAsync(ct);

        var actor = new PpmActor(ada.Id, IsPpmAdministrator: true, FixtureUserId);
        Succeeded(portfolio.Activate(actor, Today, Now));

        var range = new LocalDateRange(new LocalDate(2026, 2, 2), new LocalDate(2026, 8, 31));

        var program = portfolio.CreateProgram($"Atlas Program {suffix}", "Atlas program", range,
            new Dictionary<ProgramRole, HashSet<Guid>> { [ProgramRole.Owner] = [ada.Id], [ProgramRole.Manager] = [grace.Id, alan.Id] },
            [theme.Id], EventActor.System, Now);
        Succeeded(program);
        Succeeded(program.Value.Activate(actor, ProgramAncestryRoles.None, Now));

        var project = portfolio.CreateProject($"Atlas Project {suffix}", "Atlas\nproject", new ProjectKey(UniqueCode("AT", 10)), category.Id,
            range, program.Value.Id, "A \"business\" case\\", expectedBenefits: null,
            new Dictionary<ProjectRole, HashSet<Guid>>
            {
                [ProjectRole.Sponsor] = [ada.Id],
                [ProjectRole.Manager] = [grace.Id, alan.Id],
            },
            [theme.Id], Now, actor);
        Succeeded(project);

        var initiative = portfolio.CreateStrategicInitiative($"Atlas Initiative {suffix}", "Atlas initiative", range,
            new Dictionary<StrategicInitiativeRole, HashSet<Guid>> { [StrategicInitiativeRole.Owner] = [grace.Id] }, EventActor.System, Now);
        Succeeded(initiative);
        await context.SaveChangesAsync(ct);

        var team = Team.Create($"Atlas Team {suffix}", new TeamCode(UniqueCode("T", 9)), "Atlas team", new LocalDate(2024, 1, 2),
            Methodology.Scrum, SizingMethod.StoryPoints, EventActor.System, Now);
        var teamOfTeams = TeamOfTeams.Create($"Atlas ART {suffix}", new TeamCode(UniqueCode("A", 9)), null, new LocalDate(2024, 1, 2), EventActor.System, Now);
        context.Teams.Add(team);
        context.TeamOfTeams.Add(teamOfTeams);
        await context.SaveChangesAsync(ct);
        Succeeded(teamOfTeams.Deactivate(TeamDeactivatableArgs.Create(new LocalDate(2025, 6, 30), EventActor.System, Now)));

        var openStart = Iteration.Create($"Atlas Sprint 1 {suffix}", IterationType.Sprint, IterationState.Completed,
            new IterationDateRange(null, Instant.FromUtc(2026, 1, 14, 17, 45, 30).Plus(Duration.FromMilliseconds(250))),
            null, OwnershipInfo.CreateWaydOwned(), [], EventActor.System, Now);
        var openEnd = Iteration.Create($"Atlas Sprint 2 {suffix}", IterationType.Sprint, IterationState.Active,
            new IterationDateRange(Instant.FromUtc(2026, 1, 15, 13, 0, 0), null),
            null, OwnershipInfo.CreateWaydOwned(), [], EventActor.System, Now);
        context.Iterations.AddRange(openStart, openEnd);

        ProductWorkflowOwners.Register();
        var workflow = StatusWorkflow.Create($"Atlas Flow {suffix}", "Atlas \\ flow", ProductWorkflowOwners.Product.Key, EventActor.System, Now).Value;
        workflow.AddStatus("Atlas Draft", "Not yet \"live\"", StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, Now);
        workflow.AddStatus("Atlas Live", null, StatusCategory.Active, StatusWorkflow.NoAlias, EventActor.System, Now);
        context.StatusWorkflows.Add(workflow);
        await context.SaveChangesAsync(ct);

        var productWorkflow = await ProductWorkflow(context, ct);
        var productType = await ProductType(context, ct);
        var product = Product.Create($"Atlas Product {suffix}", null, productType, null, null,
            StatusRef.From(productWorkflow.Statuses.OrderBy(s => s.Order).First()), EventActor.System, Now);
        context.Products.Add(product);
        await context.SaveChangesAsync(ct);

        var seeded = new SeededRecords(portfolio.Id, program.Value.Id, project.Value.Id, initiative.Value.Id, theme.Id,
            openStart.Id, openEnd.Id, team.Id, teamOfTeams.Id, workflow.Id, product.Id, ada.Id, Guid.CreateVersion7());

        var ids = seeded.All;
        await context.ActivityLogs.Where(a => ids.Contains(a.AggregateId)).ExecuteDeleteAsync(ct);

        context.ActivityLogs.Add(new ActivityLogEntry(
            seeded.StrayProjectEntryId, nameof(ProjectDetailsUpdatedEvent), ActivityCategory.Updated, "Ppm", "Project",
            seeded.ProjectId, EventActor.System, Instant.FromUtc(2025, 6, 1, 12, 0, 0), ordinal: 0, correlationId: null,
            "{}", "Project Details Updated"));
        await context.SaveChangesAsync(ct);

        return seeded;
    }

    private static Employee NewEmployee(string firstName, string lastName) =>
        Employee.Create(
            new PersonName(firstName, null, lastName),
            UniqueCode("E", 12),
            Now,
            new EmailAddress($"{firstName.ToLowerInvariant()}.{UniqueCode("", 8).ToLowerInvariant()}@acme.example"),
            jobTitle: null,
            department: null,
            officeLocation: null,
            managerId: null,
            isActive: true,
            employeeType: null,
            Now);

    private static async Task<StatusWorkflow> ProductWorkflow(WaydDbContext context, CancellationToken ct)
    {
        var workflow = await context.StatusWorkflows
            .Include(w => w.Statuses)
            .FirstOrDefaultAsync(w => w.OwnerType == ProductWorkflowOwners.Product.Key && w.IsSystem, ct);

        if (workflow is not null)
        {
            return workflow;
        }

        await new ProductManagementWorkflowSeeder().Initialize(context, DateTimeProvider(), ct);

        return await context.StatusWorkflows
            .Include(w => w.Statuses)
            .FirstAsync(w => w.OwnerType == ProductWorkflowOwners.Product.Key && w.IsSystem, ct);
    }

    private static async Task<Guid> ProductType(WaydDbContext context, CancellationToken ct)
    {
        var existing = await context.ProductTypes.Select(t => t.Id).FirstOrDefaultAsync(ct);
        if (existing != Guid.Empty)
        {
            return existing;
        }

        await new ProductTypeSeeder().Initialize(context, DateTimeProvider(), ct);

        return await context.ProductTypes.Select(t => t.Id).FirstAsync(ct);
    }

    private static IDateTimeProvider DateTimeProvider()
    {
        var provider = new Mock<IDateTimeProvider>();
        provider.SetupGet(d => d.Now).Returns(Now);
        provider.SetupGet(d => d.Today).Returns(Today);

        return provider.Object;
    }

    private static string UniqueCode(string prefix, int length) =>
        (prefix + Guid.NewGuid().ToString("N").ToUpperInvariant())[..length];

    private static void Succeeded(CSharpFunctionalExtensions.Result result) =>
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

    private static void Succeeded<T>(CSharpFunctionalExtensions.Result<T> result) =>
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

    private sealed record RowSnapshot(Guid Id, Guid AggregateId, string EventType, ActivityCategory Category, string Payload, Instant Timestamp);

    private async Task<List<RowSnapshot>> Snapshot(Guid[] aggregateIds, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        return await context.ActivityLogs.AsNoTracking()
            .Where(a => aggregateIds.Contains(a.AggregateId))
            .Select(a => new RowSnapshot(a.Id, a.AggregateId, a.EventType, a.Category, a.Payload, a.Timestamp))
            .ToListAsync(ct);
    }
}
