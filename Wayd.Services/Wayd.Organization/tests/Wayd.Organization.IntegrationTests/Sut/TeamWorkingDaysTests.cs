using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Settings;
using Wayd.Organization.Application.EmployeeCapacities;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// A team's working days against a real SQL Server container: the working week is read off each operating
/// model, which only a real provider shows translates.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class TeamWorkingDaysTests(SqlServerDbContextFixture fixture)
{
    // Monday to the Sunday two weeks on.
    private static readonly LocalDate From = new(2027, 2, 1);
    private static readonly LocalDate To = new(2027, 2, 14);
    private static readonly LocalDate SecondWeek = new(2027, 2, 8);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Load_AcrossTwoOperatingModels_TakesEachDaysWorkingWeekAndHolidaysFromItsOwnModel()
    {
        // Arrange — Monday to Friday on the default calendar, then Monday to Thursday on the team's own
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        Guid teamId, calendarId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var calendar = HolidayCalendar.Create($"Calendar {Guid.NewGuid():N}", null, EventActor.System, SqlServerDbContextFixture.FixedNow);
            await seedContext.HolidayCalendars.AddAsync(calendar, cancellationToken);
            await seedContext.SaveChangesAsync(cancellationToken);

            var team = await OrganizationSeeder.SeedTeam(seedContext, "CARDS", "Cards", cancellationToken);
            var mondayToThursday = WorkingWeek.Create([IsoDayOfWeek.Monday, IsoDayOfWeek.Tuesday, IsoDayOfWeek.Wednesday, IsoDayOfWeek.Thursday]).Value;
            team.SetOperatingModel(SecondWeek, Methodology.Kanban, SizingMethod.Count, "UTC", 1, mondayToThursday, calendar.Id, EventActor.System, SqlServerDbContextFixture.FixedNow)
                .IsSuccess.Should().BeTrue();
            await seedContext.SaveChangesAsync(cancellationToken);

            (teamId, calendarId) = (team.Id, calendar.Id);
        }

        // The default calendar is off on the 3rd and the 10th; the team's own on the 9th.
        var dispatcher = Holidays(asked => asked is null
            ? [new LocalDate(2027, 2, 3), new LocalDate(2027, 2, 10)]
            : asked == calendarId ? [new LocalDate(2027, 2, 9)] : []);

        await using var context = _fixture.CreateContext();

        // Act
        var workingDays = await TeamWorkingDays.Load(context, dispatcher, Settings(new SchedulingSettings()), teamId, From, To, cancellationToken);

        // Assert — the 10th is worked: it is a holiday only in the calendar the second model does not use
        workingDays.Should().Equal(
            new LocalDate(2027, 2, 1), new LocalDate(2027, 2, 2), new LocalDate(2027, 2, 4), new LocalDate(2027, 2, 5),
            new LocalDate(2027, 2, 8), new LocalDate(2027, 2, 10), new LocalDate(2027, 2, 11));
    }

    [Fact]
    public async Task Load_ForATeamOfTeams_UsesTheSystemDefaultWorkingWeekAndCalendar()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        Guid teamOfTeamsId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var teamOfTeams = TeamOfTeams.Create("Payments", new TeamCode("PAY"), null, new LocalDate(2024, 1, 1), "UTC", EventActor.System, SqlServerDbContextFixture.FixedNow);
            await seedContext.TeamOfTeams.AddAsync(teamOfTeams, cancellationToken);
            await seedContext.SaveChangesAsync(cancellationToken);
            teamOfTeamsId = teamOfTeams.Id;
        }

        List<Guid?> calendarsRead = [];
        var dispatcher = Holidays(calendarId =>
        {
            calendarsRead.Add(calendarId);
            return [new LocalDate(2027, 2, 2)];
        });
        var settings = Settings(new SchedulingSettings { DefaultWorkingDays = [IsoDayOfWeek.Monday, IsoDayOfWeek.Tuesday, IsoDayOfWeek.Wednesday] });

        await using var context = _fixture.CreateContext();

        // Act
        var workingDays = await TeamWorkingDays.Load(context, dispatcher, settings, teamOfTeamsId, From, To, cancellationToken);

        // Assert
        workingDays.Should().Equal(
            new LocalDate(2027, 2, 1), new LocalDate(2027, 2, 3),
            new LocalDate(2027, 2, 8), new LocalDate(2027, 2, 9), new LocalDate(2027, 2, 10));
        calendarsRead.Should().Equal([null]);
    }

    /// <summary>A dispatcher answering the holiday read with <paramref name="holidaysOf"/> the calendar asked for.</summary>
    private static IDispatcher Holidays(Func<Guid?, IReadOnlyList<LocalDate>> holidaysOf)
    {
        var dispatcher = new Mock<IDispatcher>();
        dispatcher
            .Setup(d => d.Send(It.IsAny<IQuery<IReadOnlyList<LocalDate>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IQuery<IReadOnlyList<LocalDate>> query, CancellationToken _) => holidaysOf(((GetHolidayDatesQuery)query).HolidayCalendarId));
        return dispatcher.Object;
    }

    private static ISettings<SchedulingSettings> Settings(SchedulingSettings value)
    {
        var settings = new Mock<ISettings<SchedulingSettings>>();
        settings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(value);
        return settings.Object;
    }
}
