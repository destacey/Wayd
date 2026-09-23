using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Models;
using Wayd.Tests.Shared;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;

namespace Wayd.Work.Application.Tests.Infrastructure;

/// <summary>
/// Work items, teams and completion history for forecast tests, in one workspace ("TEST").
/// </summary>
public sealed class ForecastScenario : IDisposable
{
    public static readonly Instant Now = Instant.FromUtc(2026, 9, 22, 12, 0);

    /// <summary>
    /// The first simulated day for <see cref="Now"/>.
    /// </summary>
    public static readonly LocalDate Start = new(2026, 9, 22);

    private readonly Workspace _workspace = new WorkspaceFaker().AsExternal().WithKey(new WorkspaceKey("TEST")).Generate();
    private int _nextNumber = 1;

    public FakeWorkDbContext Context { get; } = new();

    public TestingDateTimeProvider DateTimeProvider { get; } = new(new FakeClock(Now));

    public WorkType Story { get; } = new WorkTypeFaker().AsStory().Generate();
    public WorkType Feature { get; } = new WorkTypeFaker().AsFeature().Generate();
    public WorkType Epic { get; } = new WorkTypeFaker().AsEpic().Generate();

    public void Dispose() => Context.Dispose();

    public Guid NewTeam()
    {
        var team = new WorkTeamFaker(TeamType.Team).Generate();
        Context.AddWorkTeam(team);
        return team.Id;
    }

    public WorkItem AddItem(
        Guid? teamId,
        WorkStatusCategory statusCategory = WorkStatusCategory.Proposed,
        double stackRank = 1,
        WorkType? type = null,
        Instant? done = null,
        Instant? created = null,
        Guid? parentId = null,
        Guid? projectId = null,
        Guid? parentProjectId = null)
    {
        var item = new WorkItemFaker()
            .WithWorkspace(_workspace)
            .WithKey(new WorkItemKey(_workspace.Key, _nextNumber++))
            .WithType(type ?? Story)
            .WithTeamId(teamId)
            .WithParentId(parentId)
            .WithProjectId(projectId)
            .WithParentProjectId(parentProjectId)
            .WithStatusCategory(statusCategory)
            .WithStackRank(stackRank)
            .WithCreated(created ?? Instant.FromUtc(2026, 1, 1, 0, 0))
            .WithDoneTimestamp(done)
            .Generate();

        Context.AddWorkItem(item);
        return item;
    }

    public WorkItem AddDoneItem(Guid? teamId, Guid? parentId = null) =>
        AddItem(teamId, WorkStatusCategory.Done, done: Now.Minus(Duration.FromDays(200)), parentId: parentId);

    /// <summary>
    /// One item finished on each of the given number of days before today — a pace that makes
    /// the item at backlog position n finish on day n in every trial.
    /// </summary>
    public void AddHistory(Guid teamId, int days = 90)
    {
        for (var day = 1; day <= days; day++)
            AddItem(teamId, WorkStatusCategory.Done, done: Now.Minus(Duration.FromDays(day)));
    }

    public void AddDependency(WorkItem predecessor, WorkItem successor) =>
        Context.AddWorkItemDependency(new WorkItemDependencyFaker(Now).WithSource(predecessor).WithTarget(successor).Generate());

    public void AddReference(WorkItem workItem, Guid objectId) =>
        Context.AddWorkItemReference(WorkItemReference.Create(workItem.Id, objectId, SystemContext.PlanningPlanningIntervalObjective));
}
