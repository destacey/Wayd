using FluentAssertions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Application.Projects;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects;

public sealed class ProjectReadRuleTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 24, 9, 0);

    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Now));
    private readonly Guid _ownerId = Guid.NewGuid();

    public ProjectReadRuleTests() => GrantView(true);

    private ProjectReadRule CreateSut() => new(_dbContext, _currentPrincipal.Object, _dateTimeProvider);

    private void GrantView(bool granted) =>
        _currentPrincipal
            .Setup(p => p.HasPermission("Permissions.Projects.View", It.IsAny<CancellationToken>()))
            .ReturnsAsync(granted);

    private ProjectFaker Faker() => new ProjectFaker().WithRoles(new() { [ProjectRole.Owner] = [_ownerId] });

    private Project WithHealth(Project project, HealthStatus status, Duration expiresIn)
    {
        project.AddHealthCheck(
            status,
            new PpmActor(_ownerId, IsPpmAdministrator: false, Guid.NewGuid().ToString()),
            ProjectAncestryRoles.None,
            Now.Plus(expiresIn),
            null,
            Now.Minus(Duration.FromHours(1)));
        return project;
    }

    [Fact]
    public void OwnerType_ShouldBeTheRegisteredProjectKey()
    {
        // Act
        var ownerType = CreateSut().OwnerType;

        // Assert
        // Persisted on every follow of a project, so a change here orphans them all.
        ownerType.Should().Be("ppm.project");
    }

    [Fact]
    public async Task Readable_ShouldBeNull_WhenTheCallerCannotViewProjects()
    {
        // Arrange
        GrantView(false);
        _dbContext.AddProjects(Faker().Generate(2));

        // Act
        var readable = await CreateSut().Readable(null, TestContext.Current.CancellationToken);

        // Assert
        readable.Should().BeNull();
    }

    [Fact]
    public async Task Readable_ShouldFlagEachProjectThatIsUnhealthyOrClosed()
    {
        // Arrange
        var red = WithHealth(Faker().Generate(), HealthStatus.Unhealthy, Duration.FromDays(7));
        var expiredRed = WithHealth(Faker().Generate(), HealthStatus.Unhealthy, Duration.FromMinutes(-1));
        var atRisk = WithHealth(Faker().Generate(), HealthStatus.AtRisk, Duration.FromDays(7));
        var completed = Faker().WithStatus(ProjectStatus.Completed).Generate();
        var canceled = Faker().WithStatus(ProjectStatus.Canceled).Generate();
        var active = Faker().WithStatus(ProjectStatus.Active).Generate();
        _dbContext.AddProjects([red, expiredRed, atRisk, completed, canceled, active]);

        // Act
        var readable = await CreateSut().Readable(null, TestContext.Current.CancellationToken);

        // Assert
        var facts = readable!.ToDictionary(f => f.Id);
        facts.Should().HaveCount(6);
        facts.Where(f => f.Value.IsUnhealthy).Select(f => f.Key).Should().Equal(red.Id);
        facts.Where(f => f.Value.IsClosed).Select(f => f.Key).Should().BeEquivalentTo([completed.Id, canceled.Id]);
    }

    [Theory]
    [InlineData("Dashboard")]
    [InlineData("compdb")]
    // Name matching is case-insensitive in SQL Server (the database collation); in memory it is not, so these
    // use the stored casing. Key matching is case-insensitive in both: the key is upper-cased before comparing.
    public async Task Readable_ShouldMatchASearchByNameOrExactKey(string search)
    {
        // Arrange
        var match = Faker().WithKey(new ProjectKey("COMPDB")).WithName("Compliance Dashboard").Generate();
        _dbContext.AddProjects([match, Faker().WithKey(new ProjectKey("OTHER")).WithName("Payments").Generate()]);

        // Act
        var readable = await CreateSut().Readable(search, TestContext.Current.CancellationToken);

        // Assert
        readable!.Select(f => f.Id).Should().Equal(match.Id);
    }

    [Theory]
    [InlineData("COMP")]
    [InlineData("no match!")]
    public async Task Readable_ShouldNotMatchAPartialKeyOrUnknownText(string search)
    {
        // Arrange
        _dbContext.AddProjects([Faker().WithKey(new ProjectKey("COMPDB")).WithName("Compliance Dashboard").Generate()]);

        // Act
        var readable = await CreateSut().Readable(search, TestContext.Current.CancellationToken);

        // Assert
        readable!.Should().BeEmpty();
    }

    [Fact]
    public async Task Describe_ShouldDescribeEachRequestedProject_AndOmitOnesThatDoNotExist()
    {
        // Arrange
        var owner = new EmployeeFaker().WithName(new PersonName("Sofia", null, "Marchetti")).Generate();
        var manager = new EmployeeFaker().WithName(new PersonName("Aaron", null, "Abbott")).Generate();
        var sponsor = new EmployeeFaker().WithName(new PersonName("Zoe", null, "Zimmer")).Generate();
        var managerId = Guid.NewGuid();
        var sponsorId = Guid.NewGuid();
        var project = WithHealth(
            new ProjectFaker()
                .WithRoles(new()
                {
                    [ProjectRole.Owner] = [_ownerId],
                    [ProjectRole.Manager] = [managerId],
                    [ProjectRole.Sponsor] = [sponsorId],
                })
                .WithKey(new ProjectKey("COMPDB"))
                .WithName("Compliance Dashboard")
                .WithStatus(ProjectStatus.Active)
                .WithDateRange(new LocalDateRange(new LocalDate(2026, 10, 1), new LocalDate(2027, 1, 30)))
                .Generate(),
            HealthStatus.Healthy,
            Duration.FromDays(7));
        foreach (var role in project.Roles)
        {
            role.Employee = role.EmployeeId == _ownerId ? owner : role.EmployeeId == managerId ? manager : sponsor;
        }
        _dbContext.AddProjects([project, Faker().Generate()]);

        // Act
        var summaries = await CreateSut().Describe([project.Id, Guid.NewGuid()], TestContext.Current.CancellationToken);

        // Assert
        var summary = summaries.Should().ContainSingle().Subject.Value;
        summary.Key.Should().Be("COMPDB");
        summary.Name.Should().Be("Compliance Dashboard");
        summary.StatusName.Should().Be("Active");
        summary.Lifecycle.Should().Be(LifecycleCategory.Active);
        summary.Health.Should().Be(HealthStatus.Healthy);
        // Owners before managers whatever the names; sponsors and members are left out.
        summary.People.Should().Equal(
            new RecordPerson("Project Owner", owner.Name.DisplayName, _ownerId),
            new RecordPerson("Project Manager", manager.Name.DisplayName, managerId));
        summary.Start.Should().Be(new LocalDate(2026, 10, 1));
        summary.End.Should().Be(new LocalDate(2027, 1, 30));
        summary.Progress.Should().BeNull("projects show their stages, not a progress figure");
    }
}
