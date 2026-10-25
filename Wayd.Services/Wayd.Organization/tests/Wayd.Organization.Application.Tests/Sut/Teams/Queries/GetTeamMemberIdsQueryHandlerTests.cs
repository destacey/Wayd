using NodaTime;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.TestData;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Queries;

public sealed class GetTeamMemberIdsQueryHandlerTests : IDisposable
{
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 6, 1, 0, 0);

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly TeamFaker _teamFaker = new();
    private readonly EmployeeFaker _employeeFaker = new();

    private GetTeamMemberIdsQueryHandler CreateHandler() => new(_dbContext);

    private Employee NewEmployee()
    {
        var employee = _employeeFaker.Generate();
        _dbContext.AddEmployee(employee);
        return employee;
    }

    [Fact]
    public async Task Handle_ShouldListEachDirectMemberOnce_WhateverTheirRoles()
    {
        // Arrange
        var team = _teamFaker.Generate();
        _dbContext.AddTeam(team);
        var twoRoles = NewEmployee();
        var oneRole = NewEmployee();
        team.AddMember(twoRoles, [Guid.NewGuid()], EventActor.System, Timestamp);
        team.AddMember(twoRoles, [Guid.NewGuid()], EventActor.System, Timestamp);
        team.AddMember(oneRole, [Guid.NewGuid()], EventActor.System, Timestamp);

        // Act
        var result = await CreateHandler().Handle(new GetTeamMemberIdsQuery(team.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEquivalentTo([twoRoles.Id, oneRole.Id]);
    }

    [Fact]
    public async Task Handle_ShouldNotListAnotherTeamsMembers()
    {
        // Arrange
        var team = _teamFaker.Generate();
        var other = _teamFaker.Generate();
        _dbContext.AddTeam(team);
        _dbContext.AddTeam(other);
        other.AddMember(NewEmployee(), [Guid.NewGuid()], EventActor.System, Timestamp);

        // Act
        var result = await CreateHandler().Handle(new GetTeamMemberIdsQuery(team.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnNothing_ForATeamThatDoesNotExist()
    {
        // Act
        var result = await CreateHandler().Handle(new GetTeamMemberIdsQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    public void Dispose() => _dbContext.Dispose();
}
