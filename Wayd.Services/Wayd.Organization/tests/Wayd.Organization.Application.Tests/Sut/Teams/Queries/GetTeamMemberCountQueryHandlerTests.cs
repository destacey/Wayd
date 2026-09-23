using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.TestData;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Queries;

public class GetTeamMemberCountQueryHandlerTests : IDisposable
{
    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly GetTeamMemberCountQueryHandler _handler;
    private readonly TeamFaker _teamFaker = new();
    private readonly EmployeeFaker _employeeFaker = new();

    public GetTeamMemberCountQueryHandlerTests()
    {
        _handler = new GetTeamMemberCountQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_CountsEachEmployeeOnceWhateverTheirRoles()
    {
        // Arrange
        var team = _teamFaker.Generate();
        _dbContext.AddTeam(team);
        var twoRoles = _employeeFaker.Generate();
        team.AddMember(twoRoles, Guid.NewGuid());
        team.AddMember(twoRoles, Guid.NewGuid());
        team.AddMember(_employeeFaker.Generate(), Guid.NewGuid());

        // Act
        var result = await _handler.Handle(new GetTeamMemberCountQuery(team.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(2);
    }

    [Fact]
    public async Task Handle_TeamWithNoMembers_ReturnsZero()
    {
        // Arrange
        var team = _teamFaker.Generate();
        _dbContext.AddTeam(team);

        // Act
        var result = await _handler.Handle(new GetTeamMemberCountQuery(team.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task Handle_UnknownTeam_ReturnsNull()
    {
        // Act
        var result = await _handler.Handle(new GetTeamMemberCountQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
