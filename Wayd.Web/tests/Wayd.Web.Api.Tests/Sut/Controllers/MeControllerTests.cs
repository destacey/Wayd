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
using Wayd.Planning.Application.Risks.Queries;
using Wayd.ProductManagement.Application.Products.Queries;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Queries;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.Web.Api.Controllers;
using Wayd.Web.Api.Mcp;
using Wayd.Web.Api.Models.Me;
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
        _mocker
            .GetMock<ICurrentPrincipal>()
            .Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string permission, CancellationToken _) => permissions.Contains(permission));

        var dispatcher = _mocker.GetMock<IDispatcher>();
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetEmployeeActiveTeamsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(teams);
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

    private async Task<MyTeamsSummaryResponse> GetSummary()
    {
        var result = await CreateController().GetTeamsSummary(TestContext.Current.CancellationToken);
        return result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<MyTeamsSummaryResponse>().Subject;
    }

    [Fact]
    public async Task GetTeamsSummary_WhenTheAccountHasNoEmployee_ReturnsNotFound()
    {
        // Arrange
        LinkedTo(null);
        var controller = CreateController();

        // Act
        var result = await controller.GetTeamsSummary(TestContext.Current.CancellationToken);

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
