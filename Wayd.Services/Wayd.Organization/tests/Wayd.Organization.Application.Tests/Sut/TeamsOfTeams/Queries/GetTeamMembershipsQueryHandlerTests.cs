using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Application.TeamsOfTeams.Queries;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.TestData;

namespace Wayd.Organization.Application.Tests.Sut.TeamsOfTeams.Queries;

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
    public async Task Handle_UnknownTeamOfTeams_ReturnsNull()
    {
        // Arrange
        _dbContext.AddTeamOfTeams(new TeamOfTeamsFaker(ActiveDate).Generate());

        // Act
        var result = await _handler.Handle(new GetTeamMembershipsQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_KnownTeamOfTeams_ReturnsItsParentMembershipThenItsChildren()
    {
        // Arrange
        var teamOfTeams = new TeamOfTeamsFaker(ActiveDate).Generate();
        var parent = new TeamOfTeamsFaker(ActiveDate).Generate();
        var child = new TeamFaker(ActiveDate).Generate();
        _dbContext.AddTeamOfTeams(teamOfTeams);
        _dbContext.AddTeamOfTeams(parent);
        _dbContext.AddTeam(child);
        teamOfTeams.AddTeamMembership(parent, new MembershipDateRange(ActiveDate, null), EventActor.System, Now).IsSuccess.Should().BeTrue();
        child.AddTeamMembership(teamOfTeams, new MembershipDateRange(ActiveDate, null), EventActor.System, Now).IsSuccess.Should().BeTrue();

        // Act
        var result = await _handler.Handle(new GetTeamMembershipsQuery(teamOfTeams.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Select(m => (m.Child.Id, m.Parent.Id)).Should().Equal(
            (teamOfTeams.Id, parent.Id),
            (child.Id, teamOfTeams.Id));
    }
}
