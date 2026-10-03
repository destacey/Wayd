using NodaTime;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.TestData;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Queries;

public class IsTeamMemberQueryHandlerTests : IDisposable
{
    private static readonly LocalDate ActiveDate = new(2024, 1, 1);
    private static readonly LocalDate JoinDate = new(2024, 3, 1);
    private static readonly LocalDate LeaveDate = new(2024, 12, 31);
    private static readonly Instant Timestamp = Instant.FromUtc(2024, 1, 1, 0, 0);

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly IsTeamMemberQueryHandler _handler;
    private readonly TeamFaker _teamFaker = new();
    private readonly TeamOfTeamsFaker _teamOfTeamsFaker = new();
    private readonly EmployeeFaker _employeeFaker = new();

    public IsTeamMemberQueryHandlerTests()
    {
        _handler = new IsTeamMemberQueryHandler(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    private (Team Team, TeamOfTeams Parent) TeamInAParent()
    {
        var parent = _teamOfTeamsFaker.WithActiveDate(ActiveDate).Generate();
        var team = _teamFaker.WithActiveDate(ActiveDate).Generate();
        team.AddTeamMembership(parent, new MembershipDateRange(JoinDate, LeaveDate), EventActor.System, Timestamp).IsSuccess.Should().BeTrue();
        _dbContext.AddTeamOfTeams(parent);
        _dbContext.AddTeam(team);
        return (team, parent);
    }

    [Fact]
    public async Task Handle_WhenTheEmployeeIsOnTheTeam_ReturnsTrue()
    {
        // Arrange
        var (team, _) = TeamInAParent();
        var employee = _employeeFaker.Generate();
        team.AddMember(employee, [Guid.NewGuid()], EventActor.System, Timestamp).IsSuccess.Should().BeTrue();

        // Act
        var result = await _handler.Handle(new IsTeamMemberQuery(team.Id, employee.Id, JoinDate), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenTheEmployeeIsOnTheParentThatDay_ReturnsTrue()
    {
        // Arrange
        var (team, parent) = TeamInAParent();
        var employee = _employeeFaker.Generate();
        parent.AddMember(employee, [Guid.NewGuid()], EventActor.System, Timestamp).IsSuccess.Should().BeTrue();

        // Act
        var result = await _handler.Handle(new IsTeamMemberQuery(team.Id, employee.Id, JoinDate), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenTheTeamHadLeftTheParentThatDay_ReturnsFalse()
    {
        // Arrange
        var (team, parent) = TeamInAParent();
        var employee = _employeeFaker.Generate();
        parent.AddMember(employee, [Guid.NewGuid()], EventActor.System, Timestamp).IsSuccess.Should().BeTrue();

        // Act
        var result = await _handler.Handle(new IsTeamMemberQuery(team.Id, employee.Id, LeaveDate.PlusDays(1)), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenTheEmployeeIsOnNeither_ReturnsFalse()
    {
        // Arrange
        var (team, _) = TeamInAParent();

        // Act
        var result = await _handler.Handle(new IsTeamMemberQuery(team.Id, Guid.NewGuid(), JoinDate), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeFalse();
    }
}
