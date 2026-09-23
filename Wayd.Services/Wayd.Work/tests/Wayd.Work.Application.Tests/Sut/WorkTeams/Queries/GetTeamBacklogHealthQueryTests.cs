using FluentAssertions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Models;
using Wayd.Tests.Shared;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkTeams.Queries;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Models.BacklogHealth;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkTeams.Queries;

public sealed class GetTeamBacklogHealthQueryTests : IDisposable
{
    private static readonly Instant _now = Instant.FromUtc(2026, 9, 22, 12, 0);

    private readonly FakeWorkDbContext _context = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(_now));
    private readonly Workspace _workspace = new WorkspaceFaker().AsExternal().WithKey(new WorkspaceKey("TEST")).Generate();
    private readonly WorkType _story = new WorkTypeFaker().AsStory().Generate();
    private readonly WorkType _feature = new WorkTypeFaker().AsFeature().Generate();
    private readonly Guid _team;
    private int _nextNumber = 1;

    public GetTeamBacklogHealthQueryTests()
    {
        MapsterTestConfiguration.Ensure();
        _team = NewTeam();
        MembersAre(5);
    }

    public void Dispose() => _context.Dispose();

    private GetTeamBacklogHealthQueryHandler Handler() => new(_context, _dispatcher.Object, _dateTimeProvider);

    private static GetTeamBacklogHealthQuery Query(Guid team, BacklogHealthThresholds? thresholds = null) =>
        new(team, thresholds ?? BacklogHealthThresholds.Default);

    private Guid NewTeam()
    {
        var team = new WorkTeamFaker(TeamType.Team).Generate();
        _context.AddWorkTeam(team);
        return team.Id;
    }

    private void MembersAre(int? count) =>
        _dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamMemberCountQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(count);

    private WorkItem AddItem(
        Guid? teamId,
        WorkStatusCategory statusCategory = WorkStatusCategory.Proposed,
        double stackRank = 1,
        WorkType? type = null,
        Instant? created = null,
        Instant? lastModified = null,
        Instant? activated = null,
        Instant? done = null,
        Guid? projectId = null)
    {
        var item = new WorkItemFaker()
            .WithWorkspace(_workspace)
            .WithKey(new WorkItemKey(_workspace.Key, _nextNumber++))
            .WithType(type ?? _story)
            .WithTeamId(teamId)
            .WithStatusCategory(statusCategory)
            .WithStackRank(stackRank)
            .WithCreated(created ?? Instant.FromUtc(2026, 1, 1, 0, 0))
            .WithLastModified(lastModified ?? _now - Duration.FromDays(1))
            .WithActivatedTimestamp(activated)
            .WithDoneTimestamp(done)
            .WithProjectId(projectId)
            .Generate();

        _context.AddWorkItem(item);
        return item;
    }

    private void AddCompletions(Guid teamId, int count, Guid? projectId = null)
    {
        for (var day = 1; day <= count; day++)
        {
            var done = _now - Duration.FromDays(day);
            AddItem(teamId, WorkStatusCategory.Done, activated: done - Duration.FromDays(3), done: done, projectId: projectId);
        }
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
    public async Task Handle_ReturnsTheTeamsOpenBacklogItemsInRankOrder()
    {
        // Arrange
        var third = AddItem(_team, stackRank: 5);
        var first = AddItem(_team, stackRank: 1, created: Instant.FromUtc(2026, 2, 1, 0, 0));
        var second = AddItem(_team, WorkStatusCategory.Active, stackRank: 1, created: Instant.FromUtc(2026, 3, 1, 0, 0));
        AddItem(_team, WorkStatusCategory.Done, done: _now - Duration.FromDays(400));
        AddItem(_team, type: _feature);
        AddItem(NewTeam());

        // Act
        var result = await Handler().Handle(Query(_team), TestContext.Current.CancellationToken);

        // Assert
        var health = result.Value!;
        health.Team.Id.Should().Be(_team);
        health.WorkItems.Select(w => (w.Id, w.Rank)).Should().Equal((first.Id, 1), (second.Id, 2), (third.Id, 3));
        health.TotalWorkItems.Should().Be(3);
        health.ProposedWorkItems.Should().Be(2);
        health.ActiveWorkItems.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ReturnsOneResultPerCheck()
    {
        // Arrange
        AddItem(_team);

        // Act
        var result = await Handler().Handle(Query(_team), TestContext.Current.CancellationToken);

        // Assert
        result.Value!.Checks.Select(c => c.Check.Id).Should().Equal(Enum.GetValues<BacklogHealthCheck>().Select(c => (int)c));
    }

    [Fact]
    public async Task Handle_FlagsItemsAgainstTheThresholdsGiven()
    {
        // Arrange
        var thresholds = BacklogHealthThresholds.Default with { StaleDays = 10 };
        var stale = AddItem(_team, lastModified: _now - Duration.FromDays(11));
        var fresh = AddItem(_team, stackRank: 2, lastModified: _now - Duration.FromDays(9));

        // Act
        var result = await Handler().Handle(Query(_team, thresholds), TestContext.Current.CancellationToken);

        // Assert
        var health = result.Value!;
        health.Thresholds.Should().Be(thresholds);
        health.WorkItems.Single(w => w.Id == stale.Id).Flags.Select(f => f.Id).Should().Contain((int)BacklogHealthCheck.Stale);
        health.WorkItems.Single(w => w.Id == fresh.Id).Flags.Select(f => f.Id).Should().NotContain((int)BacklogHealthCheck.Stale);

        var check = health.Checks.Single(c => c.Check.Id == (int)BacklogHealthCheck.Stale);
        check.Grade!.Id.Should().Be((int)HealthStatus.Unhealthy);
        check.Flagged.Should().Be(1);
        check.InScope.Should().Be(2);
    }

    [Fact]
    public async Task Handle_MeasuresHistoryOverTheLookbackWindow()
    {
        // Arrange
        AddCompletions(_team, 12);
        AddItem(_team, WorkStatusCategory.Done, done: _now - Duration.FromDays(100));
        AddItem(_team, WorkStatusCategory.Removed, done: _now - Duration.FromDays(2));
        AddItem(_team, created: _now - Duration.FromDays(5));
        AddItem(_team, created: _now - Duration.FromDays(95));

        // Act
        var result = await Handler().Handle(Query(_team), TestContext.Current.CancellationToken);

        // Assert
        var health = result.Value!;
        health.LookbackDays.Should().Be(90);
        health.To.Should().Be(new LocalDate(2026, 9, 21));
        health.From.Should().Be(new LocalDate(2026, 6, 24));
        health.ItemsCompleted.Should().Be(12);
        health.ItemsCreated.Should().Be(1);
        health.AgingWipDays.Should().BeApproximately(3, 0.0001);
    }

    [Fact]
    public async Task Handle_GradesWipLoadAgainstTheTeamsMembers()
    {
        // Arrange
        MembersAre(2);
        for (var rank = 1; rank <= 5; rank++)
            AddItem(_team, WorkStatusCategory.Active, stackRank: rank, activated: _now - Duration.FromDays(1));

        // Act
        var result = await Handler().Handle(Query(_team), TestContext.Current.CancellationToken);

        // Assert
        var health = result.Value!;
        health.MemberCount.Should().Be(2);
        var wipLoad = health.Checks.Single(c => c.Check.Id == (int)BacklogHealthCheck.WipLoad);
        wipLoad.Value.Should().BeApproximately(2.5, 0.0001);
        wipLoad.Grade!.Id.Should().Be((int)HealthStatus.Unhealthy);
        _dispatcher.Verify(d => d.Send(It.Is<GetTeamMemberCountQuery>(q => q.TeamId == _team), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TeamUnknownToTheOrganization_DoesNotGradeWipLoad()
    {
        // Arrange
        MembersAre(null);
        AddItem(_team, WorkStatusCategory.Active);

        // Act
        var result = await Handler().Handle(Query(_team), TestContext.Current.CancellationToken);

        // Assert
        var health = result.Value!;
        health.MemberCount.Should().BeNull();
        health.Checks.Single(c => c.Check.Id == (int)BacklogHealthCheck.WipLoad)
            .Outcome.Id.Should().Be((int)BacklogHealthOutcome.NotApplicable);
    }

    [Fact]
    public async Task Handle_FlagsItemsRankedAboveTheirPredecessor()
    {
        // Arrange
        var successor = AddItem(_team, stackRank: 1);
        var predecessor = AddItem(_team, stackRank: 2);
        _context.AddWorkItemDependency(new WorkItemDependencyFaker(_now).WithSource(predecessor).WithTarget(successor).Generate());

        // Act
        var result = await Handler().Handle(Query(_team), TestContext.Current.CancellationToken);

        // Assert
        var health = result.Value!;
        health.WorkItems.Single(w => w.Id == successor.Id).Flags.Select(f => f.Id).Should().Contain((int)BacklogHealthCheck.RankInversion);
        health.Checks.Single(c => c.Check.Id == (int)BacklogHealthCheck.RankInversion)
            .Grade!.Id.Should().Be((int)HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task Handle_TeamNotUsingProjects_DoesNotCheckForThem()
    {
        // Arrange
        AddItem(_team);

        // Act
        var result = await Handler().Handle(Query(_team), TestContext.Current.CancellationToken);

        // Assert
        result.Value!.Checks.Single(c => c.Check.Id == (int)BacklogHealthCheck.NoProject)
            .Outcome.Id.Should().Be((int)BacklogHealthOutcome.NotApplicable);
    }

    [Fact]
    public async Task Handle_TeamThatCompletedProjectWork_ChecksForProjects()
    {
        // Arrange
        AddCompletions(_team, 1, projectId: Guid.NewGuid());
        var noProject = AddItem(_team);

        // Act
        var result = await Handler().Handle(Query(_team), TestContext.Current.CancellationToken);

        // Assert
        var health = result.Value!;
        health.Checks.Single(c => c.Check.Id == (int)BacklogHealthCheck.NoProject)
            .Outcome.Id.Should().Be((int)BacklogHealthOutcome.Assessed);
        health.WorkItems.Single(w => w.Id == noProject.Id).Flags.Select(f => f.Id).Should().Contain((int)BacklogHealthCheck.NoProject);
    }
}
