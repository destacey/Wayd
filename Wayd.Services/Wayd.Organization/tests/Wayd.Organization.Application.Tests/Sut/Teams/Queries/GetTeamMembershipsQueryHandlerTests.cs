using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.TestData;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Queries;

public class GetTeamMembershipsQueryHandlerTests
{
    private static readonly LocalDate ActiveDate = new(2025, 1, 1);
    private static readonly Instant Now = Instant.FromUtc(2026, 6, 1, 0, 0);

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly GetTeamMembershipsQueryHandler _handler;

    public GetTeamMembershipsQueryHandlerTests()
    {
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Now).Returns(Now);

        _handler = new GetTeamMembershipsQueryHandler(
            _dbContext,
            new Mock<ILogger<GetTeamMembershipsQueryHandler>>().Object,
            dateTimeProvider.Object);
    }

    [Fact]
    public async Task Handle_UnknownTeam_ReturnsNull()
    {
        // Arrange
        _dbContext.AddTeam(new TeamFaker(ActiveDate).Generate());

        // Act
        var result = await _handler.Handle(new GetTeamMembershipsQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_KnownTeam_ReturnsItsMembershipsInTeamsOfTeams()
    {
        // Arrange
        var team = new TeamFaker(ActiveDate).Generate();
        var parent = new TeamOfTeamsFaker(ActiveDate).Generate();
        _dbContext.AddTeam(team);
        _dbContext.AddTeamOfTeams(parent);
        team.AddTeamMembership(parent, new MembershipDateRange(ActiveDate, null), EventActor.System, Now).IsSuccess.Should().BeTrue();

        // Act
        var result = await _handler.Handle(new GetTeamMembershipsQuery(team.Id), TestContext.Current.CancellationToken);

        // Assert
        var membership = result.Should().ContainSingle().Subject;
        membership.Child.Id.Should().Be(team.Id);
        membership.Parent.Id.Should().Be(parent.Id);
        membership.Start.Should().Be(ActiveDate);
        membership.State.Should().Be("Active");
    }
}
