using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// The schedule periods project the working week through its value converter, which only a real provider
/// translates.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetTeamsScheduleHistoryQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private static readonly LocalDate ActiveDate = new(2026, 1, 5);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_ReturnsEachModelsWorkingWeekAndHolidayCalendar()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var sundayToThursday = WorkingWeek.Create([IsoDayOfWeek.Sunday, IsoDayOfWeek.Monday, IsoDayOfWeek.Tuesday, IsoDayOfWeek.Wednesday, IsoDayOfWeek.Thursday]).Value;
        var calendar = HolidayCalendar.Create($"Israel {Guid.NewGuid():N}", null, EventActor.System, SqlServerDbContextFixture.FixedNow);
        var code = new TeamCode($"S{Random.Shared.Next(10_000, 99_999)}");
        var team = Team.Create($"Schedule {code.Value}", code, null, ActiveDate, Methodology.Scrum, SizingMethod.StoryPoints,
            "Asia/Jerusalem", 1, WorkingWeek.MondayToFriday, EventActor.System, SqlServerDbContextFixture.FixedNow);
        team.SetOperatingModel(ActiveDate.PlusDays(30), Methodology.Scrum, SizingMethod.StoryPoints, "Asia/Jerusalem", 1,
            sundayToThursday, calendar.Id, EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();

        await using (var seed = _fixture.CreateContext())
        {
            seed.HolidayCalendars.Add(calendar);
            seed.Teams.Add(team);
            await seed.SaveChangesAsync(cancellationToken);
        }

        await using var context = _fixture.CreateContext();
        var handler = new GetTeamsScheduleHistoryQueryHandler(context);

        // Act
        var result = await handler.Handle(new GetTeamsScheduleHistoryQuery([team.Id]), cancellationToken);

        // Assert
        var periods = result[team.Id];
        periods.Should().HaveCount(2);
        periods[0].WorkingWeek.Should().Be(WorkingWeek.MondayToFriday);
        periods[0].HolidayCalendarId.Should().BeNull();
        periods[1].WorkingWeek.Should().Be(sundayToThursday);
        periods[1].HolidayCalendarId.Should().Be(calendar.Id);
    }
}
