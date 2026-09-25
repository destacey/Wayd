using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.Requests.ProjectPortfolioManagement;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Models;
using Wayd.Tests.Shared;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkTeams.Allocation;
using Wayd.Work.Application.WorkTeams.Queries;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkTeams.Queries;

public sealed class GetTeamAllocationQueryTests : IDisposable
{
    private static readonly LocalDate From = new(2026, 7, 1);
    private static readonly LocalDate To = new(2026, 9, 30);
    private static readonly Instant InWindow = Instant.FromUtc(2026, 8, 3, 15, 0);

    private static readonly AllocationOptions ItemsByProject = new(
        AllocationDimension.Project, AllocationMeasure.Count, UnestimatedHandling.Exclude, ThemeCounting.SplitEvenly);

    private readonly FakeWorkDbContext _context = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly Workspace _workspace = new WorkspaceFaker().AsExternal().WithKey(new WorkspaceKey("TEST")).Generate();
    private readonly WorkType _story = new WorkTypeFaker().AsStory().Generate();
    private readonly WorkType _feature = new WorkTypeFaker().AsFeature().Generate();
    private readonly WorkTeam _team;
    private readonly ProjectClassification _project;
    private int _nextNumber = 1;

    public GetTeamAllocationQueryTests()
    {
        MapsterTestConfiguration.Ensure();
        _team = new WorkTeamFaker(TeamType.Team).Generate();
        _context.AddWorkTeam(_team);

        var portfolio = new PpmRecordReference(Guid.NewGuid(), 1, "Customer Experience");
        _project = new ProjectClassification(Guid.NewGuid(), "ONE", "Project One", portfolio, null, [], false);

        _dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamStructureQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetTeamStructureQuery q, CancellationToken _) => new TeamStructure(
                q.TeamId,
                [new TeamStructureTeam(_team.Id, _team.Key, _team.Code.Value, _team.Name, TeamType.Team)],
                [],
                []));
        _dispatcher
            .Setup(d => d.Send(It.IsAny<GetProjectClassificationsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetProjectClassificationsQuery q, CancellationToken _) =>
                q.ProjectIds.Contains(_project.ProjectId) ? [_project] : []);
    }

    public void Dispose() => _context.Dispose();

    private GetTeamAllocationQueryHandler Handler() => new(_context, _dispatcher.Object);

    private GetTeamAllocationQuery Query(Guid? teamId = null) => new(teamId ?? _team.Id, From, To, ItemsByProject);

    private WorkItem AddItem(
        WorkStatusCategory statusCategory = WorkStatusCategory.Done,
        Instant? done = null,
        WorkType? type = null,
        Guid? projectId = null,
        Guid? parentProjectId = null)
    {
        var item = new WorkItemFaker()
            .WithWorkspace(_workspace)
            .WithKey(new WorkItemKey(_workspace.Key, _nextNumber++))
            .WithType(type ?? _story)
            .WithTeamId(_team.Id)
            .WithStatusCategory(statusCategory)
            .WithDoneTimestamp(done ?? InWindow)
            .WithProjectId(projectId)
            .WithParentProjectId(parentProjectId)
            .Generate();

        _context.AddWorkItem(item);
        return item;
    }

    [Fact]
    public async Task Handle_UnknownTeam_ReturnsNull()
    {
        // Act
        var result = await Handler().Handle(Query(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Handle_CountsOnlyRequirementItemsDoneInsideTheWindow()
    {
        // Arrange
        AddItem();
        AddItem(done: Instant.FromUtc(2026, 9, 30, 23, 59));
        AddItem(done: Instant.FromUtc(2026, 10, 1, 0, 0));
        AddItem(done: Instant.FromUtc(2026, 6, 30, 23, 59));
        AddItem(WorkStatusCategory.Removed);
        AddItem(WorkStatusCategory.Active);
        AddItem(type: _feature);

        // Act
        var result = await Handler().Handle(Query(), TestContext.Current.CancellationToken);

        // Assert
        result.Value!.Summary.ItemsCompleted.Should().Be(2);
    }

    [Fact]
    public async Task Handle_UsesTheProjectInheritedFromTheParent()
    {
        // Arrange
        AddItem(parentProjectId: _project.ProjectId);
        AddItem(projectId: _project.ProjectId);
        AddItem();

        // Act
        var result = await Handler().Handle(Query(), TestContext.Current.CancellationToken);

        // Assert
        result.Value!.Groups.Select(g => (g.Name, g.Items)).Should().Equal(("Project One", 2.0), ("No project", 1.0));
    }

    [Fact]
    public async Task Handle_NoLinkedProjects_DoesNotAskForClassifications()
    {
        // Arrange
        AddItem();

        // Act
        await Handler().Handle(Query(), TestContext.Current.CancellationToken);

        // Assert
        _dispatcher.Verify(d => d.Send(It.IsAny<GetProjectClassificationsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
