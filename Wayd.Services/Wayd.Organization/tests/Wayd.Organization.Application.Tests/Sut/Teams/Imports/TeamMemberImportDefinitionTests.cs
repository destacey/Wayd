using FluentAssertions;
using Wayd.Common.Application.Imports;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Organization.Application.Teams.Dtos;
using Wayd.Organization.Application.Teams.Imports;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.TestData;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Imports;

public sealed class TeamMemberImportDefinitionTests : IDisposable
{
    private const int AddPass = 0;

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly TeamMemberImportDefinition _definition;

    public TeamMemberImportDefinitionTests()
    {
        _definition = new TeamMemberImportDefinition(_dbContext, new ImportPayloadSerializer());
    }

    public void Dispose() => _dbContext.Dispose();

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

    private ImportProcessRow[] Rows(params ImportTeamMemberDto[] members) =>
        [.. members.Select((m, i) => ImportProcessRow.Create($"r{i + 1}", i + 1, _definition.SerializeRow(m)))];

    private Task<CSharpFunctionalExtensions.Result<ImportPassResult>> Run(params ImportTeamMemberDto[] members) =>
        _definition.ExecutePass(
            Guid.CreateVersion7(), AddPass, Rows(members), isFinalChunk: true, TestContext.Current.CancellationToken);

    [Fact]
    public void Definition_IsAtomicAndCannotBeChunked()
    {
        // Arrange & Act
        var passes = _definition.Passes;

        // Assert — rows are not independent: several roles for one person on one team collapse into a
        // single AddMember, so a chunk boundary between them would try to add them twice
        _definition.Atomicity.Should().Be(ImportAtomicity.Atomic);
        passes.Single().Scope.Should().Be(ImportPassScope.WholeSet);
    }

    [Fact]
    public async Task AddMembers_StaffsATeamResolvingEveryNaturalKey()
    {
        // Arrange
        var team = SeedTeam("PAY");
        SeedEmployee("E-1001");
        var engineer = SeedRole("Engineer");

        // Act
        var result = await Run(new ImportTeamMemberDto("PAY", "E-1001", "Engineer"));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        team.Members.Where(m => !m.IsDeleted).Single().RoleId.Should().Be(engineer.Id);
    }

    [Fact]
    public async Task AddMembers_GivesOnePersonEveryRoleTheFileGivesThem()
    {
        // Arrange — the same employee on the same team appears once per role, and the rows are grouped
        // into one AddMember carrying both
        var team = SeedTeam("PAY");
        SeedEmployee("E-1001");
        var engineer = SeedRole("Engineer");
        var lead = SeedRole("Tech Lead");

        // Act
        var result = await Run(
            new ImportTeamMemberDto("PAY", "E-1001", "Engineer"),
            new ImportTeamMemberDto("PAY", "E-1001", "Tech Lead"));

        // Assert — both rows applied, and the domain stored one member row per role
        result.Value.Rows.Should().AllSatisfy(r => r.Failed.Should().BeFalse());

        var activeMembers = team.Members.Where(m => !m.IsDeleted).ToList();
        activeMembers.Select(m => m.RoleId).Should().BeEquivalentTo([engineer.Id, lead.Id]);
    }

    [Fact]
    public async Task AddMembers_RejectsARowNamingATeamThatDoesNotExist()
    {
        // Arrange
        SeedEmployee("E-1001");
        SeedRole("Engineer");

        // Act
        var result = await Run(new ImportTeamMemberDto("MISSING", "E-1001", "Engineer"));

        // Assert — named per row, so the person knows which line to fix
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("MISSING");
    }

    [Fact]
    public async Task AddMembers_RejectsARowNamingAnEmployeeThatDoesNotExist()
    {
        // Arrange
        SeedTeam("PAY");
        SeedRole("Engineer");

        // Act
        var result = await Run(new ImportTeamMemberDto("PAY", "E-9999", "Engineer"));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("E-9999");
    }

    [Fact]
    public async Task AddMembers_RejectsARowNamingARoleThatDoesNotExist()
    {
        // Arrange — staffing does not create roles, and the message says so
        SeedTeam("PAY");
        SeedEmployee("E-1001");

        // Act
        var result = await Run(new ImportTeamMemberDto("PAY", "E-1001", "Archmage"));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("Archmage").And.Contain("not created");
    }

    [Fact]
    public async Task AddMembers_KeepsTheGoodRowsWhenOneIsRejected()
    {
        // Arrange — the pass reports per row; the runner is what turns a rejection into a failed run,
        // because this import is atomic
        SeedTeam("PAY");
        SeedEmployee("E-1001");
        SeedRole("Engineer");

        // Act
        var result = await Run(
            new ImportTeamMemberDto("PAY", "E-1001", "Engineer"),
            new ImportTeamMemberDto("MISSING", "E-1001", "Engineer"));

        // Assert
        result.Value.Rows.Single(r => r.ImportId == "r1").Failed.Should().BeFalse();
        result.Value.Rows.Single(r => r.ImportId == "r2").Failed.Should().BeTrue();
    }
}
