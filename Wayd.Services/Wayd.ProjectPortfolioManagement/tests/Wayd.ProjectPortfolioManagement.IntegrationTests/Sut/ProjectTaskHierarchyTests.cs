using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Proves a project's task tree stays correct through each change a user can make to it, against a real SQL
/// Server container.
/// </summary>
/// <remarks>
/// <para>
/// The tree is <see cref="ProjectTask.Parent"/> and <see cref="ProjectTask.Children"/>, which EF builds from
/// <c>ParentId</c> when a project is loaded. It does not keep them current afterwards: with snapshot change
/// tracking, setting a foreign key leaves the navigations alone until <c>DetectChanges</c> runs, normally at
/// the save. A domain method that changes a parent and then walks the tree to roll dates up would otherwise
/// walk the tree as it was — through the task's old ancestors rather than its new ones.
/// </para>
/// <para>
/// So every assertion here is made twice: once <em>before</em> the save, which is where a stale tree shows,
/// and once after a reload in a fresh context, which is what was persisted. Each project is loaded exactly as
/// the task handlers load it, so the tree under test is the one EF built rather than one the test assembled.
/// The in-memory fakes perform no relationship fixup, so none of this is observable through them.
/// </para>
/// <para>
/// Two invariants are checked, and each needs the other. <see cref="AssertTreeMatchesParentIds"/> — every
/// task's navigations agree with its <c>ParentId</c>. <see cref="AssertParentsContainTheirChildren"/> — a
/// parent's range covers each dated child's. The rollup only ever widens a parent, so after a move the old
/// parent still covers the task it lost; containment alone would pass on a stale tree by checking that
/// parent instead of the new one.
/// </para>
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class ProjectTaskHierarchyTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    private static readonly LocalDate Day0 = new(2026, 3, 2);

    [Fact]
    public async Task CreateTask_UnderALoadedHierarchy_LinksTheTaskAndRollsItsDatesUpThroughEveryAncestor()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await Seed(ct);

        Guid parentId, grandparentId;
        await using (var context = _fixture.CreateContext())
        {
            var project = await Load(context, seed.ProjectId, ct);
            var grandparent = CreateTask(project, 1, "Grandparent", seed.StageId, dates: null);
            var parent = CreateTask(project, 2, "Parent", grandparent.Id, Range(0, 10));
            await context.SaveChangesAsync(ct);
            (grandparentId, parentId) = (grandparent.Id, parent.Id);
        }

        // Act
        Guid childId;
        await using (var context = _fixture.CreateContext())
        {
            var project = await Load(context, seed.ProjectId, ct);
            var child = CreateTask(project, 3, "Child", parentId, Range(40, 60));
            childId = child.Id;

            // Assert — before the save
            AssertTreeMatchesParentIds(project);
            AssertParentsContainTheirChildren(project);
            TaskIn(project, childId).Parent!.Id.Should().Be(parentId);
            TaskIn(project, parentId).Parent!.Id.Should().Be(grandparentId);

            await context.SaveChangesAsync(ct);
        }

        // Assert — as persisted
        await using var verify = _fixture.CreateContext();
        var reloaded = await Load(verify, seed.ProjectId, ct);
        AssertTreeMatchesParentIds(reloaded);
        AssertParentsContainTheirChildren(reloaded);
        TaskIn(reloaded, childId).ParentId.Should().Be(parentId);
    }

    [Fact]
    public async Task ChangeTaskPlacement_ToAnotherParent_MovesItInTheTreeAndRollsItsDatesUpThroughTheNewAncestors()
    {
        // Arrange — two branches, and a task on the first that sits well outside the second's dates.
        var ct = TestContext.Current.CancellationToken;
        var seed = await Seed(ct);

        Guid oldParentId, newParentId, movedId;
        await using (var context = _fixture.CreateContext())
        {
            var project = await Load(context, seed.ProjectId, ct);
            var oldParent = CreateTask(project, 1, "Old parent", seed.StageId, dates: null);
            var moved = CreateTask(project, 2, "Moved", oldParent.Id, Range(90, 120));
            var newParent = CreateTask(project, 3, "New parent", seed.StageId, dates: null);
            CreateTask(project, 4, "New sibling", newParent.Id, Range(0, 10));
            await context.SaveChangesAsync(ct);
            (oldParentId, newParentId, movedId) = (oldParent.Id, newParent.Id, moved.Id);
        }

        // Act
        await using (var context = _fixture.CreateContext())
        {
            var project = await Load(context, seed.ProjectId, ct);
            var result = project.ChangeTaskPlacement(movedId, newParentId, order: null);
            result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

            // Assert — before the save, where the navigations would still name the old parent
            AssertTreeMatchesParentIds(project);
            AssertParentsContainTheirChildren(project);
            TaskIn(project, movedId).Parent!.Id.Should().Be(newParentId);
            TaskIn(project, oldParentId).Children.Should().NotContain(t => t.Id == movedId);
            TaskIn(project, newParentId).Children.Should().Contain(t => t.Id == movedId);

            await context.SaveChangesAsync(ct);
        }

        // Assert — as persisted
        await using var verify = _fixture.CreateContext();
        var reloaded = await Load(verify, seed.ProjectId, ct);
        AssertTreeMatchesParentIds(reloaded);
        AssertParentsContainTheirChildren(reloaded);
        TaskIn(reloaded, movedId).ParentId.Should().Be(newParentId);
    }

    [Fact]
    public async Task UpdateTaskDates_OnALeafOfALoadedHierarchy_RollsTheChangeUpThroughEveryAncestor()
    {
        // Arrange — three levels, so the rollup has to walk more than one link.
        var ct = TestContext.Current.CancellationToken;
        var seed = await Seed(ct);

        Guid leafId;
        await using (var context = _fixture.CreateContext())
        {
            var project = await Load(context, seed.ProjectId, ct);
            var top = CreateTask(project, 1, "Top", seed.StageId, dates: null);
            var middle = CreateTask(project, 2, "Middle", top.Id, dates: null);
            var leaf = CreateTask(project, 3, "Leaf", middle.Id, Range(0, 10));
            await context.SaveChangesAsync(ct);
            leafId = leaf.Id;
        }

        // Act
        await using (var context = _fixture.CreateContext())
        {
            var project = await Load(context, seed.ProjectId, ct);
            var result = project.UpdateTaskDates(leafId, Range(200, 240), plannedDate: null, parentChanging: false);
            result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

            // Assert — before the save
            AssertTreeMatchesParentIds(project);
            AssertParentsContainTheirChildren(project);

            await context.SaveChangesAsync(ct);
        }

        // Assert — as persisted
        await using var verify = _fixture.CreateContext();
        var reloaded = await Load(verify, seed.ProjectId, ct);
        AssertTreeMatchesParentIds(reloaded);
        AssertParentsContainTheirChildren(reloaded);
        TaskIn(reloaded, leafId).PlannedDateRange.Should().Be(Range(200, 240));
    }

    /// <summary>Every task's navigations agree with its <c>ParentId</c>, in both directions.</summary>
    private static void AssertTreeMatchesParentIds(Project project)
    {
        var tasks = project.Tasks.ToList();

        foreach (var task in tasks)
        {
            (task.Parent?.Id).Should().Be(task.ParentId, "{0}'s Parent must be the task its ParentId names", task.Name);

            var expectedChildren = tasks.Where(t => t.ParentId == task.Id).Select(t => t.Id);
            task.Children.Select(c => c.Id).Should().BeEquivalentTo(expectedChildren,
                "{0}'s Children must be exactly the tasks naming it as their parent", task.Name);
        }
    }

    /// <summary>A parent's range covers the range of each dated child.</summary>
    private static void AssertParentsContainTheirChildren(Project project)
    {
        foreach (var parent in project.Tasks)
        {
            foreach (var child in parent.Children.Where(c => c.PlannedDateRange is not null))
            {
                parent.PlannedDateRange.Should().NotBeNull("{0} has a dated child, {1}", parent.Name, child.Name);
                parent.PlannedDateRange!.Start.Should().BeLessThanOrEqualTo(child.PlannedDateRange!.Start,
                    "{0} must start no later than its child {1}", parent.Name, child.Name);

                if (child.PlannedDateRange.End is null)
                {
                    parent.PlannedDateRange.End.Should().BeNull("{0} has an open-ended child, {1}", parent.Name, child.Name);
                }
                else if (parent.PlannedDateRange.End is { } parentEnd)
                {
                    parentEnd.Should().BeGreaterThanOrEqualTo(child.PlannedDateRange.End.Value,
                        "{0} must end no earlier than its child {1}", parent.Name, child.Name);
                }
            }
        }
    }

    /// <summary>Loads a project exactly as the task command handlers do, so EF builds the tree.</summary>
    private static Task<Project> Load(WaydDbContext context, Guid projectId, CancellationToken ct) =>
        context.Projects
            .Include(p => p.Stages)
            .Include(p => p.Tasks)
            .SingleAsync(p => p.Id == projectId, ct);

    private static ProjectTask TaskIn(Project project, Guid taskId) => project.Tasks.Single(t => t.Id == taskId);

    private static ProjectTask CreateTask(Project project, int number, string name, Guid parentId, FlexibleDateRange? dates)
    {
        var result = project.CreateTask(
            nextNumber: number,
            name: name,
            description: null,
            type: ProjectTaskType.Task,
            status: TaskStatus.NotStarted,
            priority: TaskPriority.Medium,
            progress: null,
            parentId: parentId,
            plannedDateRange: dates,
            plannedDate: null,
            estimatedEffortHours: null,
            roles: null);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        return result.Value;
    }

    private static FlexibleDateRange Range(int startDay, int endDay) =>
        new(Day0.PlusDays(startDay), Day0.PlusDays(endDay));

    /// <summary>A project in an active portfolio, following a one-stage lifecycle so tasks can be created.</summary>
    private async Task<(Guid ProjectId, Guid StageId)> Seed(CancellationToken ct)
    {
        await _fixture.ResetPpmData(ct);
        await using var context = _fixture.CreateContext();

        var category = ExpenditureCategory.Create("Capital", "Capital spend", isCapitalizable: true, requiresDepreciation: true);
        await context.ExpenditureCategories.AddAsync(category, ct);

        var lifecycle = ProjectLifecycle.Create("Standard", "Standard delivery lifecycle", [("Delivery", "Delivery stage")]);
        lifecycle.Activate();
        await context.ProjectLifecycles.AddAsync(lifecycle, ct);

        var portfolio = ProjectPortfolio.Create(
            "Delivery", "Delivery portfolio", null, EventActor.System, SqlServerDbContextFixture.FixedNow);
        await context.Portfolios.AddAsync(portfolio, ct);
        await context.SaveChangesAsync(ct);

        portfolio.Activate(PpmActor.System, SqlServerDbContextFixture.FixedNow.InUtc().Date, SqlServerDbContextFixture.FixedNow);
        var project = portfolio.CreateProject(
            "Gemini",
            "Gemini description",
            new ProjectKey("GEMINI"),
            category.Id,
            dateRange: null,
            programId: null,
            businessCase: null,
            expectedBenefits: null,
            roles: null,
            strategicThemes: null,
            SqlServerDbContextFixture.FixedNow,
            PpmActor.System).Value;
        project.AssignLifecycle(PpmActor.System, ProjectAncestryRoles.None, lifecycle, SqlServerDbContextFixture.FixedNow);
        await context.SaveChangesAsync(ct);

        return (project.Id, project.Stages.Single().Id);
    }
}
