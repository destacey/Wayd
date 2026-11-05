using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Models.Organizations;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Common.Models;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkItems.Queries;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkItems.Queries;

public sealed class GetTeamBacklogQueryHandlerTests : IDisposable
{
    private static readonly Instant Created = Instant.FromUtc(2026, 9, 1, 0, 0);

    private readonly FakeWorkDbContext _dbContext = new();
    private readonly GetTeamBacklogQueryHandler _handler;
    private readonly Guid _teamId = Guid.NewGuid();
    private readonly Workspace _workspace = new WorkspaceFaker().AsExternal().WithKey(new WorkspaceKey("TEST")).Generate();
    private readonly WorkType _storyType = new WorkTypeFaker().AsStory().Generate();
    private readonly WorkStatus _status = new WorkStatusFaker().WithName("Active").Generate();

    public GetTeamBacklogQueryHandlerTests()
    {
        _handler = new GetTeamBacklogQueryHandler(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_OrdersByStackRankThenCreatedThenId_AndRanksInThatOrder()
    {
        // Arrange — two items tie on rank and created, so only the id separates them
        var top = CreateWorkItem(1, stackRank: 1, Created);
        var olderAtFive = CreateWorkItem(2, stackRank: 5, Created.Minus(Duration.FromDays(1)));
        var tiedA = CreateWorkItem(3, stackRank: 5, Created);
        var tiedB = CreateWorkItem(4, stackRank: 5, Created);
        var tied = new[] { tiedA, tiedB }.OrderBy(w => w.Id).ToArray();
        _dbContext.AddWorkItems([tied[1], olderAtFive, tied[0], top]);

        // Act
        var result = await _handler.Handle(new GetTeamBacklogQuery(new TeamIdOrCode(_teamId)), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Select(w => w.Id).Should().Equal(top.Id, olderAtFive.Id, tied[0].Id, tied[1].Id);
        result.Value.Select(w => w.Rank).Should().Equal(1, 2, 3, 4);
    }

    private WorkItem CreateWorkItem(int number, double stackRank, Instant created)
    {
        var faker = new WorkItemFaker(_workspace.Id)
            .WithExternalId(number)
            .WithKey(new WorkItemKey(_workspace.Key, number))
            .WithType(_storyType)
            .WithStatus(_status)
            .WithStatusCategory(StatusCategory.Active)
            .WithTeamId(_teamId)
            .WithStackRank(stackRank)
            .WithCreated(created)
            .WithActivatedTimestamp(created.Plus(Duration.FromDays(1)))
            .WithDoneTimestamp(null);

        faker.RuleFor(x => x.Workspace, _workspace);

        return faker.Generate();
    }
}
