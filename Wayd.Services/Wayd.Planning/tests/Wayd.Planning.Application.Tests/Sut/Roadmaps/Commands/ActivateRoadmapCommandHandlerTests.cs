using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Application.Roadmaps.Commands;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.Roadmaps;
using Wayd.Planning.Domain.Tests.Data;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.Roadmaps.Commands;

public class ActivateRoadmapCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly Mock<ILogger<ActivateRoadmapCommandHandler>> _mockLogger;
    private readonly Mock<ICurrentPrincipal> _mockCurrentPrincipal;
    private readonly Guid _currentEmployeeId = Guid.NewGuid();
    private readonly RoadmapFaker _faker;

    public ActivateRoadmapCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<ActivateRoadmapCommandHandler>>();
        _mockCurrentPrincipal = new Mock<ICurrentPrincipal>();
        _mockCurrentPrincipal.Setup(u => u.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_currentEmployeeId);
        _faker = new RoadmapFaker();
    }

    private ActivateRoadmapCommandHandler CreateHandler() =>
        new(_dbContext, _mockCurrentPrincipal.Object, _mockLogger.Object);

    private Roadmap CreateArchivedRoadmap(Guid? managerId = null)
    {
        var mgrId = managerId ?? _currentEmployeeId;
        var fakeRoadmap = _faker.Generate();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [mgrId]).Value;
        roadmap.Archive(mgrId);
        return roadmap;
    }

    private Roadmap CreateActiveRoadmap(Guid? managerId = null)
    {
        var mgrId = managerId ?? _currentEmployeeId;
        var fakeRoadmap = _faker.Generate();
        return Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [mgrId]).Value;
    }

    [Fact]
    public async Task Handle_ShouldActivateRoadmap_WhenArchivedAndUserIsManager()
    {
        // Arrange
        var roadmap = CreateArchivedRoadmap();
        _dbContext.AddRoadmap(roadmap);
        var handler = CreateHandler();

        var command = new ActivateRoadmapCommand(roadmap.Id);

        // Act
        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        roadmap.State.Should().Be(RoadmapState.Active);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenRoadmapNotFound()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new ActivateRoadmapCommand(Guid.NewGuid());

        // Act
        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenUserIsNotManager()
    {
        // Arrange
        var otherManagerId = Guid.NewGuid();
        var roadmap = CreateArchivedRoadmap(managerId: otherManagerId);
        _dbContext.AddRoadmap(roadmap);
        var handler = CreateHandler();

        var command = new ActivateRoadmapCommand(roadmap.Id);

        // Act
        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        roadmap.State.Should().Be(RoadmapState.Archived);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenRoadmapIsAlreadyActive()
    {
        // Arrange
        var roadmap = CreateActiveRoadmap();
        _dbContext.AddRoadmap(roadmap);
        var handler = CreateHandler();

        var command = new ActivateRoadmapCommand(roadmap.Id);

        // Act
        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        roadmap.State.Should().Be(RoadmapState.Active);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
