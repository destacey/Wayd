using Wayd.Common.Domain.Models.Organizations;
using NodaTime;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.TestData;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Queries;

public class GetTeamScheduleHistoryQueryHandlerTests : IDisposable
{
    private static readonly LocalDate FirstStart = new(2024, 1, 1);
    private static readonly LocalDate MoveDate = new(2024, 7, 1);
    private static readonly Instant Timestamp = Instant.FromUtc(2024, 1, 1, 0, 0);

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly GetTeamScheduleHistoryQueryHandler _handler;
    private readonly TeamFaker _teamFaker = new();

    public GetTeamScheduleHistoryQueryHandlerTests()
    {
        _handler = new GetTeamScheduleHistoryQueryHandler(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_ReturnsEveryModelsScheduleInDateOrder()
    {
        // Arrange
        var team = _teamFaker.Generate();
        team.SetOperatingModel(FirstStart, Methodology.Scrum, SizingMethod.StoryPoints, "America/New_York", 1, WorkingWeek.MondayToFriday, EventActor.System, Timestamp).IsSuccess.Should().BeTrue();
        team.SetOperatingModel(MoveDate, Methodology.Scrum, SizingMethod.Effort, "America/Chicago", 2, WorkingWeek.MondayToFriday, EventActor.System, Timestamp).IsSuccess.Should().BeTrue();
        _dbContext.AddTeam(team);

        // Act
        var result = await _handler.Handle(new GetTeamScheduleHistoryQuery(team.Id), TestContext.Current.CancellationToken);

        // Assert
        var periods = result.Where(p => p.Start >= FirstStart).ToList();
        periods.Should().HaveCount(2);
        periods[0].Should().Be(new TeamSchedulePeriodDto(FirstStart, MoveDate.PlusDays(-1), "America/New_York", 1, SizingMethod.StoryPoints));
        periods[1].Should().Be(new TeamSchedulePeriodDto(MoveDate, null, "America/Chicago", 2, SizingMethod.Effort));
    }

    [Fact]
    public async Task Handle_WhenTheTeamDoesNotExist_ReturnsNothing()
    {
        // Act
        var result = await _handler.Handle(new GetTeamScheduleHistoryQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }
}
