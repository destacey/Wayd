using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using Wayd.Common.Application.Interfaces;
using Wayd.Organization.Application.Models;
using Wayd.Organization.Application.TeamsOfTeams.Queries;
using Wayd.Web.Api.Controllers.Organizations;

namespace Wayd.Web.Api.Tests.Sut.Controllers.Organizations;

public sealed class TeamsOfTeamsControllerTests
{
    private readonly AutoMocker _mocker = new();

    private TeamsOfTeamsController CreateController()
    {
        var controller = _mocker.CreateInstance<TeamsOfTeamsController>();
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    [Fact]
    public async Task GetTeamMemberships_ForATeamOfTeamsWithNone_ReturnsAnEmptyList()
    {
        // Arrange
        var teamOfTeamsId = Guid.NewGuid();
        _mocker
            .GetMock<IDispatcher>()
            .Setup(d => d.Send(It.Is<GetTeamMembershipsQuery>(q => q.TeamId == teamOfTeamsId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TeamMembershipDto>());

        var controller = CreateController();

        // Act
        var result = await controller.GetTeamMemberships(teamOfTeamsId, TestContext.Current.CancellationToken);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IEnumerable<TeamMembershipDto>>()
            .Which.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTeamMemberships_ForAnUnknownTeamOfTeams_ReturnsNotFound()
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
