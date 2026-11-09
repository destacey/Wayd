using NodaTime;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.Domain.Tests.Data;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Queries;

public sealed class GetEmployeeTeamInvolvementQueryHandlerTests
{
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 11, 1, 0, 0);

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly TeamMemberRole _developer = TeamMemberRole.Create("Developer", null).Value;
    private readonly TeamMemberRole _lead = TeamMemberRole.Create("Tech Lead", null).Value;

    private GetEmployeeTeamInvolvementQueryHandler CreateSut() => new(_dbContext);

    private void Join(BaseTeam team, Employee employee, params TeamMemberRole[] roles)
    {
        team.AddMember(employee, [.. roles.Select(r => r.Id)], EventActor.System, Timestamp);
        foreach (var member in team.Members.Where(m => m.EmployeeId == employee.Id))
        {
            member.SetPrivate(m => m.Team, team);
            _dbContext.AddTeamMember(member);
        }
    }

    [Fact]
    public async Task Handle_CountsEachTeamOnce_ByType()
    {
        // Arrange
        var employee = new EmployeeFaker().Generate();
        var payments = new TeamFaker().Generate();
        var checkout = new TeamFaker().Generate();
        var commerce = new TeamOfTeamsFaker().Generate();
        Join(payments, employee, _developer, _lead);
        Join(checkout, employee, _developer);
        Join(commerce, employee, _lead);
        Join(new TeamFaker().Generate(), new EmployeeFaker().Generate(), _developer);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetEmployeeTeamInvolvementQuery(employee.Id), TestContext.Current.CancellationToken);

        // Assert
        result.TeamCount.Should().Be(2);
        result.TeamOfTeamsCount.Should().Be(1);
        result.TeamIds.Should().BeEquivalentTo([payments.Id, checkout.Id, commerce.Id]);
    }

    [Fact]
    public async Task Handle_LeavesOutInactiveTeams()
    {
        // Arrange
        var employee = new EmployeeFaker().Generate();
        var retired = new TeamFaker().Generate();
        Join(retired, employee, _developer);
        retired.SetPrivate(t => t.IsActive, false);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetEmployeeTeamInvolvementQuery(employee.Id), TestContext.Current.CancellationToken);

        // Assert
        result.TeamCount.Should().Be(0);
        result.TeamIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_EmployeeOnNoTeam_ReturnsEmptyInvolvement()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetEmployeeTeamInvolvementQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.TeamCount.Should().Be(0);
        result.TeamOfTeamsCount.Should().Be(0);
        result.TeamIds.Should().BeEmpty();
    }
}
