using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Application.Programs;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Programs;

public sealed class ProgramReadRuleTests
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();

    public ProgramReadRuleTests() => GrantView(true);

    private ProgramReadRule CreateSut() => new(_dbContext, _currentPrincipal.Object);

    private void GrantView(bool granted) =>
        _currentPrincipal
            .Setup(p => p.HasPermission("Permissions.Programs.View", It.IsAny<CancellationToken>()))
            .ReturnsAsync(granted);

    [Fact]
    public void OwnerType_ShouldBeTheRegisteredProgramKey()
    {
        // Act
        var ownerType = CreateSut().OwnerType;

        // Assert
        // Persisted on every follow of a program, so a change here orphans them all.
        ownerType.Should().Be("ppm.program");
    }

    [Fact]
    public async Task Readable_ShouldBeNull_WhenTheCallerCannotViewPrograms()
    {
        // Arrange
        GrantView(false);
        _dbContext.AddPrograms(new ProgramFaker().Generate(2));

        // Act
        var readable = await CreateSut().Readable(null, TestContext.Current.CancellationToken);

        // Assert
        readable.Should().BeNull();
    }

    [Fact]
    public async Task Readable_ShouldFlagClosedPrograms_AndNeverAnUnhealthyOne()
    {
        // Arrange
        var completed = new ProgramFaker().WithStatus(ProgramStatus.Completed).Generate();
        var canceled = new ProgramFaker().WithStatus(ProgramStatus.Canceled).Generate();
        var active = new ProgramFaker().WithStatus(ProgramStatus.Active).Generate();
        _dbContext.AddPrograms([completed, canceled, active]);

        // Act
        var readable = await CreateSut().Readable(null, TestContext.Current.CancellationToken);

        // Assert
        var facts = readable!.ToList();
        facts.Should().HaveCount(3);
        facts.Where(f => f.IsClosed).Select(f => f.Id).Should().BeEquivalentTo([completed.Id, canceled.Id]);
        facts.Should().OnlyContain(f => !f.IsUnhealthy, "programs record no health");
    }

    [Theory]
    [InlineData("Bullhorn")]
    [InlineData("7")]
    public async Task Readable_ShouldMatchASearchByNameOrExactKey(string search)
    {
        // Arrange
        var match = new ProgramFaker().WithKey(7).WithName("Bullhorn Platform").Generate();
        _dbContext.AddPrograms([match, new ProgramFaker().WithKey(77).WithName("Payments").Generate()]);

        // Act
        var readable = await CreateSut().Readable(search, TestContext.Current.CancellationToken);

        // Assert
        readable!.Select(f => f.Id).Should().Equal(match.Id);
    }

    [Fact]
    public async Task Describe_ShouldDescribeEachRequestedProgram_AndOmitOnesThatDoNotExist()
    {
        // Arrange
        var owner = new EmployeeFaker().WithName(new PersonName("Sofia", null, "Marchetti")).Generate();
        var program = new ProgramFaker()
            .WithKey(42)
            .WithName("Mercury")
            .WithStatus(ProgramStatus.Proposed)
            .WithDateRange(new LocalDateRange(new LocalDate(2026, 1, 1), new LocalDate(2026, 12, 31)))
            .Generate();
        var managers = new[]
        {
            new EmployeeFaker().WithName(new PersonName("Yusuf", null, "Young")).Generate(),
            new EmployeeFaker().WithName(new PersonName("Aaron", null, "Abbott")).Generate(),
        };
        var sponsor = new EmployeeFaker().Generate();
        RoleAssignment<ProgramRole> Assign(ProgramRole roleType, Wayd.Common.Domain.Employees.Employee employee)
        {
            var role = new RoleAssignmentFaker<ProgramRole>().WithObjectId(program.Id).WithRole(roleType).WithEmployeeId(employee.Id).Generate();
            role.Employee = employee;
            return role;
        }
        program.SetPrivateField("_roles", new HashSet<RoleAssignment<ProgramRole>>
        {
            Assign(ProgramRole.Manager, managers[0]),
            Assign(ProgramRole.Sponsor, sponsor),
            Assign(ProgramRole.Owner, owner),
            Assign(ProgramRole.Manager, managers[1]),
        });
        _dbContext.AddPrograms([program, new ProgramFaker().Generate()]);

        // Act
        var summaries = await CreateSut().Describe([program.Id, Guid.NewGuid()], TestContext.Current.CancellationToken);

        // Assert
        var summary = summaries.Should().ContainSingle().Subject.Value;
        summary.Key.Should().Be("42");
        summary.Name.Should().Be("Mercury");
        summary.StatusName.Should().Be("Proposed");
        summary.Lifecycle.Should().Be(LifecycleCategory.NotStarted);
        summary.Health.Should().BeNull();
        // Owners first, then every manager by name; the sponsor is left out.
        summary.People.Should().Equal(
            new RecordPerson("Program Owner", owner.Name.DisplayName, owner.Id),
            new RecordPerson("Program Manager", managers[1].Name.DisplayName, managers[1].Id),
            new RecordPerson("Program Manager", managers[0].Name.DisplayName, managers[0].Id));
        summary.Start.Should().Be(new LocalDate(2026, 1, 1));
        summary.End.Should().Be(new LocalDate(2026, 12, 31));
    }
}
