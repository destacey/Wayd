using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using NodaTime;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Identity.Roles;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.IntakeDesks.Dtos;
using Wayd.DemandManagement.Application.IntakeDesks.Queries;
using Wayd.Infrastructure.Auth.Permissions;
using Wayd.Organization.Application.EmployeeCapacities.Dtos;
using Wayd.Organization.Application.EmployeeCapacities.Queries;
using Wayd.Organization.Application.Models;
using Wayd.Organization.Application.Teams.Dtos;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Planning.Application.PlanningIntervals.Dtos;
using Wayd.Planning.Application.PlanningIntervals.Queries;
using Wayd.Planning.Application.Risks.Queries;
using Wayd.ProductManagement.Application.Products.Queries;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Queries;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.Web.Api.Controllers;
using Wayd.Web.Api.Mcp;
using Wayd.Web.Api.Models.Me;
using Wayd.Work.Application.Iterations.Dtos;
using Wayd.Work.Application.Iterations.Queries;
using Wayd.Work.Application.WorkItemDependencies.Dtos;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Queries;
using Wayd.Work.Application.WorkTeams.Dtos;
using Wayd.Work.Application.WorkTeams.Queries;

namespace Wayd.Web.Api.Tests.Sut.Controllers;

public sealed class MeControllerTests
{
    private const string UserId = "a1b2c3d4-0000-0000-0000-000000000001";

    private readonly AutoMocker _mocker = new();

    private MeController CreateController()
    {
        var controller = _mocker.CreateInstance<MeController>();
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private void LinkedTo(Guid? employeeId) =>
        _mocker
            .GetMock<ICurrentPrincipal>()
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(employeeId);

    private void VerifyNothingDispatched() =>
        _mocker.GetMock<IDispatcher>().VerifyNoOtherCalls();

    [Fact]
    public async Task GetProfile_ReturnsTheAccountAndItsLinkedEmployee()
    {
        // Arrange
        var employee = new NavigationDto { Id = Guid.NewGuid(), Key = 42, Name = "Avery Quinn" };
        _mocker.GetMock<ICurrentUser>().Setup(u => u.GetUserId()).Returns(UserId);
        _mocker
            .GetMock<IUserService>()
            .Setup(s => s.GetAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDetailsDto
            {
                Id = UserId,
                UserName = "avery.quinn@acme.example",
                FirstName = "Avery",
                LastName = "Quinn",
                Email = "avery.quinn@acme.example",
                LoginProvider = "Wayd",
                Employee = employee,
                Roles = [new RoleListDto { Id = "r2", Name = "Team Member" }, new RoleListDto { Id = "r1", Name = "Basic" }],
            });

        var controller = CreateController();

        // Act
        var result = await controller.GetProfile(TestContext.Current.CancellationToken);

        // Assert
        var me = result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<MeResponse>().Subject;
        me.UserId.Should().Be(UserId);
        me.Employee.Should().Be(employee);
        me.Roles.Should().Equal("Basic", "Team Member");
    }

    [Fact]
    public async Task GetProfile_WhenTheAccountHasNoEmployee_ReturnsANullEmployee()
    {
        // Arrange
        _mocker.GetMock<ICurrentUser>().Setup(u => u.GetUserId()).Returns(UserId);
        _mocker
            .GetMock<IUserService>()
            .Setup(s => s.GetAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserDetailsDto { Id = UserId, LoginProvider = "Wayd" });

        var controller = CreateController();

        // Act
        var result = await controller.GetProfile(TestContext.Current.CancellationToken);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<MeResponse>()
            .Which.Employee.Should().BeNull();
    }

    [Fact]
    public async Task GetTeamMemberships_AsksForTheCallersEmployee()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        LinkedTo(employeeId);
        IReadOnlyList<TeamMemberDto> memberships = [];
        _mocker
            .GetMock<IDispatcher>()
            .Setup(d => d.Send(It.IsAny<GetEmployeeTeamMembershipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(memberships);

        var controller = CreateController();

        // Act
        var result = await controller.GetTeamMemberships(TestContext.Current.CancellationToken);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(memberships);
        _mocker
            .GetMock<IDispatcher>()
            .Verify(d => d.Send(It.Is<GetEmployeeTeamMembershipsQuery>(q => q.EmployeeId == employeeId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTeamMemberships_WhenNotLinkedToAnEmployee_ReturnsNotFoundSayingWhy()
    {
        // Arrange
        LinkedTo(null);
        var controller = CreateController();

        // Act
        var result = await controller.GetTeamMemberships(TestContext.Current.CancellationToken);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>()
            .Which.Value.Should().BeOfType<ProblemDetails>()
            .Which.Detail.Should().Contain("not linked to an employee");
        VerifyNothingDispatched();
    }

    [Fact]
    public async Task GetAllocations_AsksForTheCallersEmployee()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        LinkedTo(employeeId);
        IReadOnlyList<EmployeeAllocationDto> allocations = [];
        _mocker
            .GetMock<IDispatcher>()
            .Setup(d => d.Send(It.IsAny<GetEmployeeAllocationsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(allocations);

        var controller = CreateController();

        // Act
        var result = await controller.GetAllocations(TestContext.Current.CancellationToken);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(allocations);
        _mocker
            .GetMock<IDispatcher>()
            .Verify(d => d.Send(It.Is<GetEmployeeAllocationsQuery>(q => q.EmployeeId == employeeId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllocations_WhenNotLinkedToAnEmployee_ReturnsNotFound()
    {
        // Arrange
        LinkedTo(null);
        var controller = CreateController();

        // Act
        var result = await controller.GetAllocations(TestContext.Current.CancellationToken);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
        VerifyNothingDispatched();
    }

    [Fact]
    public async Task GetWorkItems_AsksForTheCallersEmployeeWithTheFilters()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        LinkedTo(employeeId);
        StatusCategory[] open = [StatusCategory.Proposed, StatusCategory.Active];
        _mocker
            .GetMock<IDispatcher>()
            .Setup(d => d.Send(It.IsAny<GetEmployeeWorkItemsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemListDto>());

        var controller = CreateController();

        // Act
        var result = await controller.GetWorkItems(open, null, null, TestContext.Current.CancellationToken);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        _mocker
            .GetMock<IDispatcher>()
            .Verify(d => d.Send(
                It.Is<GetEmployeeWorkItemsQuery>(q => q.EmployeeId == employeeId && q.StatusCategories == open && q.DoneFrom == null && q.DoneTo == null),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetWorkItems_WhenNotLinkedToAnEmployee_ReturnsNotFound()
    {
        // Arrange
        LinkedTo(null);
        var controller = CreateController();

        // Act
        var result = await controller.GetWorkItems(null, null, null, TestContext.Current.CancellationToken);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
        VerifyNothingDispatched();
    }

    private void FlagsOn(params string[] flags) =>
        _mocker
            .GetMock<IFeatureFlags>()
            .Setup(f => f.IsEnabled(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string flag, CancellationToken _) => flags.Contains(flag));

    private void TeamsAre(params Guid[] teamIds) =>
        _mocker
            .GetMock<IDispatcher>()
            .Setup(d => d.Send(It.IsAny<GetEmployeeTeamInvolvementQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmployeeTeamInvolvementDto { TeamCount = teamIds.Length, TeamOfTeamsCount = 0, TeamIds = teamIds });

    [Fact]
    public async Task GetHomeContext_PutsTogetherEachModulesCounts()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var teamIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        LinkedTo(employeeId);
        FlagsOn(FeatureFlags.Names.DemandManagement, FeatureFlags.Names.ProductManagement);
        TeamsAre(teamIds);
        var dispatcher = _mocker.GetMock<IDispatcher>();
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetMyProjectsSummaryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MyProjectsSummaryDto { TotalCount = 3, ManagerCount = 2 });
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetEmployeeOpenRiskCountQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetEmployeeIntakeInvolvementQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmployeeIntakeInvolvementDto { DeskCount = 1, AgentDeskCount = 1, OpenRequestsRaisedCount = 5 });
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsProductCountQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(6);

        var controller = CreateController();

        // Act
        var result = await controller.GetHomeContext(TestContext.Current.CancellationToken);

        // Assert
        var context = result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<HomeContextResponse>().Subject;
        context.HasLinkedEmployee.Should().BeTrue();
        context.Teams.TeamCount.Should().Be(2);
        context.Projects.ManagerCount.Should().Be(2);
        context.AssignedRiskCount.Should().Be(4);
        context.Intake.OpenRequestsRaisedCount.Should().Be(5);
        context.TeamProductCount.Should().Be(6);
        dispatcher.Verify(d => d.Send(It.Is<GetEmployeeTeamInvolvementQuery>(q => q.EmployeeId == employeeId), It.IsAny<CancellationToken>()), Times.Once);
        dispatcher.Verify(d => d.Send(It.Is<GetEmployeeOpenRiskCountQuery>(q => q.EmployeeId == employeeId), It.IsAny<CancellationToken>()), Times.Once);
        dispatcher.Verify(d => d.Send(It.Is<GetEmployeeIntakeInvolvementQuery>(q => q.EmployeeId == employeeId), It.IsAny<CancellationToken>()), Times.Once);
        dispatcher.Verify(d => d.Send(It.Is<GetTeamsProductCountQuery>(q => q.TeamIds.SequenceEqual(teamIds)), It.IsAny<CancellationToken>()), Times.Once);
        dispatcher.Verify(d => d.Send(
            It.Is<GetMyProjectsSummaryQuery>(q => q.StatusFilter!.SequenceEqual(new[] { ProjectStatus.Approved, ProjectStatus.Active })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetHomeContext_WithTheirModulesSwitchedOff_CountsNoIntakeOrProducts()
    {
        // Arrange
        LinkedTo(Guid.NewGuid());
        FlagsOn();
        TeamsAre(Guid.NewGuid());
        var controller = CreateController();

        // Act
        var result = await controller.GetHomeContext(TestContext.Current.CancellationToken);

        // Assert
        var context = result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<HomeContextResponse>().Subject;
        context.Intake.DeskCount.Should().Be(0);
        context.TeamProductCount.Should().Be(0);
        var dispatcher = _mocker.GetMock<IDispatcher>();
        dispatcher.Verify(d => d.Send(It.IsAny<GetEmployeeIntakeInvolvementQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        dispatcher.Verify(d => d.Send(It.IsAny<GetTeamsProductCountQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetHomeContext_WhenNotLinkedToAnEmployee_ReturnsEmptyInvolvement()
    {
        // Arrange
        LinkedTo(null);
        var controller = CreateController();

        // Act
        var result = await controller.GetHomeContext(TestContext.Current.CancellationToken);

        // Assert
        var context = result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<HomeContextResponse>().Subject;
        context.HasLinkedEmployee.Should().BeFalse();
        context.Teams.TeamCount.Should().Be(0);
        context.Projects.TotalCount.Should().Be(0);
        VerifyNothingDispatched();
    }

    // 06:00 on 9 November in UTC is 19:00 the same day in Auckland (UTC+13).
    private static readonly Instant SummaryNow = Instant.FromUtc(2026, 11, 9, 6, 0);

    private const string Auckland = "Pacific/Auckland";

    private static TeamNavigationDto SummaryTeam(string name) =>
        new() { Id = Guid.NewGuid(), Key = Random.Shared.Next(1, 10_000), Name = name, Code = name.ToUpperInvariant(), Type = "Team" };

    private static TeamSchedulePeriodDto Model(LocalDate start, LocalDate? end, Methodology methodology, string timeZone = "Etc/UTC", ReportingCadence? cadence = null) =>
        new(start, end, timeZone, 0, SizingMethod.Count) { Methodology = methodology, Cadence = cadence };

    private Mock<IDispatcher> SummaryOf(
        Instant now,
        IReadOnlyList<TeamNavigationDto> teams,
        Dictionary<Guid, IReadOnlyList<TeamSchedulePeriodDto>> schedules,
        params string[] permissions)
    {
        LinkedTo(Guid.NewGuid());
        _mocker.GetMock<IDateTimeProvider>().Setup(p => p.Now).Returns(now);
        _mocker.GetMock<IDateTimeProvider>().Setup(p => p.Today).Returns(now.InUtc().Date);
        _mocker
            .GetMock<ICurrentPrincipal>()
            .Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string permission, CancellationToken _) => permissions.Contains(permission));

        var dispatcher = _mocker.GetMock<IDispatcher>();
        dispatcher
            .Setup(d => d.Send(It.Is<GetEmployeeActiveTeamsQuery>(q => q.Type == TeamType.Team), It.IsAny<CancellationToken>()))
            .ReturnsAsync(teams);
        dispatcher
            .Setup(d => d.Send(It.Is<GetEmployeeActiveTeamsQuery>(q => q.Type == TeamType.TeamOfTeams), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsParentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, TeamNavigationDto>());
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsSprintSignalQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, TeamSprintSignalDto>());
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsDependenciesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetCurrentPlanningIntervalForTeamsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CurrentPlanningIntervalDto?)null);
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsScheduleHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(schedules.ToDictionary(e => e.Key, e => e.Value));
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsActiveSprintQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, NavigationDto>());
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetKanbanTeamsWindowMetricsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsOldestInProgressWorkItemQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, InProgressWorkItemDto>());
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsDependencyHealthQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TeamsDependencyHealthDto());
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsOpenRiskCountQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int>());
        return dispatcher;
    }

    private static readonly string[] _allSummaryPermissions =
    [
        ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Iterations),
        ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.WorkItems),
        ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Risks),
    ];

    private async Task<MyTeamsSummaryResponse> GetSummary(Guid[]? teamIds = null, Guid[]? teamOfTeamsIds = null)
    {
        var result = await CreateController().GetTeamsSummary(teamIds, teamOfTeamsIds, TestContext.Current.CancellationToken);
        return result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<MyTeamsSummaryResponse>().Subject;
    }

    [Fact]
    public async Task GetTeamsSummary_WhenTheAccountHasNoEmployee_ReturnsNotFound()
    {
        // Arrange
        LinkedTo(null);
        var controller = CreateController();

        // Act
        var result = await controller.GetTeamsSummary(null, null, TestContext.Current.CancellationToken);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
        VerifyNothingDispatched();
    }

    [Fact]
    public async Task GetTeamsSummary_ScrumTeams_ReturnTheirActiveSprintOrNone()
    {
        // Arrange
        var running = SummaryTeam("Running");
        var between = SummaryTeam("Between");
        var sprint = new NavigationDto { Id = Guid.NewGuid(), Key = 12, Name = "Sprint 12" };
        var dispatcher = SummaryOf(SummaryNow, [running, between], new()
        {
            [running.Id] = [Model(new LocalDate(2026, 1, 1), null, Methodology.Scrum)],
            [between.Id] = [Model(new LocalDate(2026, 1, 1), null, Methodology.Scrum)],
        }, _allSummaryPermissions);
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsActiveSprintQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, NavigationDto> { [running.Id] = sprint });

        // Act
        var summary = await GetSummary();

        // Assert
        summary.Teams.Should().HaveCount(2);
        var first = summary.Teams[0];
        first.Methodology.Should().Be(Methodology.Scrum);
        first.Sprint!.ActiveSprint.Should().Be(sprint);
        first.Flow.Should().BeNull();
        summary.Teams[1].Sprint.Should().NotBeNull();
        summary.Teams[1].Sprint!.ActiveSprint.Should().BeNull();
        dispatcher.Verify(d => d.Send(It.IsAny<GetKanbanTeamsWindowMetricsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetTeamsSummary_KanbanTeamWithACadence_ReturnsItsFlowAndCurrentPeriod()
    {
        // Arrange
        var team = SummaryTeam("Flow");
        var cadence = ReportingCadence.Create(2, new LocalDate(2026, 11, 2)).Value;
        var oldest = new InProgressWorkItemDto { Id = Guid.NewGuid(), Key = "WAYD-7", WorkspaceKey = "WAYD", Title = "Old item", ActivatedOn = SummaryNow.Minus(Duration.FromDays(20)) };
        var dispatcher = SummaryOf(SummaryNow, [team], new()
        {
            [team.Id] = [Model(new LocalDate(2026, 1, 1), null, Methodology.Kanban, cadence: cadence)],
        }, _allSummaryPermissions);
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetKanbanTeamsWindowMetricsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new KanbanTeamWindowMetricsDto
            {
                Team = team,
                TimeZone = "Etc/UTC",
                CompletedWorkItems = 9,
                InProgressWorkItems = 4,
                MedianCycleTimeDays = 3.5,
                CycleTime = CycleTimeSummary.Empty,
            }]);
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsOldestInProgressWorkItemQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, InProgressWorkItemDto> { [team.Id] = oldest });

        // Act
        var summary = await GetSummary();

        // Assert
        var flow = summary.Teams.Single().Flow!;
        flow.CurrentPeriod.Should().Be(new MyTeamReportingPeriodResponse { Start = new LocalDate(2026, 11, 2), End = new LocalDate(2026, 11, 15) });
        flow.InProgressCount.Should().Be(4);
        flow.CompletedCount.Should().Be(9);
        flow.CompletedSince.Should().Be(new LocalDate(2026, 10, 27));
        flow.MedianCycleTimeDays.Should().Be(3.5);
        flow.OldestInProgress.Should().Be(oldest);
        summary.Teams.Single().Sprint.Should().BeNull();
        dispatcher.Verify(d => d.Send(
            It.Is<GetKanbanTeamsWindowMetricsQuery>(q => q.Start == new LocalDate(2026, 10, 27) && q.End == new LocalDate(2026, 11, 9) && q.TeamIds.Single() == team.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTeamsSummary_KanbanTeamWithoutACadence_HasNoCurrentPeriod()
    {
        // Arrange
        var team = SummaryTeam("Flow");
        SummaryOf(SummaryNow, [team], new()
        {
            [team.Id] = [Model(new LocalDate(2026, 1, 1), null, Methodology.Kanban)],
        }, _allSummaryPermissions);

        // Act
        var summary = await GetSummary();

        // Assert
        var flow = summary.Teams.Single().Flow!;
        flow.CurrentPeriod.Should().BeNull();
        flow.InProgressCount.Should().Be(0);
    }

    [Fact]
    public async Task GetTeamsSummary_KanbanSinceThisWeek_CountsCompletionsOnlyFromThatDay()
    {
        // Arrange
        var team = SummaryTeam("Flow");
        var dispatcher = SummaryOf(SummaryNow, [team], new()
        {
            [team.Id] = [
                Model(new LocalDate(2026, 1, 1), new LocalDate(2026, 11, 4), Methodology.Scrum),
                Model(new LocalDate(2026, 11, 5), null, Methodology.Kanban)],
        }, _allSummaryPermissions);

        // Act
        var summary = await GetSummary();

        // Assert
        summary.Teams.Single().Flow!.CompletedSince.Should().Be(new LocalDate(2026, 11, 5));
        dispatcher.Verify(d => d.Send(
            It.Is<GetKanbanTeamsWindowMetricsQuery>(q => q.Start == new LocalDate(2026, 11, 5)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTeamsSummary_MethodologyChangingToday_ReadsTodayInTheTeamsOwnTimeZone()
    {
        // Arrange
        // 12:00 on 9 November in UTC is already 01:00 on 10 November in Auckland.
        var now = Instant.FromUtc(2026, 11, 9, 12, 0);
        var team = SummaryTeam("Switching");
        SummaryOf(now, [team], new()
        {
            [team.Id] = [
                Model(new LocalDate(2026, 1, 1), new LocalDate(2026, 11, 9), Methodology.Scrum, Auckland),
                Model(new LocalDate(2026, 11, 10), null, Methodology.Kanban, Auckland)],
        }, _allSummaryPermissions);

        // Act
        var summary = await GetSummary();

        // Assert
        var entry = summary.Teams.Single();
        entry.Methodology.Should().Be(Methodology.Kanban);
        entry.TimeZone.Should().Be(Auckland);
        entry.Sprint.Should().BeNull();
        entry.Flow!.CompletedSince.Should().Be(new LocalDate(2026, 11, 10));
    }

    [Fact]
    public async Task GetTeamsSummary_KanbanModelEditedThisWeek_CountsCompletionsFromWhenTheTeamBecameKanban()
    {
        // Arrange
        var team = SummaryTeam("Flow");
        var dispatcher = SummaryOf(SummaryNow, [team], new()
        {
            [team.Id] = [
                Model(new LocalDate(2026, 1, 1), new LocalDate(2026, 6, 30), Methodology.Scrum),
                Model(new LocalDate(2026, 7, 1), new LocalDate(2026, 11, 7), Methodology.Kanban),
                Model(new LocalDate(2026, 11, 8), null, Methodology.Kanban)],
        }, _allSummaryPermissions);

        // Act
        var summary = await GetSummary();

        // Assert
        summary.Teams.Single().Flow!.CompletedSince.Should().Be(new LocalDate(2026, 10, 27));
        dispatcher.Verify(d => d.Send(
            It.Is<GetKanbanTeamsWindowMetricsQuery>(q => q.Start == new LocalDate(2026, 10, 27)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTeamsSummary_MovingToAZoneFurtherWest_KeepsTheOldModelUntilTheNewOneStarts()
    {
        // Arrange
        // 12:00 on 9 November in UTC is 10 November in Auckland, where the old model has ended, and still
        // 9 November in Los Angeles, where the new one has not begun.
        var now = Instant.FromUtc(2026, 11, 9, 12, 0);
        var team = SummaryTeam("Moving");
        SummaryOf(now, [team], new()
        {
            [team.Id] = [
                Model(new LocalDate(2026, 1, 1), new LocalDate(2026, 11, 9), Methodology.Scrum, Auckland),
                Model(new LocalDate(2026, 11, 10), null, Methodology.Kanban, "America/Los_Angeles")],
        }, _allSummaryPermissions);

        // Act
        var summary = await GetSummary();

        // Assert
        summary.Teams.Single().Methodology.Should().Be(Methodology.Scrum);
        summary.Teams.Single().TimeZone.Should().Be(Auckland);
    }

    [Fact]
    public async Task GetTeamsSummary_KanbanTeamWithAZoneTheTzDatabaseDropped_ReadsItAsUtc()
    {
        // Arrange
        var team = SummaryTeam("Stale");
        var cadence = ReportingCadence.Create(2, new LocalDate(2026, 11, 2)).Value;
        SummaryOf(SummaryNow, [team], new()
        {
            [team.Id] = [Model(new LocalDate(2026, 1, 1), null, Methodology.Kanban, "Mars/Olympus_Mons", cadence)],
        }, _allSummaryPermissions);

        // Act
        var summary = await GetSummary();

        // Assert
        var flow = summary.Teams.Single().Flow!;
        flow.CompletedSince.Should().Be(new LocalDate(2026, 10, 27));
        flow.CurrentPeriod!.Start.Should().Be(new LocalDate(2026, 11, 2));
    }

    [Fact]
    public async Task GetTeamsSummary_TeamWithNoOperatingModel_ReturnsOnlyDependenciesAndRisks()
    {
        // Arrange
        var team = SummaryTeam("Unmodelled");
        var dispatcher = SummaryOf(SummaryNow, [team], [], _allSummaryPermissions);
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsDependencyHealthQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TeamsDependencyHealthDto
            {
                Teams = new Dictionary<Guid, TeamDependencyHealthDto> { [team.Id] = new() { OpenCount = 3, AtRiskCount = 1, UnhealthyCount = 1 } },
                AtRiskOrUnhealthyCount = 2,
            });
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsOpenRiskCountQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [team.Id] = 5 });

        // Act
        var summary = await GetSummary();

        // Assert
        var entry = summary.Teams.Single();
        entry.Methodology.Should().BeNull();
        entry.Sprint.Should().BeNull();
        entry.Flow.Should().BeNull();
        entry.Dependencies.Should().Be(new TeamDependencyHealthDto { OpenCount = 3, AtRiskCount = 1, UnhealthyCount = 1 });
        entry.OpenRiskCount.Should().Be(5);
        summary.DependenciesAtRiskCount.Should().Be(2);
    }

    [Fact]
    public async Task GetTeamsSummary_TeamWithNothingOpen_CountsZeroRatherThanNull()
    {
        // Arrange
        var team = SummaryTeam("Quiet");
        SummaryOf(SummaryNow, [team], [], _allSummaryPermissions);

        // Act
        var summary = await GetSummary();

        // Assert
        summary.Teams.Single().Dependencies.Should().Be(new TeamDependencyHealthDto());
        summary.Teams.Single().OpenRiskCount.Should().Be(0);
        summary.DependenciesAtRiskCount.Should().Be(0);
    }

    [Theory]
    [InlineData(ApplicationResource.Iterations)]
    [InlineData(ApplicationResource.WorkItems)]
    [InlineData(ApplicationResource.Risks)]
    public async Task GetTeamsSummary_WithoutASectionsPermission_LeavesThatSectionOut(string missing)
    {
        // Arrange
        var scrum = SummaryTeam("Scrum");
        var kanban = SummaryTeam("Kanban");
        var granted = _allSummaryPermissions.Where(p => p != ApplicationPermission.NameFor(ApplicationAction.View, missing)).ToArray();
        var dispatcher = SummaryOf(SummaryNow, [kanban, scrum], new()
        {
            [scrum.Id] = [Model(new LocalDate(2026, 1, 1), null, Methodology.Scrum)],
            [kanban.Id] = [Model(new LocalDate(2026, 1, 1), null, Methodology.Kanban)],
        }, granted);

        // Act
        var summary = await GetSummary();

        // Assert
        var scrumEntry = summary.Teams.Single(t => t.Team.Id == scrum.Id);
        var kanbanEntry = summary.Teams.Single(t => t.Team.Id == kanban.Id);
        (scrumEntry.Sprint is null).Should().Be(missing == ApplicationResource.Iterations);
        (kanbanEntry.Flow is null).Should().Be(missing == ApplicationResource.WorkItems);
        (scrumEntry.Dependencies is null).Should().Be(missing == ApplicationResource.WorkItems);
        (summary.DependenciesAtRiskCount is null).Should().Be(missing == ApplicationResource.WorkItems);
        (scrumEntry.OpenRiskCount is null).Should().Be(missing == ApplicationResource.Risks);

        var sends = missing switch
        {
            ApplicationResource.Iterations => dispatcher.Invocations.Count(i => i.Arguments[0] is GetTeamsActiveSprintQuery),
            ApplicationResource.WorkItems => dispatcher.Invocations.Count(i => i.Arguments[0] is GetTeamsDependencyHealthQuery or GetKanbanTeamsWindowMetricsQuery),
            _ => dispatcher.Invocations.Count(i => i.Arguments[0] is GetTeamsOpenRiskCountQuery),
        };
        sends.Should().Be(0, "a section the caller cannot see is never read");
    }

    [Fact]
    public async Task GetTeamsSummary_OnNoTeam_ReturnsNoTeams()
    {
        // Arrange
        var dispatcher = SummaryOf(SummaryNow, [], [], _allSummaryPermissions);

        // Act
        var summary = await GetSummary();

        // Assert
        summary.Teams.Should().BeEmpty();
        summary.DependenciesAtRiskCount.Should().Be(0);
        dispatcher.Verify(d => d.Send(It.IsAny<GetTeamsScheduleHistoryQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static readonly LocalDate SummaryToday = SummaryNow.InUtc().Date;

    private static TeamNavigationDto SummaryTeamOfTeams(string name) =>
        new() { Id = Guid.NewGuid(), Key = Random.Shared.Next(1, 10_000), Name = name, Code = name.ToUpperInvariant(), Type = "Team of Teams" };

    private static TeamStructureTeam Node(TeamNavigationDto team) =>
        new(team.Id, team.Key, team.Code, team.Name, team.Type == "Team" ? TeamType.Team : TeamType.TeamOfTeams);

    /// <summary>A structure for <paramref name="root"/> with each (child, parent) edge open since the start of the year.</summary>
    private static TeamStructure Structure(TeamNavigationDto root, params (TeamNavigationDto Child, TeamNavigationDto Parent)[] edges) =>
        new(
            root.Id,
            [Node(root), .. edges.Select(e => Node(e.Child))],
            [.. edges.Select(e => new TeamStructureMembership(e.Child.Id, e.Parent.Id, new LocalDate(2026, 1, 1), null))],
            []);

    /// <summary>Makes the caller a direct member of <paramref name="structures"/>' roots.</summary>
    private static void TeamsOfTeamsAre(Mock<IDispatcher> dispatcher, params (TeamNavigationDto Root, TeamStructure Structure)[] structures)
    {
        dispatcher
            .Setup(d => d.Send(It.Is<GetEmployeeActiveTeamsQuery>(q => q.Type == TeamType.TeamOfTeams), It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. structures.Select(s => s.Root)]);
        foreach (var (root, structure) in structures)
        {
            dispatcher
                .Setup(d => d.Send(It.Is<GetTeamStructureQuery>(q => q.TeamId == root.Id), It.IsAny<CancellationToken>()))
                .ReturnsAsync(structure);
        }
    }

    private static TeamSprintSignalDto Signal(AttentionStatus status, bool endedNotCompleted = false) =>
        new()
        {
            Sprint = new NavigationDto { Id = Guid.NewGuid(), Key = Random.Shared.Next(1, 10_000), Name = "Sprint" },
            EndedNotCompleted = endedNotCompleted,
            ExpectedShare = endedNotCompleted ? null : 0.5,
            DoneShare = endedNotCompleted ? null : 0.4,
            Status = status,
        };

    private static KanbanTeamWindowMetricsDto Metrics(TeamNavigationDto team, double medianDays, int inProgress = 3) =>
        new() { Team = team, TimeZone = "Etc/UTC", InProgressWorkItems = inProgress, MedianCycleTimeDays = medianDays, CycleTime = CycleTimeSummary.Empty };

    private static InProgressWorkItemDto OldestFor(double days) =>
        new() { Id = Guid.NewGuid(), Key = "WAYD-1", WorkspaceKey = "WAYD", Title = "Oldest", ActivatedOn = SummaryNow.Minus(Duration.FromHours(days * 24)) };

    private static DependencyDto Dependency(DependencyPlanningHealth health, Guid? id = null)
    {
        WorkItemDetailsNavigationDto Item() => new()
        {
            Id = Guid.NewGuid(),
            Key = "WAYD-1",
            Title = "Item",
            WorkspaceKey = "WAYD",
            Type = "Story",
            Status = "Active",
            StatusCategory = SimpleNavigationDto.FromEnum(StatusCategory.Active),
        };

        return new DependencyDto
        {
            Id = id ?? Guid.NewGuid(),
            Source = Item(),
            Target = Item(),
            LinkType = SimpleNavigationDto.Create(2, "Dependency"),
            State = SimpleNavigationDto.FromEnum(DependencyState.InProgress),
            Health = SimpleNavigationDto.FromEnum(health),
        };
    }

    private static readonly string[] _allRollupPermissions =
    [
        .. _allSummaryPermissions,
        ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.PlanningIntervals),
        ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.PlanningIntervalObjectives),
    ];

    [Fact]
    public async Task GetTeamsSummary_TeamOfTeamsOfScrumAndKanbanTeams_RowsEachTeamNeedingAttentionFirst()
    {
        // Arrange
        var train = SummaryTeamOfTeams("Train");
        var behind = SummaryTeam("Behind");
        var steady = SummaryTeam("Steady");
        var stuck = SummaryTeam("Stuck");
        var slowing = SummaryTeam("Slowing");
        var flowing = SummaryTeam("Flowing");
        var unmodelled = SummaryTeam("Unmodelled");
        var dispatcher = SummaryOf(SummaryNow, [steady], new()
        {
            [behind.Id] = [Model(new LocalDate(2026, 1, 1), null, Methodology.Scrum)],
            [steady.Id] = [Model(new LocalDate(2026, 1, 1), null, Methodology.Scrum)],
            [stuck.Id] = [Model(new LocalDate(2026, 1, 1), null, Methodology.Kanban)],
            [slowing.Id] = [Model(new LocalDate(2026, 1, 1), null, Methodology.Kanban)],
            [flowing.Id] = [Model(new LocalDate(2026, 1, 1), null, Methodology.Kanban)],
        }, _allRollupPermissions);
        TeamsOfTeamsAre(dispatcher, (train, Structure(train,
            (behind, train), (steady, train), (stuck, train), (slowing, train), (flowing, train), (unmodelled, train))));
        var behindSignal = Signal(AttentionStatus.Watch);
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsSprintSignalQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, TeamSprintSignalDto> { [behind.Id] = behindSignal });
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetKanbanTeamsWindowMetricsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Metrics(stuck, 2), Metrics(slowing, 2), Metrics(flowing, 2)]);
        var stuckOldest = OldestFor(7);
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsOldestInProgressWorkItemQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, InProgressWorkItemDto>
            {
                [stuck.Id] = stuckOldest,
                [slowing.Id] = OldestFor(5),
                [flowing.Id] = OldestFor(3),
            });

        // Act
        var summary = await GetSummary();

        // Assert
        var rollup = summary.TeamsOfTeams.Single();
        rollup.Team.Should().Be(train);
        rollup.ScrumTeamCount.Should().Be(2);
        rollup.KanbanTeamCount.Should().Be(3);
        rollup.Rows.Select(r => (r.Team.Name, r.Status)).Should().Equal(
            ("Stuck", AttentionStatus.NeedsAttention),
            ("Behind", AttentionStatus.Watch),
            ("Slowing", AttentionStatus.Watch),
            ("Flowing", AttentionStatus.OnTrack),
            ("Steady", AttentionStatus.OnTrack),
            ("Unmodelled", AttentionStatus.OnTrack));

        rollup.Rows.Single(r => r.Team.Id == behind.Id).Sprint.Should().Be(behindSignal);
        var stuckFlow = rollup.Rows.Single(r => r.Team.Id == stuck.Id).Flow!;
        stuckFlow.InProgressCount.Should().Be(3);
        stuckFlow.OldestInProgressSince.Should().Be(stuckOldest.ActivatedOn);
        stuckFlow.MedianCycleTimeDays.Should().Be(2);
        rollup.Rows.Where(r => r.IsMine).Select(r => r.Team.Id).Should().Equal(steady.Id);

        dispatcher.Verify(d => d.Send(
            It.Is<GetTeamsSprintSignalQuery>(q => q.TeamIds.Order().SequenceEqual(new[] { behind.Id, steady.Id }.Order())),
            It.IsAny<CancellationToken>()), Times.Once);
        dispatcher.Verify(d => d.Send(
            It.Is<GetTeamStructureQuery>(q => q.TeamId == train.Id && q.From == SummaryToday && q.To == SummaryToday),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTeamsSummary_TeamWithNoOperatingModelInARollup_ShowsOnlyItsName()
    {
        // Arrange
        var train = SummaryTeamOfTeams("Train");
        var unmodelled = SummaryTeam("Unmodelled");
        var dispatcher = SummaryOf(SummaryNow, [], [], _allRollupPermissions);
        TeamsOfTeamsAre(dispatcher, (train, Structure(train, (unmodelled, train))));

        // Act
        var summary = await GetSummary();

        // Assert
        var row = summary.TeamsOfTeams.Single().Rows.Single();
        row.Team.Id.Should().Be(unmodelled.Id);
        row.Team.Type.Should().Be("Team");
        row.Methodology.Should().BeNull();
        row.Sprint.Should().BeNull();
        row.Flow.Should().BeNull();
        row.Nested.Should().BeNull();
        row.Status.Should().Be(AttentionStatus.OnTrack);
        summary.TeamsOfTeams.Single().ScrumTeamCount.Should().Be(0);
        dispatcher.Verify(d => d.Send(It.IsAny<GetTeamsSprintSignalQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetTeamsSummary_NestedTeamOfTeams_IsOneRowCarryingTheWorstStatusBeneathIt()
    {
        // Arrange — Train holds Direct and Platform; Platform holds Api and Core, which holds Data. The caller is on
        // both Train and Platform.
        var train = SummaryTeamOfTeams("Train");
        var direct = SummaryTeam("Direct");
        var platform = SummaryTeamOfTeams("Platform");
        var api = SummaryTeam("Api");
        var core = SummaryTeamOfTeams("Core");
        var data = SummaryTeam("Data");
        var scrum = new LocalDate(2026, 1, 1);
        var dispatcher = SummaryOf(SummaryNow, [], new()
        {
            [direct.Id] = [Model(scrum, null, Methodology.Scrum)],
            [api.Id] = [Model(scrum, null, Methodology.Scrum)],
            [data.Id] = [Model(scrum, null, Methodology.Scrum)],
        }, _allRollupPermissions);
        TeamsOfTeamsAre(dispatcher,
            (platform, Structure(platform, (api, platform), (core, platform), (data, core))),
            (train, Structure(train, (direct, train), (platform, train), (api, platform), (core, platform), (data, core))));
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsSprintSignalQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, TeamSprintSignalDto>
            {
                [direct.Id] = Signal(AttentionStatus.OnTrack),
                [api.Id] = Signal(AttentionStatus.Watch),
                [data.Id] = Signal(AttentionStatus.NeedsAttention, endedNotCompleted: true),
            });

        // Act
        var summary = await GetSummary();

        // Assert
        var rows = summary.TeamsOfTeams.Single(r => r.Team.Id == train.Id).Rows;
        rows.Select(r => r.Team.Id).Should().Equal(platform.Id, direct.Id);
        var nested = rows[0];
        nested.IsMine.Should().BeTrue("the caller is a member of the nested team of teams directly");
        nested.Team.Type.Should().Be("Team of Teams");
        nested.Status.Should().Be(AttentionStatus.NeedsAttention);
        nested.Nested.Should().Be(new MyTeamOfTeamsNestedResponse { TeamCount = 2, NeedsAttentionCount = 1, WatchCount = 1 });
        nested.Methodology.Should().BeNull();
        nested.Sprint.Should().BeNull();
        summary.SprintsEndedNotCompletedCount.Should().Be(1);
    }

    [Fact]
    public async Task GetTeamsSummary_ChildSprintEndedButNotCompleted_NeedsAttentionAndIsCountedOnce()
    {
        // Arrange — the caller is on two teams of teams that share the team left with a sprint open
        var train = SummaryTeamOfTeams("Train");
        var guild = SummaryTeamOfTeams("Guild");
        var shared = SummaryTeam("Shared");
        var dispatcher = SummaryOf(SummaryNow, [], new()
        {
            [shared.Id] = [Model(new LocalDate(2026, 1, 1), null, Methodology.Scrum)],
        }, _allRollupPermissions);
        TeamsOfTeamsAre(dispatcher, (guild, Structure(guild, (shared, guild))), (train, Structure(train, (shared, train))));
        var leftOpen = Signal(AttentionStatus.NeedsAttention, endedNotCompleted: true);
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsSprintSignalQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, TeamSprintSignalDto> { [shared.Id] = leftOpen });

        // Act
        var summary = await GetSummary();

        // Assert
        summary.TeamsOfTeams.Should().HaveCount(2);
        summary.TeamsOfTeams.Should().AllSatisfy(r =>
        {
            r.Rows.Single().Sprint.Should().Be(leftOpen);
            r.Rows.Single().Status.Should().Be(AttentionStatus.NeedsAttention);
        });
        summary.SprintsEndedNotCompletedCount.Should().Be(1);
    }

    [Fact]
    public async Task GetTeamsSummary_RollupFooter_CountsCrossTeamDependenciesRisksAndThePlanningInterval()
    {
        // Arrange
        var train = SummaryTeamOfTeams("Train");
        var guild = SummaryTeamOfTeams("Guild");
        var payments = SummaryTeam("Payments");
        var checkout = SummaryTeam("Checkout");
        var dispatcher = SummaryOf(SummaryNow, [], [], _allRollupPermissions);
        TeamsOfTeamsAre(dispatcher, (train, Structure(train, (payments, train), (checkout, train))), (guild, Structure(guild, (checkout, guild))));
        var sharedUnhealthy = Guid.NewGuid();
        dispatcher
            .Setup(d => d.Send(It.Is<GetTeamsDependenciesQuery>(q => q.TeamIds.Contains(payments.Id)), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Dependency(DependencyPlanningHealth.Healthy),
                Dependency(DependencyPlanningHealth.AtRisk),
                Dependency(DependencyPlanningHealth.Unhealthy, sharedUnhealthy),
                Dependency(DependencyPlanningHealth.Unhealthy),
            ]);
        dispatcher
            .Setup(d => d.Send(It.Is<GetTeamsDependenciesQuery>(q => !q.TeamIds.Contains(payments.Id)), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Dependency(DependencyPlanningHealth.Unhealthy, sharedUnhealthy)]);
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsOpenRiskCountQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [train.Id] = 2, [payments.Id] = 3, [checkout.Id] = 4 });
        var pi = new NavigationDto { Id = Guid.NewGuid(), Key = 8, Name = "PI 26.4" };
        dispatcher
            .Setup(d => d.Send(It.Is<GetCurrentPlanningIntervalForTeamsQuery>(q => q.PreferredTeamId == train.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CurrentPlanningIntervalDto { PlanningInterval = pi, ObjectivesAtRiskCount = 5 });

        // Act
        var summary = await GetSummary();

        // Assert
        var rollup = summary.TeamsOfTeams.Single(r => r.Team.Id == train.Id);
        rollup.Dependencies.Should().Be(new MyTeamOfTeamsDependenciesResponse { OpenCount = 4, AtRiskCount = 1, UnhealthyCount = 2 });
        rollup.Risks.Should().Be(new MyTeamOfTeamsRisksResponse { OwnOpenCount = 2, TeamsOpenCount = 7 });
        rollup.PlanningInterval.Should().Be(new MyTeamOfTeamsPlanningIntervalResponse { PlanningInterval = pi, ObjectivesAtRiskCount = 5 });
        summary.TeamsOfTeams.Single(r => r.Team.Id == guild.Id).PlanningInterval.Should().BeNull();
        summary.UnhealthyCrossTeamDependencyCount.Should().Be(2);
        dispatcher.Verify(d => d.Send(
            It.Is<GetCurrentPlanningIntervalForTeamsQuery>(q => q.PreferredTeamId == train.Id && q.Today == SummaryToday
                && q.TeamIds.Order().SequenceEqual(new[] { train.Id, payments.Id, checkout.Id }.Order())),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(ApplicationResource.WorkItems)]
    [InlineData(ApplicationResource.Risks)]
    [InlineData(ApplicationResource.PlanningIntervals)]
    [InlineData(ApplicationResource.PlanningIntervalObjectives)]
    public async Task GetTeamsSummary_WithoutAFooterSectionsPermission_LeavesThatSectionOutOfTheRollup(string missing)
    {
        // Arrange
        var train = SummaryTeamOfTeams("Train");
        var payments = SummaryTeam("Payments");
        var granted = _allRollupPermissions.Where(p => p != ApplicationPermission.NameFor(ApplicationAction.View, missing)).ToArray();
        var dispatcher = SummaryOf(SummaryNow, [], [], granted);
        TeamsOfTeamsAre(dispatcher, (train, Structure(train, (payments, train))));
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetCurrentPlanningIntervalForTeamsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetCurrentPlanningIntervalForTeamsQuery q, CancellationToken _) => new CurrentPlanningIntervalDto
            {
                PlanningInterval = new NavigationDto { Id = Guid.NewGuid(), Key = 1, Name = "PI" },
                ObjectivesAtRiskCount = q.IncludeObjectives ? 3 : null,
            });

        // Act
        var summary = await GetSummary();

        // Assert
        var rollup = summary.TeamsOfTeams.Single();
        (rollup.Dependencies is null).Should().Be(missing == ApplicationResource.WorkItems);
        (summary.UnhealthyCrossTeamDependencyCount is null).Should().Be(missing == ApplicationResource.WorkItems);
        (rollup.Risks is null).Should().Be(missing == ApplicationResource.Risks);
        (rollup.PlanningInterval is null).Should().Be(missing == ApplicationResource.PlanningIntervals);
        (rollup.PlanningInterval?.ObjectivesAtRiskCount is null).Should().Be(missing is ApplicationResource.PlanningIntervals or ApplicationResource.PlanningIntervalObjectives);

        var sends = missing switch
        {
            ApplicationResource.WorkItems => dispatcher.Invocations.Count(i => i.Arguments[0] is GetTeamsDependenciesQuery),
            ApplicationResource.Risks => dispatcher.Invocations.Count(i => i.Arguments[0] is GetTeamsOpenRiskCountQuery),
            ApplicationResource.PlanningIntervals => dispatcher.Invocations.Count(i => i.Arguments[0] is GetCurrentPlanningIntervalForTeamsQuery),
            _ => dispatcher.Invocations.Count(i => i.Arguments[0] is GetCurrentPlanningIntervalForTeamsQuery { IncludeObjectives: true }),
        };
        sends.Should().Be(0, "a section the caller cannot see is never read");
    }

    [Fact]
    public async Task GetTeamsSummary_TeamInATeamOfTeams_SaysWhichItIsPartOf()
    {
        // Arrange
        var payments = SummaryTeam("Payments");
        var loner = SummaryTeam("Loner");
        var train = SummaryTeamOfTeams("Train");
        var dispatcher = SummaryOf(SummaryNow, [loner, payments], [], _allSummaryPermissions);
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsParentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, TeamNavigationDto> { [payments.Id] = train });

        // Act
        var summary = await GetSummary();

        // Assert
        summary.Teams.Single(t => t.Team.Id == payments.Id).PartOf.Should().Be(train);
        summary.Teams.Single(t => t.Team.Id == loner.Id).PartOf.Should().BeNull();
        summary.TeamsOfTeams.Should().BeEmpty();
        dispatcher.Verify(d => d.Send(It.Is<GetTeamsParentQuery>(q => q.Date == SummaryToday), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTeamsSummary_NarrowedToSomeTeams_SummarisesAndCountsOnlyThoseButListsEveryTeam()
    {
        // Arrange
        var kept = SummaryTeam("Kept");
        var left = SummaryTeam("Left");
        var dispatcher = SummaryOf(SummaryNow, [kept, left], new()
        {
            [kept.Id] = [Model(new LocalDate(2026, 1, 1), null, Methodology.Scrum)],
            [left.Id] = [Model(new LocalDate(2026, 1, 1), null, Methodology.Scrum)],
        }, _allSummaryPermissions);
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsDependencyHealthQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TeamsDependencyHealthDto { AtRiskOrUnhealthyCount = 2 });

        // Act
        var summary = await GetSummary(teamIds: [kept.Id]);

        // Assert
        summary.Teams.Select(t => t.Team.Id).Should().Equal(kept.Id);
        summary.AllTeams.Should().Equal(kept, left);
        summary.DependenciesAtRiskCount.Should().Be(2);
        dispatcher.Verify(d => d.Send(
            It.Is<GetTeamsDependencyHealthQuery>(q => q.TeamIds.SequenceEqual(new[] { kept.Id })),
            It.IsAny<CancellationToken>()), Times.Once);
        dispatcher.Verify(d => d.Send(
            It.Is<GetTeamsActiveSprintQuery>(q => q.TeamIds.SequenceEqual(new[] { kept.Id })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTeamsSummary_NarrowedToATeamAndOneTheCallerLeft_IgnoresTheOneLeft()
    {
        // Arrange
        var kept = SummaryTeam("Kept");
        var other = SummaryTeam("Other");
        SummaryOf(SummaryNow, [kept, other], [], _allSummaryPermissions);

        // Act
        var summary = await GetSummary(teamIds: [kept.Id, Guid.NewGuid()]);

        // Assert
        summary.Teams.Select(t => t.Team.Id).Should().Equal(kept.Id);
    }

    [Fact]
    public async Task GetTeamsSummary_NarrowedOnlyToTeamsTheCallerLeft_ShowsEveryTeam()
    {
        // Arrange
        var first = SummaryTeam("First");
        var second = SummaryTeam("Second");
        SummaryOf(SummaryNow, [first, second], [], _allSummaryPermissions);

        // Act
        var summary = await GetSummary(teamIds: [Guid.NewGuid()]);

        // Assert
        summary.Teams.Select(t => t.Team.Id).Should().Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task GetTeamsSummary_NarrowedToOneTeamOfTeams_RollsUpAndCountsOnlyThatOne()
    {
        // Arrange — each team of teams has a team left with a sprint open; only Train is chosen.
        var train = SummaryTeamOfTeams("Train");
        var guild = SummaryTeamOfTeams("Guild");
        var onTrain = SummaryTeam("OnTrain");
        var onGuild = SummaryTeam("OnGuild");
        var scrum = new LocalDate(2026, 1, 1);
        var dispatcher = SummaryOf(SummaryNow, [], new()
        {
            [onTrain.Id] = [Model(scrum, null, Methodology.Scrum)],
            [onGuild.Id] = [Model(scrum, null, Methodology.Scrum)],
        }, _allRollupPermissions);
        TeamsOfTeamsAre(dispatcher, (guild, Structure(guild, (onGuild, guild))), (train, Structure(train, (onTrain, train), (guild, train))));
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsSprintSignalQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetTeamsSprintSignalQuery q, CancellationToken _) => q.TeamIds.ToDictionary(
                id => id, _ => Signal(AttentionStatus.NeedsAttention, endedNotCompleted: true)));

        // Act
        var summary = await GetSummary(teamOfTeamsIds: [train.Id]);

        // Assert
        summary.TeamsOfTeams.Select(r => r.Team.Id).Should().Equal(train.Id);
        summary.AllTeamsOfTeams.Should().Equal(guild, train);
        summary.TeamsOfTeams.Single().Rows.Single(r => r.Team.Id == guild.Id).IsMine
            .Should().BeTrue("the caller is still a member of the team of teams left out");
        summary.SprintsEndedNotCompletedCount.Should().Be(1);
        dispatcher.Verify(d => d.Send(It.Is<GetTeamStructureQuery>(q => q.TeamId == guild.Id), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetTeamsSummary_NarrowedToOneTeamOfTeams_CountsOnlyItsUnhealthyCrossTeamDependencies()
    {
        // Arrange — each team of teams has one unhealthy dependency of its own; only Train is chosen
        var train = SummaryTeamOfTeams("Train");
        var guild = SummaryTeamOfTeams("Guild");
        var payments = SummaryTeam("Payments");
        var checkout = SummaryTeam("Checkout");
        var dispatcher = SummaryOf(SummaryNow, [], [], _allRollupPermissions);
        TeamsOfTeamsAre(dispatcher, (train, Structure(train, (payments, train))), (guild, Structure(guild, (checkout, guild))));
        dispatcher
            .Setup(d => d.Send(It.Is<GetTeamsDependenciesQuery>(q => q.TeamIds.Contains(payments.Id)), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Dependency(DependencyPlanningHealth.Unhealthy)]);
        dispatcher
            .Setup(d => d.Send(It.Is<GetTeamsDependenciesQuery>(q => q.TeamIds.Contains(checkout.Id)), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Dependency(DependencyPlanningHealth.Unhealthy)]);

        // Act
        var summary = await GetSummary(teamOfTeamsIds: [train.Id]);

        // Assert
        summary.UnhealthyCrossTeamDependencyCount.Should().Be(1);
        dispatcher.Verify(d => d.Send(It.Is<GetTeamsDependenciesQuery>(q => q.TeamIds.Contains(checkout.Id)), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetTeamsSummary_NarrowedOnlyToATeamOfTeams_KeepsEveryTeam()
    {
        // Arrange
        var first = SummaryTeam("First");
        var second = SummaryTeam("Second");
        var train = SummaryTeamOfTeams("Train");
        var guild = SummaryTeamOfTeams("Guild");
        var dispatcher = SummaryOf(SummaryNow, [first, second], [], _allRollupPermissions);
        TeamsOfTeamsAre(dispatcher, (guild, Structure(guild)), (train, Structure(train)));

        // Act
        var summary = await GetSummary(teamOfTeamsIds: [train.Id]);

        // Assert
        summary.Teams.Select(t => t.Team.Id).Should().Equal(first.Id, second.Id);
        summary.TeamsOfTeams.Select(r => r.Team.Id).Should().Equal(train.Id);
    }

    [Fact]
    public async Task GetTeamsSummary_OnNoTeam_ListsNoTeamsToChooseFrom()
    {
        // Arrange
        SummaryOf(SummaryNow, [], [], _allSummaryPermissions);

        // Act
        var summary = await GetSummary(teamIds: [Guid.NewGuid()]);

        // Assert
        summary.AllTeams.Should().BeEmpty();
        summary.AllTeamsOfTeams.Should().BeEmpty();
    }

    [Theory]
    [InlineData(nameof(MeController.GetProfile))]
    [InlineData(nameof(MeController.GetTeamMemberships))]
    [InlineData(nameof(MeController.GetAllocations))]
    [InlineData(nameof(MeController.GetHomeContext))]
    [InlineData(nameof(MeController.GetTeamsSummary))]
    public void ReadingYourOwnRecords_NeedsNoPermission(string action)
    {
        // Arrange
        var method = typeof(MeController).GetMethod(action)!;

        // Act
        var permission = method.GetCustomAttribute<MustHavePermissionAttribute>();

        // Assert
        permission.Should().BeNull();
    }

    [Fact]
    public void Tools_AreOfferedOnEveryConnection()
    {
        // Arrange
        var controller = typeof(MeController);

        // Act
        var placement = controller.GetCustomAttribute<McpToolsAttribute>()!;

        // Assert
        placement.Toolset.Should().BeNull();
        placement.Skill.Should().Be("wayd-users");
    }
}
