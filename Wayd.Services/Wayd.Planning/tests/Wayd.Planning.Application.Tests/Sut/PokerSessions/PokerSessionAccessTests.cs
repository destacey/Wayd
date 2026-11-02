using Moq;
using NodaTime;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums;
using Wayd.Planning.Application.PokerSessions;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PokerSessions;

/// <summary>
/// Covers how the caller, or a user by id, is resolved into what a poker session decides access with.
/// </summary>
public class PokerSessionAccessTests : IDisposable
{
    private const string UserId = "user-1";
    private static readonly LocalDate Today = new(2026, 1, 15);

    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IUserService> _userService = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly PokerSessionAccess _access;

    private readonly Guid _employeeId = Guid.NewGuid();
    private readonly Guid _teamId = Guid.NewGuid();

    public PokerSessionAccessTests()
    {
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.Today).Returns(Today);

        _currentUser.Setup(u => u.GetUserId()).Returns(UserId);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);
        _userService.Setup(u => u.GetEmployeeIdAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);
        _userService.Setup(u => u.HasPermissionAsync(UserId, PokerSessionAccess.ViewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsWithMemberQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid>());

        _access = new PokerSessionAccess(_dbContext, _currentPrincipal.Object, _currentUser.Object, _userService.Object, _dispatcher.Object, dateTimeProvider.Object);
    }

    private void MemberOfTeam(Guid teamId) =>
        _dispatcher
            .Setup(d => d.Send(It.Is<GetTeamsWithMemberQuery>(q => q.EmployeeId == _employeeId && q.AsOf == Today && q.TeamIds.Contains(teamId)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { teamId });

    private PokerSession AddPrivateSession(Action<PokerSessionFaker>? configure = null)
    {
        var faker = new PokerSessionFaker().WithVisibility(Visibility.Private);
        configure?.Invoke(faker);
        var session = faker.Generate();
        _dbContext.AddPokerSession(session);
        return session;
    }

    [Fact]
    public async Task ResolveActor_ShouldCarryTheEmployeeUserAdministratorAndInvitedTeams()
    {
        // Arrange
        var session = AddPrivateSession(f => f.WithInvitedTeams(_teamId, Guid.NewGuid()));
        MemberOfTeam(_teamId);
        _currentPrincipal.Setup(p => p.HasPermission(PokerSessionAccess.AdministratorPermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var actor = await _access.ResolveActor(session, TestContext.Current.CancellationToken);

        // Assert
        actor.EmployeeId.Should().Be(_employeeId);
        actor.UserId.Should().Be(UserId);
        actor.IsAdministrator.Should().BeTrue();
        actor.TeamIds.Should().Equal(_teamId);
    }

    [Fact]
    public async Task ResolveActor_SessionWithoutTeamInvites_ShouldNotAskTheOrganization()
    {
        // Arrange
        var session = AddPrivateSession();

        // Act
        var actor = await _access.ResolveActor(session, TestContext.Current.CancellationToken);

        // Assert
        actor.TeamIds.Should().BeEmpty();
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResolveActor_WithoutAnEmployee_ShouldRefuse()
    {
        // Arrange
        var session = AddPrivateSession();
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var act = () => _access.ResolveActor(session, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task ResolveReader_ShouldResolveTeamsAmongThoseInvitedToPrivateSessions()
    {
        // Arrange
        AddPrivateSession(f => f.WithInvitedTeams(_teamId));
        _dbContext.AddPokerSession(new PokerSessionFaker().WithInvitedTeams(Guid.NewGuid()).Generate());
        MemberOfTeam(_teamId);

        // Act
        var reader = await _access.ResolveReader(TestContext.Current.CancellationToken);

        // Assert
        reader.EmployeeId.Should().Be(_employeeId);
        reader.TeamIds.Should().Equal(_teamId);
        _dispatcher.Verify(d => d.Send(It.Is<GetTeamsWithMemberQuery>(q => q.TeamIds.Count == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CanUserAccess_WithoutViewPermission_ShouldBeFalse()
    {
        // Arrange
        var session = new PokerSessionFaker().Generate();
        _dbContext.AddPokerSession(session);
        _userService.Setup(u => u.HasPermissionAsync(UserId, PokerSessionAccess.ViewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var canAccess = await _access.CanUserAccess(session.Id, UserId, TestContext.Current.CancellationToken);

        // Assert
        canAccess.Should().BeFalse();
    }

    [Fact]
    public async Task CanUserAccess_PublicSession_ShouldBeTrue()
    {
        // Arrange
        var session = new PokerSessionFaker().Generate();
        _dbContext.AddPokerSession(session);

        // Act
        var canAccess = await _access.CanUserAccess(session.Id, UserId, TestContext.Current.CancellationToken);

        // Assert
        canAccess.Should().BeTrue();
    }

    [Fact]
    public async Task CanUserAccess_UnknownSession_ShouldBeFalse()
    {
        // Act
        var canAccess = await _access.CanUserAccess(Guid.NewGuid(), UserId, TestContext.Current.CancellationToken);

        // Assert
        canAccess.Should().BeFalse();
    }

    [Fact]
    public async Task CanUserAccess_PrivateSession_ShouldAdmitInviteesTeamMembersAndAdministratorsOnly()
    {
        // Arrange
        var invited = AddPrivateSession(f => f.WithInvitedEmployees(_employeeId));
        var throughTeam = AddPrivateSession(f => f.WithInvitedTeams(_teamId));
        var closed = AddPrivateSession(f => f.WithInvitedTeams(Guid.NewGuid()));
        MemberOfTeam(_teamId);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act & Assert
        (await _access.CanUserAccess(invited.Id, UserId, cancellationToken)).Should().BeTrue();
        (await _access.CanUserAccess(throughTeam.Id, UserId, cancellationToken)).Should().BeTrue();
        (await _access.CanUserAccess(closed.Id, UserId, cancellationToken)).Should().BeFalse();

        _userService.Setup(u => u.HasPermissionAsync(UserId, PokerSessionAccess.AdministratorPermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        (await _access.CanUserAccess(closed.Id, UserId, cancellationToken)).Should().BeTrue();
    }

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, false, true)]
    [InlineData(true, true, true, false)]
    public void Reader_CanManage_ShouldAdmitFacilitatorsAndAdministratorsUnlessReadOnly(bool isFacilitator, bool isAdministrator, bool isReadOnly, bool expected)
    {
        // Arrange
        var reader = new PokerSessionReader(_employeeId, isAdministrator, [], isReadOnly);
        Guid[] facilitatorIds = isFacilitator ? [_employeeId] : [Guid.NewGuid()];

        // Act
        var canManage = reader.CanManage(facilitatorIds);

        // Assert
        canManage.Should().Be(expected);
    }

    [Fact]
    public void Reader_CanManage_AdministratorWithNoEmployee_ShouldBeFalse()
    {
        // Arrange — every poker command needs a linked employee, so offering the controls would only fail.
        var reader = new PokerSessionReader(null, true, [], false);

        // Act
        var canManage = reader.CanManage([Guid.NewGuid()]);

        // Assert
        canManage.Should().BeFalse();
    }

    public void Dispose() => _dbContext.Dispose();
}
