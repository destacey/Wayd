using System.Reflection;
using Microsoft.Extensions.Logging;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Enums;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.TestData;
using Moq;
using NodaTime;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Commands;

public class DeleteTeamOperatingModelCommandHandlerTests : IDisposable
{
    private readonly TeamFaker _teamFaker;
    private readonly FakeOrganizationDbContext _dbContext;
    private readonly DeleteTeamOperatingModelCommandHandler _handler;
    private readonly Mock<ILogger<DeleteTeamOperatingModelCommandHandler>> _mockLogger;

    public DeleteTeamOperatingModelCommandHandlerTests()
    {
        _teamFaker = new TeamFaker();
        _dbContext = new FakeOrganizationDbContext();
        _mockLogger = new Mock<ILogger<DeleteTeamOperatingModelCommandHandler>>();

        _handler = new DeleteTeamOperatingModelCommandHandler(
            _dbContext,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTryingToRemoveOnlyOperatingModel()
    {
        // Arrange
        var team = _teamFaker.Generate();
        _dbContext.AddTeam(team);

        // Create an operating model
        var startDate = new LocalDate(2024, 1, 1);
        var createResult = team.SetOperatingModel(startDate, Methodology.Scrum, SizingMethod.StoryPoints);
        createResult.IsSuccess.Should().BeTrue();
        var operatingModelId = createResult.Value.Id;

        var command = new DeleteTeamOperatingModelCommand(team.Id, operatingModelId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - Cannot remove the last operating model
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Cannot remove the last operating model");
        team.OperatingModels.Should().HaveCount(1);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTeamDoesNotExist()
    {
        // Arrange
        var nonExistentTeamId = Guid.NewGuid();
        var operatingModelId = Guid.NewGuid();

        var command = new DeleteTeamOperatingModelCommand(nonExistentTeamId, operatingModelId);

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
        var team = _teamFaker.Generate();
        _dbContext.AddTeam(team);

        // Create an operating model so the team has at least one
        team.SetOperatingModel(new LocalDate(2024, 1, 1), Methodology.Scrum, SizingMethod.StoryPoints);

        var nonExistentOperatingModelId = Guid.NewGuid();
        var command = new DeleteTeamOperatingModelCommand(team.Id, nonExistentOperatingModelId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(nonExistentOperatingModelId.ToString());
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    // TODO: convert to integration test
    [Fact]
    public async Task Handle_ShouldRemoveCurrentModel_WhenTeamHasMultipleModels()
    {
        // Arrange
        var team = _teamFaker.Generate();
        _dbContext.AddTeam(team);

        // Create first operating model
        var firstStartDate = new LocalDate(2023, 1, 1);
        var firstResult = team.SetOperatingModel(firstStartDate, Methodology.Scrum, SizingMethod.StoryPoints);
        firstResult.IsSuccess.Should().BeTrue();
        var firstModel = firstResult.Value;
        var firstModelId = Guid.NewGuid();
        SetOperatingModelId(firstModel, firstModelId);

        // Create second (current) operating model
        var secondStartDate = new LocalDate(2024, 1, 1);
        var secondResult = team.SetOperatingModel(secondStartDate, Methodology.Kanban, SizingMethod.Count);
        secondResult.IsSuccess.Should().BeTrue();
        var secondModel = secondResult.Value;
        var secondModelId = Guid.NewGuid();
        SetOperatingModelId(secondModel, secondModelId);

        secondModel.IsCurrent.Should().BeTrue();
        team.OperatingModels.Should().HaveCount(2);

        // Delete the current model (allowed because there's still one remaining)
        var command = new DeleteTeamOperatingModelCommand(team.Id, secondModelId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        team.OperatingModels.Should().HaveCount(1);
        team.OperatingModels.Single().Id.Should().Be(firstModelId);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    /// <summary>
    /// Helper method to set the Id on a TeamOperatingModel using reflection.
    /// In production, the database generates IDs, but for unit tests we need to set them manually.
    /// </summary>
    private static void SetOperatingModelId(TeamOperatingModel model, Guid id)
    {
        var idProperty = typeof(TeamOperatingModel).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        idProperty?.SetValue(model, id);
    }
}
