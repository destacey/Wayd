using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Settings;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Organization.Application.EmployeeCapacities.Queries;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// The capacity of many teams over many periods against a real SQL Server container.
/// </summary>
/// <remarks>
/// Its figures must be the ones the team capacity read and the team of teams rollup give for each period alone,
/// read in one go. Only a real provider shows that the reads translate and agree.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetTeamsCapacityByPeriodQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private static readonly CapacityPeriod FirstWeek = new(new LocalDate(2027, 2, 1), new LocalDate(2027, 2, 7));
    private static readonly CapacityPeriod SecondWeek = new(new LocalDate(2027, 2, 8), new LocalDate(2027, 2, 14));
    private static readonly LocalDate Holiday = new(2027, 2, 3);
    private static readonly LocalDate LongBefore = new(2026, 6, 1);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_GivesEachTeamWhatItsOwnCapacityReadGivesForEachPeriod()
    {
        // Arrange — Payments holds Cards from the second week. Ada is half on Payments and half on Cards as a
        // lead; Grace is on Cards in full and away for half of the 9th.
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        var defaultCalendarId = await _fixture.SeedHolidayCalendar(cancellationToken, Holiday);
        Guid paymentsId, cardsId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var payments = TeamOfTeams.Create("Payments", new TeamCode("PAY"), null, new LocalDate(2024, 1, 1), "UTC", EventActor.System, SqlServerDbContextFixture.FixedNow);
            await seedContext.AddAsync(payments, cancellationToken);
            var cards = await OrganizationSeeder.SeedTeam(seedContext, "CARDS", "Cards", cancellationToken);
            cards.AddTeamMembership(payments, new MembershipDateRange(SecondWeek.Start, null), EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
            await seedContext.SaveChangesAsync(cancellationToken);

            var ada = await OrganizationSeeder.SeedEmployee(seedContext, "E-1001", "ada@acme.example", cancellationToken);
            var grace = await OrganizationSeeder.SeedEmployee(seedContext, "E-1002", "grace@acme.example", cancellationToken);
            var lead = await OrganizationSeeder.SeedRole(seedContext, "Lead", cancellationToken);
            lead.SetCountsTowardDeliveryCapacity(false);

            var adaCapacity = EmployeeCapacity.Create(ada.Id);
            adaCapacity.ChangeAllocations(
            [
                new AllocationChange(payments.Id, null, LongBefore, null, 50),
                new AllocationChange(cards.Id, lead.Id, LongBefore, null, 50),
            ], EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();

            var graceCapacity = EmployeeCapacity.Create(grace.Id);
            graceCapacity.ChangeAllocations([new AllocationChange(cards.Id, null, LongBefore, null, 100)], EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
            graceCapacity.RecordTimeAway(new LocalDate(2027, 2, 9), new LocalDate(2027, 2, 9), 50, TimeAwayReason.Training, EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();

            await seedContext.EmployeeCapacities.AddRangeAsync([adaCapacity, graceCapacity], cancellationToken);
            await seedContext.SaveChangesAsync(cancellationToken);

            (paymentsId, cardsId) = (payments.Id, cards.Id);
        }

        await using var context = _fixture.CreateContext();
        var unknownId = Guid.NewGuid();

        // Act
        var byTeam = await Handler(context, defaultCalendarId).Handle(
            new GetTeamsCapacityByPeriodQuery([cardsId, paymentsId, unknownId], [FirstWeek, SecondWeek]),
            cancellationToken);

        // Assert
        byTeam.Should().NotBeNull();
        byTeam!.Keys.Should().BeEquivalentTo([cardsId, paymentsId]);

        var cardsCapacity = byTeam[cardsId];
        cardsCapacity.Select(c => c.Period).Should().Equal(FirstWeek, SecondWeek);
        foreach (var period in cardsCapacity)
        {
            var read = await CapacityHandler(context, defaultCalendarId).Handle(new GetTeamCapacityQuery(cardsId, TeamType.Team, period.Period.Start, period.Period.End), cancellationToken);
            (period.AllocatedPersonDays, period.DeliveryPersonDays, period.AwayPersonDays)
                .Should().Be((read!.AllocatedPersonDays, read.DeliveryPersonDays, read.AwayPersonDays));
        }

        // The second week: Grace's four and a half days and Ada's lead time on Cards
        cardsCapacity[1].Should().Be(new TeamPeriodCapacity(SecondWeek, 7m, 4.5m, 0.5m, true));

        var paymentsCapacity = byTeam[paymentsId];
        foreach (var period in paymentsCapacity)
        {
            var rollup = await RollupHandler(context, defaultCalendarId).Handle(new GetTeamOfTeamsCapacityRollupQuery(paymentsId, period.Period.Start, period.Period.End), cancellationToken);
            (period.AllocatedPersonDays, period.DeliveryPersonDays).Should().Be((rollup!.AllocatedPersonDays, rollup.DeliveryPersonDays));
        }

        // Cards adds to Payments only in the second week
        paymentsCapacity[0].Should().Be(new TeamPeriodCapacity(FirstWeek, 2m, 2m, 0m, true));
        paymentsCapacity[1].Should().Be(new TeamPeriodCapacity(SecondWeek, 9.5m, 7m, 0.5m, true));

        // A day off for Cards comes out of Cards and of Payments' rollup alike: 1.5 person-days on the 10th, 1 of them delivery
        var withDayOff = await Handler(context, defaultCalendarId).Handle(
            new GetTeamsCapacityByPeriodQuery(
                [cardsId, paymentsId],
                [SecondWeek],
                new Dictionary<Guid, IReadOnlyList<LocalDate>> { [cardsId] = [new LocalDate(2027, 2, 10)] }),
            cancellationToken);
        withDayOff![cardsId][0].Should().Be(new TeamPeriodCapacity(SecondWeek, 5.5m, 3.5m, 0.5m, true));
        withDayOff[paymentsId][0].Should().Be(new TeamPeriodCapacity(SecondWeek, 8m, 6m, 0.5m, true));
    }

    private static GetTeamsCapacityByPeriodQueryHandler Handler(WaydDbContext context, Guid? defaultCalendarId)
    {
        var featureFlags = new Mock<IFeatureFlags>();
        featureFlags.Setup(f => f.IsEnabled(FeatureFlags.Names.TeamCapacityManagement, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return new(context, Settings(defaultCalendarId), featureFlags.Object);
    }

    private static GetTeamCapacityQueryHandler CapacityHandler(WaydDbContext context, Guid? defaultCalendarId)
    {
        var principal = new Mock<ICurrentPrincipal>();
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(d => d.Now).Returns(SqlServerDbContextFixture.FixedNow);
        return new(context, new Mock<IDispatcher>().Object, principal.Object, Settings(defaultCalendarId), clock.Object);
    }

    private static GetTeamOfTeamsCapacityRollupQueryHandler RollupHandler(WaydDbContext context, Guid? defaultCalendarId) =>
        new(context, Settings(defaultCalendarId));

    private static ISettings<SchedulingSettings> Settings(Guid? defaultCalendarId)
    {
        var settings = new Mock<ISettings<SchedulingSettings>>();
        settings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(new SchedulingSettings { DefaultHolidayCalendarId = defaultCalendarId });
        return settings.Object;
    }
}
