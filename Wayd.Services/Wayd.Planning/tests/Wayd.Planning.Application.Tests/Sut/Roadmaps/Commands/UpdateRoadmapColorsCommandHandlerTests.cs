using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Interfaces;
using Wayd.Planning.Application.Roadmaps.Commands;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.Roadmaps;
using Wayd.Planning.Domain.Tests.Data;
using Moq;

namespace Wayd.Planning.Application.Tests.Sut.Roadmaps.Commands;

public class UpdateRoadmapColorsCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly Mock<ILogger<UpdateRoadmapColorsCommandHandler>> _mockLogger;
    private readonly Mock<ICurrentUser> _mockCurrentUser;
    private readonly Guid _currentEmployeeId = Guid.NewGuid();
    private readonly RoadmapFaker _faker;

    public UpdateRoadmapColorsCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<UpdateRoadmapColorsCommandHandler>>();
        _mockCurrentUser = new Mock<ICurrentUser>();
        _mockCurrentUser.Setup(u => u.GetEmployeeId()).Returns(_currentEmployeeId);
        _faker = new RoadmapFaker();
    }

    private UpdateRoadmapColorsCommandHandler CreateHandler() =>
        new(_dbContext, _mockCurrentUser.Object, _mockLogger.Object);

    private Roadmap CreateActiveRoadmap(Guid? managerId = null)
    {
        var mgrId = managerId ?? _currentEmployeeId;
        var fakeRoadmap = _faker.Generate();
        return Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [mgrId]).Value;
    }

    [Fact]
    public async Task Handle_ShouldUpdateColors_WhenValidAndUserIsManager()
    {
        // Arrange
        var roadmap = CreateActiveRoadmap();
        _dbContext.AddRoadmap(roadmap);
        var handler = CreateHandler();

        var command = new UpdateRoadmapColorsCommand(roadmap.Id,
        [
            new UpsertRoadmapColorModel("#FF0000", "At Risk", 1, true),
            new UpsertRoadmapColorModel("#00FF00", "On Track", 2, false),
        ]);

        // Act
        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        roadmap.Colors.Should().HaveCount(2);
        roadmap.Colors.Should().ContainSingle(c => c.Color == "#FF0000" && c.Name == "At Risk" && c.IsDefault);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldReplaceExistingColors()
    {
        // Arrange
        var roadmap = CreateActiveRoadmap();
        roadmap.UpdateColors(
            [new UpsertRoadmapColorModel("#111111", "Old", 1, false)],
            _currentEmployeeId);
        _dbContext.AddRoadmap(roadmap);
        var handler = CreateHandler();

        var command = new UpdateRoadmapColorsCommand(roadmap.Id,
        [
            new UpsertRoadmapColorModel("#222222", "New", 1, false),
        ]);

        // Act
        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        roadmap.Colors.Should().ContainSingle(c => c.Color == "#222222");
        roadmap.Colors.Should().NotContain(c => c.Color == "#111111");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldClearColors_WhenEmptyCollection()
    {
        // Arrange
        var roadmap = CreateActiveRoadmap();
        roadmap.UpdateColors(
            [new UpsertRoadmapColorModel("#111111", "Old", 1, false)],
            _currentEmployeeId);
        _dbContext.AddRoadmap(roadmap);
        var handler = CreateHandler();

        var command = new UpdateRoadmapColorsCommand(roadmap.Id, []);

        // Act
        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        roadmap.Colors.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenRoadmapNotFound()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new UpdateRoadmapColorsCommand(Guid.NewGuid(),
        [
            new UpsertRoadmapColorModel("#FF0000", "At Risk", 1, false),
        ]);

        // Act
        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldNotSave_WhenUserIsNotManager()
    {
        // Arrange
        var otherManagerId = Guid.NewGuid();
        var roadmap = CreateActiveRoadmap(managerId: otherManagerId);
        _dbContext.AddRoadmap(roadmap);
        var handler = CreateHandler();

        var command = new UpdateRoadmapColorsCommand(roadmap.Id,
        [
            new UpsertRoadmapColorModel("#FF0000", "At Risk", 1, false),
        ]);

        // Act
        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        roadmap.Colors.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
