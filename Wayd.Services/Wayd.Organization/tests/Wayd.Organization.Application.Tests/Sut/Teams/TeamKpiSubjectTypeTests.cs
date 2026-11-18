using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Organization.Application.Teams;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.Organization.Application.Tests.Sut.Teams;

public sealed class TeamKpiSubjectTypeTests : IDisposable
{
    private static readonly LocalDate ActiveDate = new(2024, 1, 1);
    private static readonly Instant Timestamp = Instant.FromUtc(2024, 1, 1, 0, 0);

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 11, 16, 9, 30)));
    private readonly TeamFaker _teamFaker = new();
    private readonly TeamOfTeamsFaker _teamOfTeamsFaker = new();
    private readonly EmployeeFaker _employeeFaker = new();

    public TeamKpiSubjectTypeTests()
    {
        _currentPrincipal.Setup(p => p.HoldsPermission(TeamKpiSubjectType.ViewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    public void Dispose() => _dbContext.Dispose();

    private TeamKpiSubjectType CreateSut() => new(_dbContext, _currentPrincipal.Object, _dateTimeProvider);

    private Employee SignedIn()
    {
        var employee = _employeeFaker.Generate();
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(employee.Id);
        return employee;
    }

    /// <summary>A team inside a team of teams inside another, all joined from the active date with no end.</summary>
    private (Team Team, TeamOfTeams Parent, TeamOfTeams Grandparent) ThreeLevels()
    {
        var grandparent = _teamOfTeamsFaker.WithActiveDate(ActiveDate).Generate();
        var parent = _teamOfTeamsFaker.WithActiveDate(ActiveDate).Generate();
        var team = _teamFaker.WithActiveDate(ActiveDate).Generate();
        parent.AddTeamMembership(grandparent, new MembershipDateRange(ActiveDate, null), EventActor.System, Timestamp).IsSuccess.Should().BeTrue();
        team.AddTeamMembership(parent, new MembershipDateRange(ActiveDate, null), EventActor.System, Timestamp).IsSuccess.Should().BeTrue();
        _dbContext.AddTeamOfTeams(grandparent);
        _dbContext.AddTeamOfTeams(parent);
        _dbContext.AddTeam(team);
        return (team, parent, grandparent);
    }

    [Fact]
    public async Task Resolve_ShouldNameTheTeam_AndLetAMemberManage()
    {
        // Arrange
        var (team, _, _) = ThreeLevels();
        var employee = SignedIn();
        team.AddMember(employee, [Guid.NewGuid()], EventActor.System, Timestamp).IsSuccess.Should().BeTrue();

        // Act
        var access = await CreateSut().Resolve(team.Id, TestContext.Current.CancellationToken);

        // Assert
        access.Should().NotBeNull();
        access!.Name.Should().Be(team.Name);
        access.Code.Should().Be(team.Code.Value);
        access.CanManage.Should().BeTrue();
    }

    [Fact]
    public async Task Resolve_ShouldLetAMemberOfATeamOfTeamsTwoLevelsUpManage()
    {
        // Arrange
        var (team, _, grandparent) = ThreeLevels();
        var employee = SignedIn();
        grandparent.AddMember(employee, [Guid.NewGuid()], EventActor.System, Timestamp).IsSuccess.Should().BeTrue();

        // Act
        var access = await CreateSut().Resolve(team.Id, TestContext.Current.CancellationToken);

        // Assert
        access!.CanManage.Should().BeTrue();
    }

    [Fact]
    public async Task Resolve_ShouldNotLetAMemberOfATeamBelowManageItsParent()
    {
        // Arrange
        var (team, parent, _) = ThreeLevels();
        var employee = SignedIn();
        team.AddMember(employee, [Guid.NewGuid()], EventActor.System, Timestamp).IsSuccess.Should().BeTrue();

        // Act
        var access = await CreateSut().Resolve(parent.Id, TestContext.Current.CancellationToken);

        // Assert
        access!.CanManage.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_ShouldIgnoreATeamOfTeamsTheTeamHasLeft()
    {
        // Arrange
        var former = _teamOfTeamsFaker.WithActiveDate(ActiveDate).Generate();
        var team = _teamFaker.WithActiveDate(ActiveDate).Generate();
        team.AddTeamMembership(former, new MembershipDateRange(ActiveDate, new LocalDate(2025, 12, 31)), EventActor.System, Timestamp).IsSuccess.Should().BeTrue();
        _dbContext.AddTeamOfTeams(former);
        _dbContext.AddTeam(team);
        var employee = SignedIn();
        former.AddMember(employee, [Guid.NewGuid()], EventActor.System, Timestamp).IsSuccess.Should().BeTrue();

        // Act
        var access = await CreateSut().Resolve(team.Id, TestContext.Current.CancellationToken);

        // Assert
        access!.CanManage.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_ShouldNotLetACallerWithNoLinkedEmployeeManage()
    {
        // Arrange
        var (team, _, _) = ThreeLevels();
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var access = await CreateSut().Resolve(team.Id, TestContext.Current.CancellationToken);

        // Assert
        access.Should().NotBeNull();
        access!.CanManage.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_ShouldBeNull_WithoutTheTeamsViewPermission()
    {
        // Arrange
        var (team, _, _) = ThreeLevels();
        var employee = SignedIn();
        team.AddMember(employee, [Guid.NewGuid()], EventActor.System, Timestamp).IsSuccess.Should().BeTrue();
        _currentPrincipal.Setup(p => p.HoldsPermission(TeamKpiSubjectType.ViewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var access = await CreateSut().Resolve(team.Id, TestContext.Current.CancellationToken);

        // Assert
        access.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_ShouldBeNull_ForAnUnknownTeam()
    {
        // Arrange
        ThreeLevels();
        SignedIn();

        // Act
        var access = await CreateSut().Resolve(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        access.Should().BeNull();
    }
}
