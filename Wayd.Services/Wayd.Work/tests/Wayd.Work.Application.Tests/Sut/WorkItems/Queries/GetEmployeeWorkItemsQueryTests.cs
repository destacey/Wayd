using NodaTime;
using Wayd.Common.Domain.StatusWorkflows.Enums;
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

        var expected = CreateWorkItem(employeeId, requirementType, StatusCategory.Done, 1);
        var assignedToSomeoneElse = CreateWorkItem(otherEmployeeId, requirementType, StatusCategory.Done, 2);
        var nonRequirementType = CreateWorkItem(employeeId, otherType, StatusCategory.Done, 3);

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

        var expected = CreateWorkItem(employeeId, requirementType, StatusCategory.Done, 1, inRangeDone);
        var activeItem = CreateWorkItem(employeeId, requirementType, StatusCategory.Active, 2, null);
        var beforeRange = CreateWorkItem(employeeId, requirementType, StatusCategory.Done, 3, Instant.FromUtc(2025, 12, 31, 0, 0));
        var afterRange = CreateWorkItem(employeeId, requirementType, StatusCategory.Done, 4, Instant.FromUtc(2026, 2, 1, 0, 0));

        context.AddWorkItems([expected, activeItem, beforeRange, afterRange]);
        var handler = new GetEmployeeWorkItemsQueryHandler(context);

        var result = await handler.Handle(
            new GetEmployeeWorkItemsQuery(
                employeeId,
                [StatusCategory.Done],
                Instant.FromUtc(2026, 1, 1, 0, 0),
                Instant.FromUtc(2026, 1, 31, 0, 0)),
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(expected.Id, result[0].Id);
    }

    [Fact]
    public async Task Handle_OrdersByStackRankThenId()
    {
        // Arrange — two items share a stack rank, so only the id separates them
        using var context = new FakeWorkDbContext();
        var employeeId = Guid.NewGuid();
        var requirementType = new WorkTypeFaker().AsStory().Generate();

        var lowest = CreateWorkItem(employeeId, requirementType, StatusCategory.Active, 1, stackRank: 1);
        var tiedA = CreateWorkItem(employeeId, requirementType, StatusCategory.Active, 2, stackRank: 5);
        var highest = CreateWorkItem(employeeId, requirementType, StatusCategory.Active, 3, stackRank: 9);
        var tiedB = CreateWorkItem(employeeId, requirementType, StatusCategory.Active, 4, stackRank: 5);
        var tied = new[] { tiedA, tiedB }.OrderBy(w => w.Id).ToArray();

        context.AddWorkItems([highest, tied[1], lowest, tied[0]]);
        var handler = new GetEmployeeWorkItemsQueryHandler(context);

        // Act
        var result = await handler.Handle(new GetEmployeeWorkItemsQuery(employeeId), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([lowest.Id, tied[0].Id, tied[1].Id, highest.Id], result.Select(w => w.Id));
    }

    private static WorkItem CreateWorkItem(
        Guid employeeId,
        WorkType workType,
        StatusCategory statusCategory,
        int externalId,
        Instant? doneTimestamp = null,
        double? stackRank = null)
    {
        var workspace = new WorkspaceFaker()
            .AsExternal()
            .WithKey(new WorkspaceKey("TEST"))
            .Generate();
        var status = new WorkStatusFaker()
            .WithName(statusCategory.ToString())
            .Generate();
        var created = Instant.FromUtc(2025, 12, 1, 0, 0);
        Instant? activated = statusCategory is StatusCategory.Proposed
            ? null
            : created.Plus(Duration.FromDays(1));
        var done = doneTimestamp ?? (statusCategory is StatusCategory.Done or StatusCategory.Removed
            ? created.Plus(Duration.FromDays(5))
            : null);
        var faker = new WorkItemFaker(workspace.Id)
            .WithExternalId(externalId)
            .WithKey(new WorkItemKey(workspace.Key, externalId))
            .WithType(workType).WithStatus(status).WithStatusCategory(statusCategory).WithAssignedToId(employeeId).WithCreated(created).WithActivatedTimestamp(activated).WithDoneTimestamp(done);

        if (stackRank is { } rank)
            faker = faker.WithStackRank(rank);

        faker.RuleFor(x => x.Workspace, workspace);

        return faker.Generate();
    }
}