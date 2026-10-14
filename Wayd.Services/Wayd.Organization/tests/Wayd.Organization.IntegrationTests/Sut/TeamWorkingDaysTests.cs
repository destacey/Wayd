using Moq;
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
    public async Task Load_AcrossTwoOperatingModels_TakesEachDaysWorkingWeekAndCalendarFromItsOwnModel()
    {
        // Arrange — Monday to Friday on the default calendar, then Monday to Thursday on the team's own
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        var calendarId = await _fixture.SeedHolidayCalendar(cancellationToken, new LocalDate(2027, 2, 9));
        Guid teamId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var team = await OrganizationSeeder.SeedTeam(seedContext, "CARDS", "Cards", cancellationToken);
            var mondayToThursday = WorkingWeek.Create([IsoDayOfWeek.Monday, IsoDayOfWeek.Tuesday, IsoDayOfWeek.Wednesday, IsoDayOfWeek.Thursday]).Value;
            team.SetOperatingModel(SecondWeek, Methodology.Kanban, SizingMethod.Count, "UTC", 1, mondayToThursday, calendarId, EventActor.System, SqlServerDbContextFixture.FixedNow)
                .IsSuccess.Should().BeTrue();
            await seedContext.SaveChangesAsync(cancellationToken);

            teamId = team.Id;
        }

        await using var context = _fixture.CreateContext();

        // Act
        var teamDays = await TeamWorkingDays.Load(context, Settings(new SchedulingSettings()), teamId, From, To, cancellationToken);

        // Assert — the 9th is still a day of the working week: whether it is a holiday depends on the person
        teamDays.Should().Equal(
            new TeamDay(new LocalDate(2027, 2, 1), null),
            new TeamDay(new LocalDate(2027, 2, 2), null),
            new TeamDay(new LocalDate(2027, 2, 3), null),
            new TeamDay(new LocalDate(2027, 2, 4), null),
            new TeamDay(new LocalDate(2027, 2, 5), null),
            new TeamDay(new LocalDate(2027, 2, 8), calendarId),
            new TeamDay(new LocalDate(2027, 2, 9), calendarId),
            new TeamDay(new LocalDate(2027, 2, 10), calendarId),
            new TeamDay(new LocalDate(2027, 2, 11), calendarId));
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

        var settings = Settings(new SchedulingSettings { DefaultWorkingDays = [IsoDayOfWeek.Monday, IsoDayOfWeek.Tuesday, IsoDayOfWeek.Wednesday] });

        await using var context = _fixture.CreateContext();

        // Act
        var teamDays = await TeamWorkingDays.Load(context, settings, teamOfTeamsId, From, To, cancellationToken);

        // Assert — no calendar of its own on any day, which reads as the system default
        teamDays.Should().Equal(
            new TeamDay(new LocalDate(2027, 2, 1), null),
            new TeamDay(new LocalDate(2027, 2, 2), null),
            new TeamDay(new LocalDate(2027, 2, 3), null),
            new TeamDay(new LocalDate(2027, 2, 8), null),
            new TeamDay(new LocalDate(2027, 2, 9), null),
            new TeamDay(new LocalDate(2027, 2, 10), null));
    }

    private static ISettings<SchedulingSettings> Settings(SchedulingSettings value)
    {
        var settings = new Mock<ISettings<SchedulingSettings>>();
        settings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(value);
        return settings.Object;
    }
}
