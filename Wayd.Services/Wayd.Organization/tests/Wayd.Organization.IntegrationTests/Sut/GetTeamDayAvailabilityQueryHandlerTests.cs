using Moq;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Settings;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Organization.Application.EmployeeCapacities.Queries;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// The share of a team's delivery capacity that is there each day, against a real SQL Server container: the
/// role join, the batched holiday read across calendars and the time away read only translate on a real provider.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetTeamDayAvailabilityQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private static readonly LocalDate LongBefore = new(2026, 6, 1);
    private static readonly LocalDate Monday = new(2027, 2, 1);
    private static readonly LocalDate Tuesday = new(2027, 2, 2);
    private static readonly LocalDate Wednesday = new(2027, 2, 3);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_WhenThreeOfEightShareAHolidayCalendar_GivesFiveEighthsOnItsHoliday()
    {
        // Arrange — three people follow a regional calendar with a holiday on the Tuesday; the other five follow
        // the system default, which has none
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        var regionalCalendarId = await _fixture.SeedHolidayCalendar(cancellationToken, Tuesday);
        Guid teamId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var team = await OrganizationSeeder.SeedTeam(seedContext, "CARDS", "Cards", cancellationToken);
            teamId = team.Id;

            for (var person = 0; person < 8; person++)
            {
                var employee = await OrganizationSeeder.SeedEmployee(seedContext, $"E-{1000 + person}", $"person{person}@acme.example", cancellationToken);
                var capacity = Allocate(employee.Id, new AllocationChange(team.Id, null, LongBefore, null, 50));
                if (person < 3)
                    capacity.ChangeHolidayCalendar(regionalCalendarId, EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
                await seedContext.EmployeeCapacities.AddAsync(capacity, cancellationToken);
            }

            await seedContext.SaveChangesAsync(cancellationToken);
        }

        await using var context = _fixture.CreateContext();

        // Act
        var availability = await Handler(context).Handle(new GetTeamDayAvailabilityQuery(teamId, Monday, Wednesday), cancellationToken);

        // Assert
        availability.Should().NotBeNull();
        availability!.Keys.Should().BeEquivalentTo([Monday, Tuesday, Wednesday]);
        availability[Monday].Share.Should().Be(1m);
        availability[Tuesday].Should().Be(new TeamDayAvailability(4m, 2.5m));
        availability[Tuesday].Share.Should().Be(0.625m);
        availability[Wednesday].Share.Should().Be(1m);
    }

    [Fact]
    public async Task Handle_WhenOneOfFourIsAwayForHalfADay_GivesSevenEighthsThatDay()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        Guid teamId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var team = await OrganizationSeeder.SeedTeam(seedContext, "CARDS", "Cards", cancellationToken);
            teamId = team.Id;

            for (var person = 0; person < 4; person++)
            {
                var employee = await OrganizationSeeder.SeedEmployee(seedContext, $"E-{1000 + person}", $"person{person}@acme.example", cancellationToken);
                var capacity = Allocate(employee.Id, new AllocationChange(team.Id, null, LongBefore, null, 100));
                if (person == 0)
                    capacity.RecordTimeAway(Tuesday, Tuesday, 50, TimeAwayReason.Training, EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
                await seedContext.EmployeeCapacities.AddAsync(capacity, cancellationToken);
            }

            await seedContext.SaveChangesAsync(cancellationToken);
        }

        await using var context = _fixture.CreateContext();

        // Act
        var availability = await Handler(context).Handle(new GetTeamDayAvailabilityQuery(teamId, Monday, Wednesday), cancellationToken);

        // Assert
        availability![Tuesday].Should().Be(new TeamDayAvailability(4m, 3.5m));
        availability[Tuesday].Share.Should().Be(0.875m);
        availability[Monday].Share.Should().Be(1m);
    }

    [Fact]
    public async Task Handle_ForSomeoneOnTwoTeams_LowersBothTeamsOnTheDayTheyAreAway()
    {
        // Arrange — Ada splits her time 60/40 between two teams, each with one other person, and is away on the Tuesday
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        Guid cardsId, loansId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var cards = await OrganizationSeeder.SeedTeam(seedContext, "CARDS", "Cards", cancellationToken);
            var loans = await OrganizationSeeder.SeedTeam(seedContext, "LOANS", "Loans", cancellationToken);
            var ada = await OrganizationSeeder.SeedEmployee(seedContext, "E-1001", "ada@acme.example", cancellationToken);
            var grace = await OrganizationSeeder.SeedEmployee(seedContext, "E-1002", "grace@acme.example", cancellationToken);
            var hopper = await OrganizationSeeder.SeedEmployee(seedContext, "E-1003", "hopper@acme.example", cancellationToken);

            var adaCapacity = Allocate(ada.Id, new AllocationChange(cards.Id, null, LongBefore, null, 60), new AllocationChange(loans.Id, null, LongBefore, null, 40));
            adaCapacity.RecordTimeAway(Tuesday, Tuesday, 100, TimeAwayReason.PaidTimeOff, EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();

            await seedContext.EmployeeCapacities.AddRangeAsync(
            [
                adaCapacity,
                Allocate(grace.Id, new AllocationChange(cards.Id, null, LongBefore, null, 60)),
                Allocate(hopper.Id, new AllocationChange(loans.Id, null, LongBefore, null, 60)),
            ], cancellationToken);
            await seedContext.SaveChangesAsync(cancellationToken);

            (cardsId, loansId) = (cards.Id, loans.Id);
        }

        await using var context = _fixture.CreateContext();
        var handler = Handler(context);

        // Act
        var cardsAvailability = await handler.Handle(new GetTeamDayAvailabilityQuery(cardsId, Monday, Wednesday), cancellationToken);
        var loansAvailability = await handler.Handle(new GetTeamDayAvailabilityQuery(loansId, Monday, Wednesday), cancellationToken);

        // Assert — her time away takes her share of each team, and no other day moves
        cardsAvailability![Tuesday].Should().Be(new TeamDayAvailability(1.2m, 0.6m));
        cardsAvailability[Tuesday].Share.Should().Be(0.5m);
        loansAvailability![Tuesday].Should().Be(new TeamDayAvailability(1m, 0.6m));
        loansAvailability[Tuesday].Share.Should().Be(0.6m);
        cardsAvailability[Monday].Share.Should().Be(1m);
        loansAvailability[Wednesday].Share.Should().Be(1m);
    }

    private static EmployeeCapacity Allocate(Guid employeeId, params AllocationChange[] changes)
    {
        var capacity = EmployeeCapacity.Create(employeeId);
        capacity.ChangeAllocations(changes, EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
        return capacity;
    }

    private static GetTeamDayAvailabilityQueryHandler Handler(WaydDbContext context)
    {
        var settings = new Mock<ISettings<SchedulingSettings>>();
        settings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(new SchedulingSettings());

        var featureFlags = new Mock<IFeatureFlags>();
        featureFlags.Setup(f => f.IsEnabled(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        return new GetTeamDayAvailabilityQueryHandler(context, settings.Object, featureFlags.Object);
    }
}
