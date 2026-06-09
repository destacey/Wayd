using NodaTime;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Models;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkItems.Queries;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkItems.Queries;

public sealed class GetEmployeeWorkItemsQueryTests
{
    [Fact]
    public async Task Handle_ReturnsOnlyRequirementWorkItemsAssignedToEmployee()
    {
        using var context = new FakeWorkDbContext();
        var employeeId = Guid.NewGuid();
        var otherEmployeeId = Guid.NewGuid();
        var requirementType = new WorkTypeFaker().AsStory().Generate();
        var otherType = new WorkTypeFaker().AsOther().Generate();

        var expected = CreateWorkItem(employeeId, requirementType, WorkStatusCategory.Done, 1);
        var assignedToSomeoneElse = CreateWorkItem(otherEmployeeId, requirementType, WorkStatusCategory.Done, 2);
        var nonRequirementType = CreateWorkItem(employeeId, otherType, WorkStatusCategory.Done, 3);

        context.AddWorkItems([expected, assignedToSomeoneElse, nonRequirementType]);
        var handler = new GetEmployeeWorkItemsQueryHandler(context);

        var result = await handler.Handle(new GetEmployeeWorkItemsQuery(employeeId), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(expected.Id, result[0].Id);
    }

    [Fact]
    public async Task Handle_AppliesStatusCategoryAndDoneDateFilters()
    {
        using var context = new FakeWorkDbContext();
        var employeeId = Guid.NewGuid();
        var requirementType = new WorkTypeFaker().AsStory().Generate();
        var inRangeDone = Instant.FromUtc(2026, 1, 15, 0, 0);

        var expected = CreateWorkItem(employeeId, requirementType, WorkStatusCategory.Done, 1, inRangeDone);
        var activeItem = CreateWorkItem(employeeId, requirementType, WorkStatusCategory.Active, 2, null);
        var beforeRange = CreateWorkItem(employeeId, requirementType, WorkStatusCategory.Done, 3, Instant.FromUtc(2025, 12, 31, 0, 0));
        var afterRange = CreateWorkItem(employeeId, requirementType, WorkStatusCategory.Done, 4, Instant.FromUtc(2026, 2, 1, 0, 0));

        context.AddWorkItems([expected, activeItem, beforeRange, afterRange]);
        var handler = new GetEmployeeWorkItemsQueryHandler(context);

        var result = await handler.Handle(
            new GetEmployeeWorkItemsQuery(
                employeeId,
                [WorkStatusCategory.Done],
                Instant.FromUtc(2026, 1, 1, 0, 0),
                Instant.FromUtc(2026, 1, 31, 0, 0)),
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(expected.Id, result[0].Id);
    }

    private static WorkItem CreateWorkItem(
        Guid employeeId,
        WorkType workType,
        WorkStatusCategory statusCategory,
        int externalId,
        Instant? doneTimestamp = null)
    {
        var workspace = new WorkspaceFaker()
            .AsExternal()
            .WithKey(new WorkspaceKey("TEST"))
            .Generate();
        var status = new WorkStatusFaker()
            .WithName(statusCategory.ToString())
            .Generate();
        var created = Instant.FromUtc(2025, 12, 1, 0, 0);
        Instant? activated = statusCategory is WorkStatusCategory.Proposed
            ? null
            : created.Plus(Duration.FromDays(1));
        var done = doneTimestamp ?? (statusCategory is WorkStatusCategory.Done or WorkStatusCategory.Removed
            ? created.Plus(Duration.FromDays(5))
            : null);
        var faker = new WorkItemFaker(workspace.Id)
            .WithExternalId(externalId)
            .WithType(workType).WithStatus(status).WithStatusCategory(statusCategory).WithAssignedToId(employeeId).WithCreated(created).WithActivatedTimestamp(activated).WithDoneTimestamp(done);

        faker.RuleFor(x => x.Workspace, workspace);

        return faker.Generate();
    }
}