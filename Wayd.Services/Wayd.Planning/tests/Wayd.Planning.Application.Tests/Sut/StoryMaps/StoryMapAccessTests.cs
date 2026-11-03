using Moq;
using NodaTime;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums;
using Wayd.Planning.Application.StoryMaps;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps;

/// <summary>
/// Covers how the caller, or a user by id, is resolved into what a story map decides access with.
/// </summary>
public class StoryMapAccessTests : IDisposable
{
    private const string UserId = "user-1";
    private static readonly LocalDate Today = new(2026, 1, 15);

    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Mock<IUserService> _userService = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly StoryMapAccess _access;

    private readonly Guid _employeeId = Guid.NewGuid();
    private readonly Guid _teamId = Guid.NewGuid();

    public StoryMapAccessTests()
    {
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.Today).Returns(Today);

        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);
        _userService.Setup(u => u.GetEmployeeIdAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);
        _userService.Setup(u => u.HasPermissionAsync(UserId, StoryMapAccess.ViewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsWithMemberQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid>());

        _access = new StoryMapAccess(_dbContext, _currentPrincipal.Object, _userService.Object, _dispatcher.Object, dateTimeProvider.Object);
    }

    private void MemberOfTeam(Guid teamId) =>
        _dispatcher
            .Setup(d => d.Send(It.Is<GetTeamsWithMemberQuery>(q => q.EmployeeId == _employeeId && q.AsOf == Today && q.TeamIds.Contains(teamId)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { teamId });

    private StoryMap AddPrivateMap(Action<StoryMapFaker>? configure = null)
    {
        var faker = new StoryMapFaker().WithVisibility(Visibility.Private);
        configure?.Invoke(faker);
        var map = faker.Generate();
        _dbContext.AddStoryMap(map);
        return map;
    }

    [Fact]
    public async Task ResolveActor_ShouldCarryTheEmployeeAdministratorAndEditorTeams()
    {
        // Arrange
        var map = AddPrivateMap(f => f.WithEditorTeams(_teamId, Guid.NewGuid()));
        MemberOfTeam(_teamId);
        _currentPrincipal.Setup(p => p.HasPermission(StoryMapAccess.AdministratorPermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var actor = await _access.ResolveActor(map, TestContext.Current.CancellationToken);

        // Assert
        actor.EmployeeId.Should().Be(_employeeId);
        actor.IsAdministrator.Should().BeTrue();
        actor.TeamIds.Should().Equal(_teamId);
    }

    [Fact]
    public async Task ResolveActor_MapWithoutEditorTeams_ShouldNotAskTheOrganization()
    {
        // Arrange
        var map = AddPrivateMap();

        // Act
        var actor = await _access.ResolveActor(map, TestContext.Current.CancellationToken);

        // Assert
        actor.TeamIds.Should().BeEmpty();
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResolveActor_WithoutAnEmployee_ShouldRefuse()
    {
        // Arrange
        var map = AddPrivateMap();
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var act = () => _access.ResolveActor(map, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task ResolveReader_ShouldResolveTeamsAmongEveryMapsEditorTeams()
    {
        // Arrange — a public map's editor teams count too, since they decide who edits it.
        var publicTeamId = Guid.NewGuid();
        AddPrivateMap(f => f.WithEditorTeams(_teamId));
        _dbContext.AddStoryMap(new StoryMapFaker().WithEditorTeams(publicTeamId).Generate());
        MemberOfTeam(_teamId);

        // Act
        var reader = await _access.ResolveReader(TestContext.Current.CancellationToken);

        // Assert
        reader.EmployeeId.Should().Be(_employeeId);
        reader.TeamIds.Should().Equal(_teamId);
        _dispatcher.Verify(d => d.Send(It.Is<GetTeamsWithMemberQuery>(q => q.TeamIds.Count == 2), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveReader_Administrator_ShouldNotAskTheOrganization()
    {
        // Arrange
        AddPrivateMap(f => f.WithEditorTeams(_teamId));
        _currentPrincipal.Setup(p => p.HoldsPermission(StoryMapAccess.AdministratorPermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var reader = await _access.ResolveReader(TestContext.Current.CancellationToken);

        // Assert
        reader.IsAdministrator.Should().BeTrue();
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CanUserAccess_WithoutViewPermission_ShouldBeFalse()
    {
        // Arrange
        var map = new StoryMapFaker().Generate();
        _dbContext.AddStoryMap(map);
        _userService.Setup(u => u.HasPermissionAsync(UserId, StoryMapAccess.ViewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var canAccess = await _access.CanUserAccess(map.Id, UserId, TestContext.Current.CancellationToken);

        // Assert
        canAccess.Should().BeFalse();
    }

    [Fact]
    public async Task CanUserAccess_PublicMap_ShouldBeTrue()
    {
        // Arrange
        var map = new StoryMapFaker().Generate();
        _dbContext.AddStoryMap(map);

        // Act
        var canAccess = await _access.CanUserAccess(map.Id, UserId, TestContext.Current.CancellationToken);

        // Assert
        canAccess.Should().BeTrue();
    }

    [Fact]
    public async Task CanUserAccess_MissingMap_ShouldBeFalse()
    {
        // Act
        var canAccess = await _access.CanUserAccess(Guid.NewGuid(), UserId, TestContext.Current.CancellationToken);

        // Assert
        canAccess.Should().BeFalse();
    }

    [Fact]
    public async Task CanUserAccess_PrivateMapTheUserIsNotOn_ShouldBeFalse()
    {
        // Arrange
        var map = AddPrivateMap();

        // Act
        var canAccess = await _access.CanUserAccess(map.Id, UserId, TestContext.Current.CancellationToken);

        // Assert
        canAccess.Should().BeFalse();
    }

    [Fact]
    public async Task CanUserAccess_PrivateMapOfAnEditorTeamTheUserIsIn_ShouldBeTrue()
    {
        // Arrange
        var map = AddPrivateMap(f => f.WithEditorTeams(_teamId));
        MemberOfTeam(_teamId);

        // Act
        var canAccess = await _access.CanUserAccess(map.Id, UserId, TestContext.Current.CancellationToken);

        // Assert
        canAccess.Should().BeTrue();
    }

    [Fact]
    public async Task CanUserAccess_PrivateMapForAnAdministrator_ShouldBeTrue()
    {
        // Arrange
        var map = AddPrivateMap();
        _userService.Setup(u => u.HasPermissionAsync(UserId, StoryMapAccess.AdministratorPermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var canAccess = await _access.CanUserAccess(map.Id, UserId, TestContext.Current.CancellationToken);

        // Assert
        canAccess.Should().BeTrue();
    }

    [Fact]
    public async Task CanUserAccess_PrivateMapForAUserWithNoEmployee_ShouldBeFalse()
    {
        // Arrange
        var map = AddPrivateMap();
        _userService.Setup(u => u.GetEmployeeIdAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var canAccess = await _access.CanUserAccess(map.Id, UserId, TestContext.Current.CancellationToken);

        // Assert
        canAccess.Should().BeFalse();
    }

    public void Dispose() => _dbContext.Dispose();
}
