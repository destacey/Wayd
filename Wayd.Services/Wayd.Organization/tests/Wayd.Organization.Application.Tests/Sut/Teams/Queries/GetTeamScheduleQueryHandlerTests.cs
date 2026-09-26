using NodaTime;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Enums;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.TestData;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Queries;

public class GetTeamScheduleQueryHandlerTests : IDisposable
{
    private static readonly LocalDate FirstStart = new(2024, 1, 1);
    private static readonly LocalDate MoveDate = new(2024, 7, 1);

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly GetTeamScheduleQueryHandler _handler;
    private readonly TeamFaker _teamFaker = new();

    public GetTeamScheduleQueryHandlerTests()
    {
        _handler = new GetTeamScheduleQueryHandler(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    private Team TeamThatMoved()
    {
        var team = _teamFaker.Generate();
        team.SetOperatingModel(FirstStart, Methodology.Scrum, SizingMethod.StoryPoints, "America/New_York", 1).IsSuccess.Should().BeTrue();
        team.SetOperatingModel(MoveDate, Methodology.Scrum, SizingMethod.StoryPoints, "America/Chicago", 2).IsSuccess.Should().BeTrue();
        _dbContext.AddTeam(team);
        return team;
    }

    [Fact]
    public async Task Handle_BeforeAMove_ReturnsTheOldModelsSchedule()
    {
        // Arrange
        var team = TeamThatMoved();

        // Act
        var result = await _handler.Handle(new GetTeamScheduleQuery(team.Id, MoveDate.PlusDays(-1)), TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(new TeamScheduleDto("America/New_York", 1));
    }

    [Fact]
    public async Task Handle_OnAndAfterAMove_ReturnsTheNewModelsSchedule()
    {
        // Arrange
        var team = TeamThatMoved();

        // Act
        var onMove = await _handler.Handle(new GetTeamScheduleQuery(team.Id, MoveDate), TestContext.Current.CancellationToken);
        var later = await _handler.Handle(new GetTeamScheduleQuery(team.Id, MoveDate.PlusYears(1)), TestContext.Current.CancellationToken);

        // Assert
        onMove.Should().Be(new TeamScheduleDto("America/Chicago", 2));
        later.Should().Be(new TeamScheduleDto("America/Chicago", 2));
    }

    [Fact]
    public async Task Handle_AfterACorrection_ReturnsTheCorrectedScheduleForTheWholePeriod()
    {
        // Arrange
        var team = TeamThatMoved();
        var original = team.OperatingModels.Single(m => m.DateRange.Start == FirstStart);
        original.Update(original.Methodology, original.SizingMethod, "America/Denver", 1).IsSuccess.Should().BeTrue();

        // Act
        var result = await _handler.Handle(new GetTeamScheduleQuery(team.Id, FirstStart), TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(new TeamScheduleDto("America/Denver", 1));
    }

    [Fact]
    public async Task Handle_BeforeTheFirstModel_ReturnsNull()
    {
        // Arrange
        var team = TeamThatMoved();

        // Act
        var result = await _handler.Handle(new GetTeamScheduleQuery(team.Id, FirstStart.PlusDays(-1)), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_UnknownTeam_ReturnsNull()
    {
        // Arrange
        TeamThatMoved();

        // Act
        var result = await _handler.Handle(new GetTeamScheduleQuery(Guid.NewGuid(), MoveDate), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }
}
