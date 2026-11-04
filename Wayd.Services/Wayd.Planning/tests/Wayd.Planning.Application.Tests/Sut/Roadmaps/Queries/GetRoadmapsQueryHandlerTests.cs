using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Models;
using Wayd.Planning.Application.Roadmaps;
using Wayd.Planning.Application.Roadmaps.Queries;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.Roadmaps;
using NodaTime.Testing;
using Wayd.Common.Domain.Events;
using Wayd.Tests.Shared;

namespace Wayd.Planning.Application.Tests.Sut.Roadmaps.Queries;

public class GetRoadmapsQueryHandlerTests
{
    private static readonly EventActor Actor = EventActor.User("user-1");
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 1, 0, 0);

    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();

    private static readonly LocalDateRange AnyDateRange = new(new LocalDate(2026, 1, 1), new LocalDate(2026, 12, 31));

    private GetRoadmapsQueryHandler CreateSut() => new(_dbContext, _currentPrincipal.Object);

    /// <summary>
    /// Adds a roadmap. A roadmap always has at least one owner (the domain requires it), so when no
    /// owner is named the roadmap is owned by some unrelated employee — the realistic shape of a
    /// public roadmap the caller does not own.
    /// </summary>
    private Roadmap AddRoadmap(string name, Visibility visibility, params Guid[] ownerIds)
    {
        var owners = ownerIds.Length > 0 ? ownerIds : [Guid.NewGuid()];
        var roadmap = Roadmap.Create(name, null, AnyDateRange, visibility, owners, Actor, Now).Value;
        _dbContext.Roadmaps.Add(roadmap);
        return roadmap;
    }

    private void GivenEmployeeId(Guid? employeeId) =>
        _currentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(employeeId);

    private void GivenAdministrator() =>
        _currentPrincipal
            .Setup(p => p.HoldsPermission(RoadmapAccess.AdministratorPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

    [Fact]
    public async Task Handle_ShouldReturnPublicRoadmapsOnly_WhenUserHasNoEmployeeLink()
    {
        // Arrange — an unlinked account owns nothing. This used to throw during handler
        // construction, failing the whole list rather than degrading.
        AddRoadmap("Public one", Visibility.Public);
        AddRoadmap("Private one", Visibility.Private, Guid.NewGuid());
        GivenEmployeeId(null);

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetRoadmapsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle();
        result[0].Name.Should().Be("Public one");
    }

    [Fact]
    public async Task Handle_ShouldNotThrow_WhenUserHasNoEmployeeLink()
    {
        // Arrange
        AddRoadmap("Public one", Visibility.Public);
        GivenEmployeeId(null);

        var sut = CreateSut();

        // Act
        var act = () => sut.Handle(new GetRoadmapsQuery(), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_ShouldReturnOwnedPrivateRoadmaps_WhenUserIsLinkedAndOwns()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        AddRoadmap("Public one", Visibility.Public);
        AddRoadmap("Mine", Visibility.Private, employeeId);
        AddRoadmap("Someone else's", Visibility.Private, Guid.NewGuid());
        GivenEmployeeId(employeeId);

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetRoadmapsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Select(r => r.Name).Should().BeEquivalentTo(["Public one", "Mine"]);
    }

    [Fact]
    public async Task Handle_ShouldExcludePrivateRoadmapsOwnedByOthers_WhenUserIsLinked()
    {
        // Arrange — the sentinel-free predicate must not accidentally widen visibility.
        var employeeId = Guid.NewGuid();
        AddRoadmap("Someone else's", Visibility.Private, Guid.NewGuid());
        GivenEmployeeId(employeeId);

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetRoadmapsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnEveryRoadmap_WhenUserIsAnAdministrator()
    {
        // Arrange
        AddRoadmap("Public one", Visibility.Public);
        AddRoadmap("Someone else's", Visibility.Private, Guid.NewGuid());
        GivenEmployeeId(Guid.NewGuid());
        GivenAdministrator();

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetRoadmapsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Select(r => r.Name).Should().BeEquivalentTo(["Public one", "Someone else's"]);
        result.Should().OnlyContain(r => r.CanManage);
    }

    [Fact]
    public async Task Handle_ShouldSetCanManage_OnlyOnRoadmapsTheUserOwns()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        AddRoadmap("Public one", Visibility.Public);
        AddRoadmap("Mine", Visibility.Public, employeeId);
        GivenEmployeeId(employeeId);

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetRoadmapsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Single(r => r.Name == "Mine").CanManage.Should().BeTrue();
        result.Single(r => r.Name == "Public one").CanManage.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldSetCanManageFalse_WhenTheTokenIsReadOnly()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        AddRoadmap("Mine", Visibility.Private, employeeId);
        GivenEmployeeId(employeeId);
        _currentPrincipal.Setup(p => p.IsReadOnly).Returns(true);

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetRoadmapsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle().Which.CanManage.Should().BeFalse();
    }
}
