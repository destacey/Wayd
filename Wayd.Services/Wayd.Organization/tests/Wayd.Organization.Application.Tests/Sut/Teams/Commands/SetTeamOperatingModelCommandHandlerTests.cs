using Wayd.Common.Domain.Models.Organizations;
using Microsoft.Extensions.Logging;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Organization.TestData;
using Moq;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Identity;
using Wayd.Tests.Shared;
using NodaTime;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Commands;

public class SetTeamOperatingModelCommandHandlerTests : IDisposable
{
    private readonly TeamFaker _teamFaker;
    private readonly FakeOrganizationDbContext _dbContext;
    private readonly SetTeamOperatingModelCommandHandler _handler;
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 6, 2, 0, 0)));
    private readonly Mock<ILogger<SetTeamOperatingModelCommandHandler>> _mockLogger;

    public SetTeamOperatingModelCommandHandlerTests()
    {
        _teamFaker = new TeamFaker();
        _dbContext = new FakeOrganizationDbContext();
        _mockLogger = new Mock<ILogger<SetTeamOperatingModelCommandHandler>>();

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(SystemUser.Id);

        _handler = new SetTeamOperatingModelCommandHandler(
            _dbContext,
            _dateTimeProvider,
            currentUser.Object,
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
            SizingMethod.StoryPoints,
            "America/Chicago",
            2,
            WorkingWeek.MondayToFriday.Days,
            null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Note: result.Value (the ID) will be Guid.Empty in unit tests since the database generates IDs
        team.OperatingModels.Should().HaveCount(1);
        team.OperatingModels.First().Methodology.Should().Be(Methodology.Scrum);
        team.OperatingModels.First().SizingMethod.Should().Be(SizingMethod.StoryPoints);
        team.OperatingModels.First().TimeZone.Should().Be("America/Chicago");
        team.OperatingModels.First().CommitmentGraceDays.Should().Be(2);
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
            SizingMethod.Count,
            "UTC",
            1,
            WorkingWeek.MondayToFriday.Days,
            null);

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
            SizingMethod.StoryPoints,
            "UTC",
            1,
            WorkingWeek.MondayToFriday.Days,
            null);

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
        var initialResult = team.SetOperatingModel(initialStartDate, Methodology.Scrum, SizingMethod.StoryPoints, "UTC", 1, WorkingWeek.MondayToFriday, null, EventActor.System, _dateTimeProvider.Now);
        initialResult.IsSuccess.Should().BeTrue();
        var initialModel = initialResult.Value;

        // Create command for new operating model
        var newStartDate = new LocalDate(2024, 1, 1);
        var command = new SetTeamOperatingModelCommand(
            team.Id,
            newStartDate,
            Methodology.Kanban,
            SizingMethod.Count,
            "UTC",
            1,
            WorkingWeek.MondayToFriday.Days,
            null);

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
        team.SetOperatingModel(initialStartDate, Methodology.Scrum, SizingMethod.StoryPoints, "UTC", 1, WorkingWeek.MondayToFriday, null, EventActor.System, _dateTimeProvider.Now);

        // Try to create a model with earlier start date
        var earlierStartDate = new LocalDate(2023, 12, 31);
        var command = new SetTeamOperatingModelCommand(
            team.Id,
            earlierStartDate,
            Methodology.Kanban,
            SizingMethod.Count,
            "UTC",
            1,
            WorkingWeek.MondayToFriday.Days,
            null);

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
        team.SetOperatingModel(initialStartDate, Methodology.Scrum, SizingMethod.StoryPoints, "UTC", 1, WorkingWeek.MondayToFriday, null, EventActor.System, _dateTimeProvider.Now);

        // Try to create a model with same start date
        var command = new SetTeamOperatingModelCommand(
            team.Id,
            initialStartDate,
            Methodology.Kanban,
            SizingMethod.Count,
            "UTC",
            1,
            WorkingWeek.MondayToFriday.Days,
            null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("start date must be after");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithAnUnknownHolidayCalendar_FailsWithoutSaving()
    {
        // Arrange
        var team = _teamFaker.Generate();
        _dbContext.AddTeam(team);
        var command = new SetTeamOperatingModelCommand(team.Id, new LocalDate(2024, 1, 1), Methodology.Scrum, SizingMethod.StoryPoints,
            "UTC", 1, WorkingWeek.MondayToFriday.Days, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        team.OperatingModels.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithAnExistingHolidayCalendar_SetsItOnTheModel()
    {
        // Arrange
        var team = _teamFaker.Generate();
        _dbContext.AddTeam(team);
        var calendar = new HolidayCalendarFaker().Generate();
        _dbContext.AddHolidayCalendar(calendar);
        var command = new SetTeamOperatingModelCommand(team.Id, new LocalDate(2024, 1, 1), Methodology.Scrum, SizingMethod.StoryPoints,
            "UTC", 1, WorkingWeek.MondayToFriday.Days, calendar.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        team.OperatingModels.Single().HolidayCalendarId.Should().Be(calendar.Id);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
