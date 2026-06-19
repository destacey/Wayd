using Microsoft.Extensions.Logging;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Enums;
using Wayd.Organization.Domain.Tests.Data;
using Moq;
using NodaTime;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Commands;

public class SetTeamOperatingModelCommandHandlerTests : IDisposable
{
    private readonly TeamFaker _teamFaker;
    private readonly FakeOrganizationDbContext _dbContext;
    private readonly SetTeamOperatingModelCommandHandler _handler;
    private readonly Mock<ILogger<SetTeamOperatingModelCommandHandler>> _mockLogger;

    public SetTeamOperatingModelCommandHandlerTests()
    {
        _teamFaker = new TeamFaker();
        _dbContext = new FakeOrganizationDbContext();
        _mockLogger = new Mock<ILogger<SetTeamOperatingModelCommandHandler>>();

        _handler = new SetTeamOperatingModelCommandHandler(
            _dbContext,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ShouldCreateOperatingModel_WhenTeamExistsWithNoCurrentModel()
    {
        // Arrange
        var team = _teamFaker.Generate();
        _dbContext.AddTeam(team);

        var startDate = new LocalDate(2024, 1, 1);
        var command = new SetTeamOperatingModelCommand(
            team.Id,
            startDate,
            Methodology.Scrum,
            SizingMethod.StoryPoints);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Note: result.Value (the ID) will be Guid.Empty in unit tests since the database generates IDs
        team.OperatingModels.Should().HaveCount(1);
        team.OperatingModels.First().Methodology.Should().Be(Methodology.Scrum);
        team.OperatingModels.First().SizingMethod.Should().Be(SizingMethod.StoryPoints);
        team.OperatingModels.First().DateRange.Start.Should().Be(startDate);
        team.OperatingModels.First().IsCurrent.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldCreateOperatingModelWithKanban_WhenTeamExistsWithNoCurrentModel()
    {
        // Arrange
        var team = _teamFaker.Generate();
        _dbContext.AddTeam(team);

        var startDate = new LocalDate(2024, 1, 1);
        var command = new SetTeamOperatingModelCommand(
            team.Id,
            startDate,
            Methodology.Kanban,
            SizingMethod.Count);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        team.OperatingModels.Should().HaveCount(1);
        team.OperatingModels.First().Methodology.Should().Be(Methodology.Kanban);
        team.OperatingModels.First().SizingMethod.Should().Be(SizingMethod.Count);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTeamDoesNotExist()
    {
        // Arrange
        var nonExistentTeamId = Guid.NewGuid();
        var command = new SetTeamOperatingModelCommand(
            nonExistentTeamId,
            new LocalDate(2024, 1, 1),
            Methodology.Scrum,
            SizingMethod.StoryPoints);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(nonExistentTeamId.ToString());
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldCloseCurrentModelAndCreateNew_WhenTeamHasCurrentModel()
    {
        // Arrange
        var team = _teamFaker.Generate();
        _dbContext.AddTeam(team);

        // Create initial operating model
        var initialStartDate = new LocalDate(2023, 1, 1);
        var initialResult = team.SetOperatingModel(initialStartDate, Methodology.Scrum, SizingMethod.StoryPoints);
        initialResult.IsSuccess.Should().BeTrue();
        var initialModel = initialResult.Value;

        // Create command for new operating model
        var newStartDate = new LocalDate(2024, 1, 1);
        var command = new SetTeamOperatingModelCommand(
            team.Id,
            newStartDate,
            Methodology.Kanban,
            SizingMethod.Count);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        team.OperatingModels.Should().HaveCount(2);

        // Previous model should be closed
        initialModel.IsCurrent.Should().BeFalse();
        initialModel.DateRange.End.Should().Be(new LocalDate(2023, 12, 31));

        // New model should be current
        var newModel = team.OperatingModels.Single(m => m.IsCurrent);
        newModel.Methodology.Should().Be(Methodology.Kanban);
        newModel.SizingMethod.Should().Be(SizingMethod.Count);
        newModel.DateRange.Start.Should().Be(newStartDate);
        newModel.DateRange.End.Should().BeNull();

        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenNewStartDateIsBeforeCurrentModelStartDate()
    {
        // Arrange
        var team = _teamFaker.Generate();
        _dbContext.AddTeam(team);

        // Create initial operating model
        var initialStartDate = new LocalDate(2024, 1, 1);
        team.SetOperatingModel(initialStartDate, Methodology.Scrum, SizingMethod.StoryPoints);

        // Try to create a model with earlier start date
        var earlierStartDate = new LocalDate(2023, 12, 31);
        var command = new SetTeamOperatingModelCommand(
            team.Id,
            earlierStartDate,
            Methodology.Kanban,
            SizingMethod.Count);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("start date must be after");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenNewStartDateEqualsCurrentModelStartDate()
    {
        // Arrange
        var team = _teamFaker.Generate();
        _dbContext.AddTeam(team);

        // Create initial operating model
        var initialStartDate = new LocalDate(2024, 1, 1);
        team.SetOperatingModel(initialStartDate, Methodology.Scrum, SizingMethod.StoryPoints);

        // Try to create a model with same start date
        var command = new SetTeamOperatingModelCommand(
            team.Id,
            initialStartDate,
            Methodology.Kanban,
            SizingMethod.Count);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("start date must be after");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
