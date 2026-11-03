using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class SetStoryMapOwnersCommandHandlerTests : IDisposable
{
    private readonly FakeStoryMapAccess _access = new();
    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<IStoryMapNotifier> _mockNotifier = new();
    private readonly SetStoryMapOwnersCommandHandler _handler;

    public SetStoryMapOwnersCommandHandlerTests()
    {
        _handler = new SetStoryMapOwnersCommandHandler(_dbContext, _access, _mockNotifier.Object, new Mock<ILogger<SetStoryMapOwnersCommandHandler>>().Object);
    }

    [Fact]
    public async Task Handle_ShouldReplaceTheOwnersAndRecheckAccess()
    {
        // Arrange
        var map = new StoryMapFaker().Generate();
        _dbContext.AddStoryMap(map);
        var employee = new EmployeeFaker().Generate();
        _dbContext.AddEmployee(employee);

        // Act
        var result = await _handler.Handle(new SetStoryMapOwnersCommand(map.Id, [employee.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        map.Owners.Select(o => o.EmployeeId).Should().Equal(employee.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyAccessChanged(map.Id), Times.Once);
    }

    [Fact]
    public async Task Handle_ByEditor_ShouldFailAsNotAnOwner()
    {
        // Arrange — the hole this closes: anyone who could edit could make themselves owner.
        var editorId = Guid.NewGuid();
        var map = new StoryMapFaker().WithEditors(editorId).Generate();
        _dbContext.AddStoryMap(map);
        _access.Actor = StoryMapActors.Employee(editorId);

        // Act
        var result = await _handler.Handle(new SetStoryMapOwnersCommand(map.Id, [editorId]), TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(StoryMap.NotAnOwnerError);
        map.Owners.Select(o => o.EmployeeId).Should().Equal(StoryMapActors.OwnerEmployeeId);
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_UnknownEmployee_ShouldFail()
    {
        // Arrange
        var map = new StoryMapFaker().Generate();
        _dbContext.AddStoryMap(map);

        // Act
        var result = await _handler.Handle(new SetStoryMapOwnersCommand(map.Id, [Guid.NewGuid()]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        map.Owners.Select(o => o.EmployeeId).Should().Equal(StoryMapActors.OwnerEmployeeId);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenAConcurrentChangeConflicts_ShouldReapplyAndSave()
    {
        // Arrange — another owner changed the map between our read and our write. The conflict is what keeps two
        // owners who each remove the other from both saving and leaving the map with none.
        var map = new StoryMapFaker().Generate();
        _dbContext.AddStoryMap(map);
        var employee = new EmployeeFaker().Generate();
        _dbContext.AddEmployee(employee);
        _dbContext.ConcurrencyConflictsToThrow = 1;
        // The fake reloads the same, already-changed map, where the old owner no longer is one; an administrator
        // manages it either way.
        _access.Actor = StoryMapActors.Administrator;

        // Act
        var result = await _handler.Handle(new SetStoryMapOwnersCommand(map.Id, [employee.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _dbContext.SaveChangesCallCount.Should().Be(2);
        _access.ResolveActorCallCount.Should().Be(2);
        map.Owners.Select(o => o.EmployeeId).Should().Equal(employee.Id);
    }

    [Fact]
    public async Task Handle_MapNotFound_ShouldFail()
    {
        // Act
        var result = await _handler.Handle(new SetStoryMapOwnersCommand(Guid.NewGuid(), [Guid.NewGuid()]), TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(StoryMap.NotFoundError);
    }

    public void Dispose() => _dbContext.Dispose();
}
