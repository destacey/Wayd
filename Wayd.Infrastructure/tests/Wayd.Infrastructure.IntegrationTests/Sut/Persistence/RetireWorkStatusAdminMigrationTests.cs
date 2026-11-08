using Microsoft.EntityFrameworkCore;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Migrators.MSSQL.Migrations;
using Wayd.Work.Domain;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves the Retire-WorkStatus-Admin migration copies each shared work status's description onto the work item
/// statuses of the same name, and removes the work status permissions from every role.
/// </summary>
/// <remarks>The SQL is hand-written and joins three modules' tables, so only a real SQL Server shows what it matches.</remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class RetireWorkStatusAdminMigrationTests(SqlServerDbContextFixture fixture)
{
    private const string WidgetType = "test.retire-work-status.widget";
    private static readonly WorkflowOwnerDescriptor Widget = new(WidgetType, "Widget", new Dictionary<int, WorkflowAlias>(), [], hasManagedWorkflows: true);
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 8, 12, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task CopyDescriptions_ShouldCopyOntoEveryWorkItemStatusOfTheName_AndOnlyThere()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var name = UniqueName("Review");
        var described = UniqueName("Described");
        await SeedWorkStatus(name, "  Waiting on a reviewer  ", isDeleted: false, ct);
        await SeedWorkStatus(described, "From the shared status", isDeleted: false, ct);
        var agile = await SeedWorkflow(WorkWorkflowOwners.WorkItem.Key, [name, described], ct);
        var scrum = await SeedWorkflow(WorkWorkflowOwners.WorkItem.Key, [name], ct);
        var widget = await SeedWorkflow(WidgetType, [name], ct);
        await SetDescription(agile, described, "Written in Wayd", ct);

        // Act
        await Run(RetireWorkStatusAdmin.CopyDescriptionsSql, ct);

        // Assert
        (await DescriptionOf(agile, name, ct)).Should().Be("Waiting on a reviewer");
        (await DescriptionOf(scrum, name, ct)).Should().Be("Waiting on a reviewer");
        (await DescriptionOf(widget, name, ct)).Should().BeNull("only work item statuses were made from work statuses");
        (await DescriptionOf(agile, described, ct)).Should().Be("Written in Wayd", "a status that has a description keeps it");
    }

    [Fact]
    public async Task CopyDescriptions_ShouldLeaveAWorkflowWaydOwns()
    {
        // Arrange — a native work item workflow was never made from a shared work status
        var ct = TestContext.Current.CancellationToken;
        var name = UniqueName("Owned");
        await SeedWorkStatus(name, "From the shared status", isDeleted: false, ct);
        WorkWorkflowOwners.Register();
        var owned = StatusWorkflow.Create($"Owned {Guid.NewGuid():N}", null, WorkWorkflowOwners.WorkItem.Key, EventActor.System, Now).Value;
        owned.AddStatus(name, null, StatusCategory.Active, StatusWorkflow.NoAlias, EventActor.System, Now).IsSuccess.Should().BeTrue();
        await using (var context = _fixture.CreateContext())
        {
            context.StatusWorkflows.Add(owned);
            await context.SaveChangesAsync(ct);
        }

        // Act
        await Run(RetireWorkStatusAdmin.CopyDescriptionsSql, ct);

        // Assert
        (await DescriptionOf(owned.Id, name, ct)).Should().BeNull();
    }

    [Fact]
    public async Task CopyDescriptions_ShouldTrimLineBreaksAndTabs()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var name = UniqueName("Breaks");
        var blank = UniqueName("Breaks only");
        await SeedWorkStatus(name, "\r\n\tReady for review \r\n", isDeleted: false, ct);
        await SeedWorkStatus(blank, "\r\n\t ", isDeleted: false, ct);
        var workflow = await SeedWorkflow(WorkWorkflowOwners.WorkItem.Key, [name, blank], ct);

        // Act
        await Run(RetireWorkStatusAdmin.CopyDescriptionsSql, ct);

        // Assert
        (await DescriptionOf(workflow, name, ct)).Should().Be("Ready for review");
        (await DescriptionOf(workflow, blank, ct)).Should().BeNull();
    }

    [Fact]
    public async Task CopyDescriptions_ShouldSkipADeletedOrBlankWorkStatus()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var deleted = UniqueName("Deleted");
        var blank = UniqueName("Blank");
        await SeedWorkStatus(deleted, "Gone", isDeleted: true, ct);
        await SeedWorkStatus(blank, "   ", isDeleted: false, ct);
        var workflow = await SeedWorkflow(WorkWorkflowOwners.WorkItem.Key, [deleted, blank], ct);

        // Act
        await Run(RetireWorkStatusAdmin.CopyDescriptionsSql, ct);

        // Assert
        (await DescriptionOf(workflow, deleted, ct)).Should().BeNull();
        (await DescriptionOf(workflow, blank, ct)).Should().BeNull();
    }

    [Fact]
    public async Task CopyDescriptions_ShouldCutALongDescriptionToWhatAStatusHolds()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var name = UniqueName("Long");
        await SeedWorkStatus(name, new string('x', 511) + " " + new string('y', 400), isDeleted: false, ct);
        var workflow = await SeedWorkflow(WorkWorkflowOwners.WorkItem.Key, [name], ct);

        // Act
        await Run(RetireWorkStatusAdmin.CopyDescriptionsSql, ct);

        // Assert
        (await DescriptionOf(workflow, name, ct)).Should().Be(new string('x', 511), "the cut is trimmed too");
    }

    [Fact]
    public async Task RemoveWorkStatusPermissions_ShouldRemoveOnlyTheWorkStatusClaims()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var roleId = Guid.NewGuid().ToString();
        await using (var context = _fixture.CreateContext())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO [Identity].[Roles] ([Id], [Name], [NormalizedName]) VALUES ({roleId}, {roleId}, {roleId.ToUpperInvariant()});
                INSERT INTO [Identity].[RoleClaims] ([RoleId], [ClaimType], [ClaimValue], [Created])
                VALUES ({roleId}, 'permission', 'Permissions.WorkStatuses.View', SYSUTCDATETIME()),
                       ({roleId}, 'permission', 'Permissions.WorkStatuses.Create', SYSUTCDATETIME()),
                       ({roleId}, 'permission', 'Permissions.WorkStatuses.Update', SYSUTCDATETIME()),
                       ({roleId}, 'permission', 'Permissions.WorkStatuses.Delete', SYSUTCDATETIME()),
                       ({roleId}, 'permission', 'Permissions.WorkStatusCategories.View', SYSUTCDATETIME()),
                       ({roleId}, 'permission', 'Permissions.WorkProcesses.View', SYSUTCDATETIME());
                """,
                ct);
        }

        // Act
        await Run(RetireWorkStatusAdmin.RemoveWorkStatusPermissionsSql, ct);

        // Assert
        await using var assertContext = _fixture.CreateContext();
        var claims = await assertContext.Database
            .SqlQuery<string>($"SELECT [ClaimValue] AS [Value] FROM [Identity].[RoleClaims] WHERE [RoleId] = {roleId}")
            .ToListAsync(ct);
        claims.Should().BeEquivalentTo(["Permissions.WorkStatusCategories.View", "Permissions.WorkProcesses.View"]);
    }

    private static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private async Task SeedWorkStatus(string name, string description, bool isDeleted, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO [Work].[WorkStatuses] ([Name], [Description], [IsActive], [IsDeleted], [SystemCreated], [SystemLastModified])
            VALUES ({name}, {description}, 1, {isDeleted}, SYSUTCDATETIME(), SYSUTCDATETIME());
            """,
            ct);
    }

    private async Task<Guid> SeedWorkflow(string ownerType, IReadOnlyList<string> statusNames, CancellationToken ct)
    {
        WorkWorkflowOwners.Register();
        WorkflowOwners.Register(Widget);
        var key = Guid.NewGuid().ToString("N");
        var workflow = StatusWorkflow.CreateManaged($"Retire {key}", ownerType, $"retire:{key}",
            [.. statusNames.Select((n, i) => new SourceStatus($"s{i}", n, StatusCategory.Active))], EventActor.System, Now).Value;

        await using var context = _fixture.CreateContext();
        context.StatusWorkflows.Add(workflow);
        await context.SaveChangesAsync(ct);

        return workflow.Id;
    }

    private async Task SetDescription(Guid workflowId, string statusName, string description, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [StatusWorkflows].[WorkflowStatuses] SET [Description] = {description} WHERE [WorkflowId] = {workflowId} AND [Name] = {statusName};",
            ct);
    }

    private async Task<string?> DescriptionOf(Guid workflowId, string statusName, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        var workflow = await context.StatusWorkflows.AsNoTracking().Include(w => w.Statuses).SingleAsync(w => w.Id == workflowId, ct);
        return workflow.Statuses.Single(s => s.Name == statusName).Description;
    }

    private async Task Run(string sql, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync(sql, ct);
    }
}
