using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Models;
using Wayd.Planning.Application.Roadmaps.Queries;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.Roadmaps;

namespace Wayd.Planning.Application.Tests.Sut.Roadmaps.Queries;

public class GetRoadmapsQueryHandlerTests
{
    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();

    private static readonly LocalDateRange AnyDateRange = new(new LocalDate(2026, 1, 1), new LocalDate(2026, 12, 31));

    private GetRoadmapsQueryHandler CreateSut() => new(_dbContext, _currentPrincipal.Object);

    /// <summary>
    /// Adds a roadmap. A roadmap always has at least one manager (the domain requires it), so when no
    /// manager is named the roadmap is managed by some unrelated employee — the realistic shape of a
    /// public roadmap the caller does not manage.
    /// </summary>
    private Roadmap AddRoadmap(string name, Visibility visibility, params Guid[] managerIds)
    {
        var managers = managerIds.Length > 0 ? managerIds : [Guid.NewGuid()];
        var roadmap = Roadmap.Create(name, null, AnyDateRange, visibility, managers).Value;
        _dbContext.Roadmaps.Add(roadmap);
        return roadmap;
    }

    private void GivenEmployeeId(Guid? employeeId) =>
        _currentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(employeeId);

    [Fact]
    public async Task Handle_ShouldReturnPublicRoadmapsOnly_WhenUserHasNoEmployeeLink()
    {
        // Arrange — an unlinked account manages nothing. This used to throw during handler
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
    public async Task Handle_ShouldReturnManagedPrivateRoadmaps_WhenUserIsLinkedAndManages()
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
    public async Task Handle_ShouldExcludePrivateRoadmapsManagedByOthers_WhenUserIsLinked()
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
}
