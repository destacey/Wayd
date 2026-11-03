using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.StoryMaps;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

public class SetStoryMapEditorsCommandHandlerTests : IDisposable
{
    private readonly FakeStoryMapAccess _access = new();
    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<IStoryMapNotifier> _mockNotifier = new();
    private readonly SetStoryMapEditorsCommandHandler _handler;

    public SetStoryMapEditorsCommandHandlerTests()
    {
        _handler = new SetStoryMapEditorsCommandHandler(_dbContext, _access, _mockNotifier.Object, new Mock<ILogger<SetStoryMapEditorsCommandHandler>>().Object);
    }

    [Fact]
    public async Task Handle_ShouldReplaceTheEditorsAndRecheckAccess()
    {
        // Arrange
        var map = new StoryMapFaker().WithEditors(Guid.NewGuid()).Generate();
        _dbContext.AddStoryMap(map);
        var employee = new EmployeeFaker().Generate();
        var team = new PlanningTeamFaker(TeamType.Team).Generate();
        _dbContext.AddEmployee(employee);
        _dbContext.AddPlanningTeam(team);

        // Act
        var result = await _handler.Handle(new SetStoryMapEditorsCommand(map.Id, [employee.Id], [team.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        map.Editors.Select(e => e.EmployeeId).Should().Equal(employee.Id);
        map.EditorTeams.Select(t => t.TeamId).Should().Equal(team.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyAccessChanged(map.Id), Times.Once);
    }

    [Fact]
    public async Task Handle_UnknownTeam_ShouldFail()
    {
        // Arrange
        var map = new StoryMapFaker().Generate();
        _dbContext.AddStoryMap(map);

        // Act
        var result = await _handler.Handle(new SetStoryMapEditorsCommand(map.Id, [], [Guid.NewGuid()]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        map.EditorTeams.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ByViewer_ShouldFailAsNotAnOwner()
    {
        // Arrange
        var map = new StoryMapFaker().Generate();
        _dbContext.AddStoryMap(map);
        _access.Actor = StoryMapActors.Viewer;

        // Act
        var result = await _handler.Handle(new SetStoryMapEditorsCommand(map.Id, [StoryMapActors.Viewer.EmployeeId], []), TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(StoryMap.NotAnOwnerError);
        map.Editors.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.VerifyNoOtherCalls();
    }

    public void Dispose() => _dbContext.Dispose();
}
