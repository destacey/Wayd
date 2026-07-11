using Microsoft.Extensions.Logging;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Enums;
using Wayd.Organization.TestData;
using Wayd.Tests.Shared;
using Moq;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Commands;

public class UpdateTeamOperatingModelCommandHandlerTests : IDisposable
{
    private readonly TeamFaker _teamFaker;
    private readonly TeamOperatingModelFaker _operatingModelFaker;
    private readonly FakeOrganizationDbContext _dbContext;
    private readonly UpdateTeamOperatingModelCommandHandler _handler;
    private readonly Mock<ILogger<UpdateTeamOperatingModelCommandHandler>> _mockLogger;
    private readonly TestingDateTimeProvider _dateTimeProvider;

    public UpdateTeamOperatingModelCommandHandlerTests()
    {
        _teamFaker = new TeamFaker();
        _operatingModelFaker = new TeamOperatingModelFaker();

        _dbContext = new FakeOrganizationDbContext();
        _mockLogger = new Mock<ILogger<UpdateTeamOperatingModelCommandHandler>>();
        _dateTimeProvider = new TestingDateTimeProvider(DateTime.UtcNow);

        _handler = new UpdateTeamOperatingModelCommandHandler(
            _dbContext,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ShouldUpdateOperatingModel_WhenModelExists()
    {
        // Arrange
        var operatingModelFaker = _operatingModelFaker
            .WithMethodology(Methodology.Scrum)
            .WithSizingMethod(SizingMethod.StoryPoints);

        var team = _teamFaker.WithOperatingModel(operatingModelFaker).Generate();
        var operatingModel = team.OperatingModels.First();

        _dbContext.AddTeam(team);

        var command = new UpdateTeamOperatingModelCommand(
            team.Id,
            operatingModel.Id,
            Methodology.Kanban,
            SizingMethod.Count);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        operatingModel.Methodology.Should().Be(Methodology.Kanban);
        operatingModel.SizingMethod.Should().Be(SizingMethod.Count);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldUpdateMethodologyOnly_WhenSizingMethodUnchanged()
    {
        // Arrange
        var operatingModelFaker = _operatingModelFaker
            .WithMethodology(Methodology.Scrum)
            .WithSizingMethod(SizingMethod.StoryPoints);

        var team = _teamFaker.WithOperatingModel(operatingModelFaker).Generate();
        var operatingModel = team.OperatingModels.First();

        _dbContext.AddTeam(team);

        var command = new UpdateTeamOperatingModelCommand(
            team.Id,
            operatingModel.Id,
            Methodology.Kanban,
            SizingMethod.StoryPoints);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        operatingModel.Methodology.Should().Be(Methodology.Kanban);
        operatingModel.SizingMethod.Should().Be(SizingMethod.StoryPoints);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldUpdateSizingMethodOnly_WhenMethodologyUnchanged()
    {
        // Arrange
        var operatingModelFaker = _operatingModelFaker
            .WithMethodology(Methodology.Scrum)
            .WithSizingMethod(SizingMethod.StoryPoints);

        var team = _teamFaker.WithOperatingModel(operatingModelFaker).Generate();
        var operatingModel = team.OperatingModels.First();

        _dbContext.AddTeam(team);

        var command = new UpdateTeamOperatingModelCommand(
            team.Id,
            operatingModel.Id,
            Methodology.Scrum,
            SizingMethod.Count);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        operatingModel.Methodology.Should().Be(Methodology.Scrum);
        operatingModel.SizingMethod.Should().Be(SizingMethod.Count);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenValuesUnchanged()
    {
        // Arrange
        var operatingModelFaker = _operatingModelFaker
            .WithMethodology(Methodology.Scrum)
            .WithSizingMethod(SizingMethod.StoryPoints);

        var team = _teamFaker.WithOperatingModel(operatingModelFaker).Generate();
        var operatingModel = team.OperatingModels.First();

        _dbContext.AddTeam(team);

        var command = new UpdateTeamOperatingModelCommand(
            team.Id,
            operatingModel.Id,
            Methodology.Scrum,
            SizingMethod.StoryPoints);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        operatingModel.Methodology.Should().Be(Methodology.Scrum);
        operatingModel.SizingMethod.Should().Be(SizingMethod.StoryPoints);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTeamDoesNotExist()
    {
        // Arrange
        var nonExistentTeamId = Guid.NewGuid();
        var operatingModelId = Guid.NewGuid();

        var command = new UpdateTeamOperatingModelCommand(
            nonExistentTeamId,
            operatingModelId,
            Methodology.Kanban,
            SizingMethod.Count);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(nonExistentTeamId.ToString());
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenOperatingModelDoesNotExist()
    {
        // Arrange
        var operatingModelFaker = _operatingModelFaker
            .WithMethodology(Methodology.Scrum)
            .WithSizingMethod(SizingMethod.StoryPoints);

        var team = _teamFaker.WithOperatingModel(operatingModelFaker).Generate();
        var nonExistentOperatingModelId = Guid.NewGuid();

        _dbContext.AddTeam(team);

        var command = new UpdateTeamOperatingModelCommand(
            team.Id,
            nonExistentOperatingModelId,
            Methodology.Kanban,
            SizingMethod.Count);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(nonExistentOperatingModelId.ToString());
        result.Error.Should().Contain(team.Id.ToString());
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenOperatingModelBelongsToDifferentTeam()
    {
        // Arrange
        var team1OpModel = _operatingModelFaker
            .WithMethodology(Methodology.Scrum)
            .WithSizingMethod(SizingMethod.StoryPoints);

        var team = _teamFaker.WithOperatingModel(team1OpModel).Generate();

        var team2OpModel = _operatingModelFaker
            .WithMethodology(Methodology.Kanban)
            .WithSizingMethod(SizingMethod.Count);

        var differentTeam = _teamFaker.WithOperatingModel(team2OpModel).Generate();

        var operatingModel = team.OperatingModels.First();

        _dbContext.AddTeams([team, differentTeam]);

        // Use the operating model ID but with a different team ID
        var command = new UpdateTeamOperatingModelCommand(
            differentTeam.Id,
            operatingModel.Id,
            Methodology.Kanban,
            SizingMethod.Count);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldNotAffectOtherModels_WhenUpdatingSpecificModel()
    {
        // Arrange
        var team1OpModel = _operatingModelFaker
            .WithMethodology(Methodology.Scrum)
            .WithSizingMethod(SizingMethod.StoryPoints);

        var team = _teamFaker.WithOperatingModel(team1OpModel).Generate();

        var team2OpModel = _operatingModelFaker
            .WithMethodology(Methodology.Kanban)
            .WithSizingMethod(SizingMethod.Count);

        var differentTeam = _teamFaker.WithOperatingModel(team2OpModel).Generate();

        var targetModel = team.OperatingModels.First();
        var otherModel = differentTeam.OperatingModels.First();

        _dbContext.AddTeams([team, differentTeam]);

        var command = new UpdateTeamOperatingModelCommand(
            team.Id,
            targetModel.Id,
            Methodology.Kanban,
            SizingMethod.Count);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        targetModel.Methodology.Should().Be(Methodology.Kanban);

        // Other model should remain unchanged
        otherModel.Methodology.Should().Be(Methodology.Kanban);
        otherModel.SizingMethod.Should().Be(SizingMethod.Count);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
