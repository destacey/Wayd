using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using NodaTime;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Interfaces;
using Wayd.Organization.Application.Models;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Web.Api.Controllers.Organizations;
using Wayd.Work.Application.Iterations.Dtos;
using Wayd.Work.Application.Iterations.Queries;
using Wayd.Work.Application.WorkTeams.Dtos;

namespace Wayd.Web.Api.Tests.Sut.Controllers.Organizations;

public sealed class TeamsControllerTests
{
    private readonly AutoMocker _mocker = new();

    private TeamsController CreateController()
    {
        var controller = _mocker.CreateInstance<TeamsController>();
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    [Fact]
    public async Task GetActiveSprint_WhenTheTeamHasOne_ReturnsIt()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var sprint = new SprintDetailsDto
        {
            Id = Guid.NewGuid(),
            Key = 12,
            Name = "Sprint 12",
            State = new SimpleNavigationDto { Id = 2, Name = "Active" },
            Start = new LocalDate(2026, 10, 12),
            End = new LocalDate(2026, 10, 25),
            Team = new WorkTeamNavigationDto { Id = teamId, Key = 3, Name = "Team Atlas", Code = "ATLAS", Type = "Team" },
        };
        _mocker
            .GetMock<IDispatcher>()
            .Setup(d => d.Send(It.Is<GetTeamActiveSprintQuery>(q => q.TeamId == teamId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sprint);

        var controller = CreateController();

        // Act
        var result = await controller.GetActiveSprint(teamId, TestContext.Current.CancellationToken);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(sprint);
    }

    [Fact]
    public async Task GetActiveSprint_WhenTheTeamHasNoSprintInProgress_ReturnsNotFound()
    {
        // Arrange — a null answer once became an empty 204, which a hosted MCP tool reports as a failure.
        _mocker
            .GetMock<IDispatcher>()
            .Setup(d => d.Send(It.IsAny<GetTeamActiveSprintQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SprintDetailsDto?)null);

        var controller = CreateController();

        // Act
        var result = await controller.GetActiveSprint(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetTeamMemberships_ForATeamWithNone_ReturnsAnEmptyList()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        _mocker
            .GetMock<IDispatcher>()
            .Setup(d => d.Send(It.Is<GetTeamMembershipsQuery>(q => q.TeamId == teamId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TeamMembershipDto>());

        var controller = CreateController();

        // Act
        var result = await controller.GetTeamMemberships(teamId, TestContext.Current.CancellationToken);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IEnumerable<TeamMembershipDto>>()
            .Which.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTeamMemberships_ForAnUnknownTeam_ReturnsNotFound()
    {
        // Arrange
        _mocker
            .GetMock<IDispatcher>()
            .Setup(d => d.Send(It.IsAny<GetTeamMembershipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<TeamMembershipDto>?)null);

        var controller = CreateController();

        // Act
        var result = await controller.GetTeamMemberships(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }
}
