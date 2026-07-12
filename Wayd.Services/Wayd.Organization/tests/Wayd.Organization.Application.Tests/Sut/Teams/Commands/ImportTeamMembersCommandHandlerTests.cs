using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Organization.Application.Teams.Dtos;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.TestData;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Commands;

public class ImportTeamMembersCommandHandlerTests : IDisposable
{
    private readonly FakeOrganizationDbContext _dbContext = new();

    private ImportTeamMembersCommandHandler CreateHandler() =>
        new(_dbContext, NullLogger<ImportTeamMembersCommandHandler>.Instance);

    private Team SeedTeam(string code)
    {
        var team = new TeamFaker().WithCode(new TeamCode(code)).Generate();
        _dbContext.AddTeam(team);
        return team;
    }

    private Employee SeedEmployee(string employeeNumber)
    {
        var employee = new EmployeeFaker().WithEmployeeNumber(employeeNumber).Generate();
        _dbContext.AddEmployee(employee);
        return employee;
    }

    private TeamMemberRole SeedRole(string name)
    {
        var role = new TeamMemberRoleFaker().WithName(name).Generate();
        _dbContext.AddTeamMemberRole(role);
        return role;
    }

    [Fact]
    public async Task Handle_AddsMember_ResolvingNaturalKeys()
    {
        // Arrange
        var team = SeedTeam("PAY");
        var employee = SeedEmployee("E-1001");
        var role = SeedRole("Engineer");

        var command = new ImportTeamMembersCommand([new ImportTeamMemberDto("pay", "E-1001", "Engineer")]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        team.Members.Should().ContainSingle();
        var member = team.Members.Single();
        member.EmployeeId.Should().Be(employee.Id);
        member.RoleId.Should().Be(role.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_GroupsMultipleRoleRows_IntoOneMemberWithAllRoles()
    {
        // Arrange — same employee on the same team appears once per role.
        var team = SeedTeam("PAY");
        SeedEmployee("E-1001");
        var engineer = SeedRole("Engineer");
        var lead = SeedRole("Tech Lead");

        var command = new ImportTeamMembersCommand(
        [
            new ImportTeamMemberDto("PAY", "E-1001", "Engineer"),
            new ImportTeamMemberDto("PAY", "E-1001", "Tech Lead"),
        ]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var activeMembers = team.Members.Where(m => !m.IsDeleted).ToList();
        activeMembers.Should().HaveCount(2);
        activeMembers.Select(m => m.RoleId).Should().BeEquivalentTo([engineer.Id, lead.Id]);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Fails_WhenTeamCodeUnresolved()
    {
        // Arrange
        SeedEmployee("E-1001");
        SeedRole("Engineer");

        var command = new ImportTeamMembersCommand([new ImportTeamMemberDto("NOPE", "E-1001", "Engineer")]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("team code 'NOPE'");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Fails_WhenEmployeeNumberUnresolved()
    {
        // Arrange
        SeedTeam("PAY");
        SeedRole("Engineer");

        var command = new ImportTeamMembersCommand([new ImportTeamMemberDto("PAY", "E-NOPE", "Engineer")]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("employee number 'E-NOPE'");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Fails_WhenRoleUnresolved()
    {
        // Arrange
        SeedTeam("PAY");
        SeedEmployee("E-1001");

        var command = new ImportTeamMembersCommand([new ImportTeamMemberDto("PAY", "E-1001", "Ghost Role")]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("role 'Ghost Role'");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
