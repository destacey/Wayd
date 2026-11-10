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

public sealed class GetEmployeeActiveTeamsQueryHandlerTests
{
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 11, 1, 0, 0);

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly TeamMemberRole _developer = TeamMemberRole.Create("Developer", null).Value;
    private readonly TeamMemberRole _lead = TeamMemberRole.Create("Tech Lead", null).Value;

    private GetEmployeeActiveTeamsQueryHandler CreateSut() => new(_dbContext);

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
    public async Task Handle_ListsEachActiveTeamOnce_ByName_LeavingOutTeamsOfTeams()
    {
        // Arrange
        var employee = new EmployeeFaker().Generate();
        var payments = new TeamFaker().WithName("Payments").Generate();
        var checkout = new TeamFaker().WithName("checkout").Generate();
        var retired = new TeamFaker().WithName("Retired").Generate();
        Join(payments, employee, _developer, _lead);
        Join(checkout, employee, _developer);
        Join(retired, employee, _developer);
        retired.SetPrivate(t => t.IsActive, false);
        Join(new TeamOfTeamsFaker().Generate(), employee, _lead);
        Join(new TeamFaker().Generate(), new EmployeeFaker().Generate(), _developer);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetEmployeeActiveTeamsQuery(employee.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Select(t => t.Id).Should().Equal(checkout.Id, payments.Id);
        result[1].Code.Should().Be(payments.Code.Value);
        result[1].Type.Should().Be("Team");
    }

    [Fact]
    public async Task Handle_EmployeeOnNoTeam_ReturnsNone()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetEmployeeActiveTeamsQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }
}
