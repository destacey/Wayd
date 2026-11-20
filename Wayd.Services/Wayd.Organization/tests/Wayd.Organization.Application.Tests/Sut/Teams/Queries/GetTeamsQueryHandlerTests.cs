using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Queries;

public class GetTeamsQueryHandlerTests
{
    private static readonly LocalDate ActiveDate = new(2025, 1, 1);

    // 1 June in UTC, still 31 May in the system zone.
    private static readonly Instant Now = Instant.FromUtc(2026, 6, 1, 5, 0);

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly GetTeamsQueryHandler _handler;

    public GetTeamsQueryHandlerTests()
    {
        _handler = new GetTeamsQueryHandler(
            _dbContext,
            new TestingSystemCalendar(new TestingDateTimeProvider(new FakeClock(Now))));
    }

    [Fact]
    public async Task Handle_ParentMembershipStartingOnTheUtcDay_IsNotShownUntilThatDayInTheSystemZone()
    {
        // Arrange
        var team = new TeamFaker(ActiveDate).Generate();
        var parent = new TeamOfTeamsFaker(ActiveDate).Generate();
        _dbContext.AddTeam(team);
        _dbContext.AddTeamOfTeams(parent);
        team.AddTeamMembership(parent, new MembershipDateRange(new LocalDate(2026, 6, 1), null), EventActor.System, Now).IsSuccess.Should().BeTrue();

        // Act
        var result = await _handler.Handle(new GetTeamsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle().Which.TeamOfTeams.Should().BeNull();
    }
}
