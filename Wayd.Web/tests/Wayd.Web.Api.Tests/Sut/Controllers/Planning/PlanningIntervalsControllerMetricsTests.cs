using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using NodaTime;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Interfaces;
using Wayd.Organization.Application.Teams.Dtos;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Planning.Application.Models;
using Wayd.Planning.Application.PlanningIntervals.Dtos;
using Wayd.Planning.Application.PlanningIntervals.Queries;
using Wayd.Planning.Application.PlanningSprints.Dtos;
using Wayd.Web.Api.Controllers.Planning;
using Wayd.Web.Api.Models.Planning.PlanningIntervals;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Queries;

namespace Wayd.Web.Api.Tests.Sut.Controllers.Planning;

public sealed class PlanningIntervalsControllerMetricsTests
{
    private readonly AutoMocker _mocker = new();
    private readonly Guid _sprintId = Guid.NewGuid();
    private readonly Guid _teamId = Guid.NewGuid();

    private PlanningIntervalsController CreateController()
    {
        var controller = _mocker.CreateInstance<PlanningIntervalsController>();
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    /// <summary>A PI with one sprint, mapped for a team whose Planning copy carries <paramref name="sprintTeamCode"/>.</summary>
    private void SetupPlanningIntervalWithOneSprint(string sprintTeamCode)
    {
        var dispatcher = _mocker.GetMock<IDispatcher>();

        dispatcher
            .Setup(d => d.Send(It.IsAny<GetPlanningIntervalQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlanningIntervalDetailsDto
            {
                Id = Guid.NewGuid(),
                Key = 7,
                Name = "PI 26.1",
                Start = new LocalDate(2026, 1, 5),
                End = new LocalDate(2026, 3, 29),
                State = new SimpleNavigationDto { Id = 2, Name = "Active" },
            });

        dispatcher
            .Setup(d => d.Send(It.IsAny<GetPlanningIntervalIterationSprintsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PlanningIntervalIterationSprintsDto
            {
                Id = Guid.NewGuid(),
                Key = 1,
                Name = "Iteration 1",
                Start = new LocalDate(2026, 1, 5),
                End = new LocalDate(2026, 1, 18),
                Category = new SimpleNavigationDto { Id = 1, Name = "Development" },
                Sprints = [new PlanningSprintListDto
                {
                    Id = _sprintId,
                    Key = 1,
                    Name = "S-1",
                    State = new SimpleNavigationDto { Id = 1, Name = "Active" },
                    Team = new PlanningTeamNavigationDto { Id = _teamId, Key = 1, Name = "Team Atlas", Code = sprintTeamCode, Type = "Team" },
                }],
            }]);

        dispatcher
            .Setup(d => d.Send(It.IsAny<GetSprintsWorkItemMetricsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new SprintWorkItemMetricsDto { SprintId = _sprintId, CycleTime = CycleTimeSummary.Empty }]);
    }

    private void SetupOrganizationTeams(params TeamListDto[] teams) =>
        _mocker
            .GetMock<IDispatcher>()
            .Setup(d => d.Send(It.IsAny<GetTeamsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. teams]);

    [Fact]
    public async Task GetMetrics_WhenATeamCodeCannotBeResolved_ReturnsNotFound()
    {
        // Arrange — the team is gone from Organization and its Planning copy has no code.
        SetupPlanningIntervalWithOneSprint(sprintTeamCode: "");
        SetupOrganizationTeams();

        var controller = CreateController();

        // Act
        var result = await controller.GetMetrics("7", TestContext.Current.CancellationToken);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMetrics_WhenOrganizationHasTheTeam_ReturnsItsCode()
    {
        // Arrange
        SetupPlanningIntervalWithOneSprint(sprintTeamCode: "");
        SetupOrganizationTeams(new TeamListDto { Id = _teamId, Key = 1, Name = "Team Atlas", Code = "ATLAS", Type = "Team" });

        var controller = CreateController();

        // Act
        var result = await controller.GetMetrics("7", TestContext.Current.CancellationToken);

        // Assert
        var response = result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<PlanningIntervalMetricsResponse>().Subject;
        response.TeamMetrics.Should().ContainSingle().Which.TeamCode.Should().Be("ATLAS");
    }
}
