using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Settings;
using Wayd.Organization.Application.EmployeeCapacities;
using Wayd.Organization.Application.EmployeeCapacities.Queries;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// The capacity report against a real SQL Server container.
/// </summary>
/// <remarks>
/// The read takes the hierarchy from the dated team memberships, and every allocation of everyone with time in it
/// to find their time outside. Only a real provider shows that those translate.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetTeamCapacityReportQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    // Monday to the Sunday two weeks on, with a holiday on the first Wednesday.
    private static readonly LocalDate From = new(2027, 2, 1);
    private static readonly LocalDate To = new(2027, 2, 14);
    private static readonly LocalDate Holiday = new(2027, 2, 3);
    private static readonly LocalDate SecondWeek = new(2027, 2, 8);
    private static readonly LocalDate LongBefore = new(2026, 6, 1);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_ForATeamOfTeamsHoldingAnother_CountsEveryTeamBeneathBothForTheDaysEachWasThere()
    {
        // Arrange — Payments holds Cards, which left and came back, and Lending, which joined in the second
        // week and holds Loans. Ada is allocated to Payments itself and to Cards; Hopper also to a team outside.
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        var defaultCalendarId = await _fixture.SeedHolidayCalendar(cancellationToken, Holiday);
        Guid paymentsId, adaId, hopperId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var payments = await SeedTeamOfTeams(seedContext, "PAY", "Payments", cancellationToken);
            var lending = await SeedTeamOfTeams(seedContext, "LEND", "Lending", cancellationToken);
            var cards = await OrganizationSeeder.SeedTeam(seedContext, "CARDS", "Cards", cancellationToken);
            var loans = await OrganizationSeeder.SeedTeam(seedContext, "LOANS", "Loans", cancellationToken);
            var outside = await OrganizationSeeder.SeedTeam(seedContext, "OUT", "Outside", cancellationToken);

            Join(cards, payments, LongBefore, new LocalDate(2027, 2, 2));
            Join(cards, payments, new LocalDate(2027, 2, 11), null);
            Join(lending, payments, SecondWeek, null);
            Join(loans, lending, LongBefore, null);
            await seedContext.SaveChangesAsync(cancellationToken);

            var ada = await OrganizationSeeder.SeedEmployee(seedContext, "E-1001", "ada@acme.example", cancellationToken);
            var grace = await OrganizationSeeder.SeedEmployee(seedContext, "E-1002", "grace@acme.example", cancellationToken);
            var hopper = await OrganizationSeeder.SeedEmployee(seedContext, "E-1003", "hopper@acme.example", cancellationToken);
            var engineer = await OrganizationSeeder.SeedRole(seedContext, "Engineer", cancellationToken);
            var lead = await OrganizationSeeder.SeedRole(seedContext, "Lead", cancellationToken);
            lead.SetCountsTowardDeliveryCapacity(false);

            await seedContext.EmployeeCapacities.AddRangeAsync(
            [
                Allocate(ada.Id, new AllocationChange(payments.Id, null, LongBefore, null, 50), new AllocationChange(cards.Id, engineer.Id, LongBefore, null, 50)),
                Allocate(grace.Id, new AllocationChange(loans.Id, engineer.Id, LongBefore, null, 100)),
                Allocate(hopper.Id, new AllocationChange(lending.Id, lead.Id, LongBefore, null, 40), new AllocationChange(outside.Id, null, LongBefore, null, 20)),
            ], cancellationToken);
            await seedContext.SaveChangesAsync(cancellationToken);

            (paymentsId, adaId, hopperId) = (payments.Id, ada.Id, hopper.Id);
        }

        await using var context = _fixture.CreateContext();

        // Act
        var report = await Handler(context, defaultCalendarId).Handle(
            new GetTeamCapacityReportQuery(paymentsId, TeamType.TeamOfTeams, From, To, CapacityReportInterval.Week), cancellationToken);

        // Assert — every team beneath both teams of teams, each beneath its parent
        report.Should().NotBeNull();
        report!.Teams.Select(t => (t.NodeId, t.ParentNodeId, t.Team.Code)).Should().Equal(
            (0, (int?)null, "PAY"),
            (1, 0, "CARDS"),
            (2, 0, "LEND"),
            (3, 2, "LOANS"));

        // Ada on Payments: half of ten days, one of them a holiday; on Cards: half of four days. Grace on Loans
        // for the second week, Hopper leading Lending for the second week.
        report.Lines.Select(l => (l.NodeId, l.Role?.Name, l.Total.AllocatedPersonDays, l.Total.HolidayPersonDays, l.Total.AvailablePersonDays, l.Total.DeliveryPersonDays)).Should().Equal(
            (0, null, 5m, 0.5m, 4.5m, 4.5m),
            (1, "Engineer", 2m, 0m, 2m, 2m),
            (2, "Lead", 2m, 0m, 2m, 0m),
            (3, "Engineer", 5m, 0m, 5m, 5m));

        // What is left agrees with the rollup the team of teams page shows
        report.Total.AllocatedPersonDays.Should().Be(14m);
        report.Total.HolidayPersonDays.Should().Be(0.5m);
        report.Total.AvailablePersonDays.Should().Be(13.5m);
        report.Total.DeliveryPersonDays.Should().Be(11.5m);
        report.Lines.Sum(l => l.Total.AvailablePersonDays).Should().Be(report.Total.AvailablePersonDays);
        report.Roles.Sum(r => r.Total.AvailablePersonDays).Should().Be(report.Total.AvailablePersonDays);
        report.Buckets.Should().HaveCount(2);
        report.Lines.Should().OnlyContain(l => l.Buckets.Sum(b => b.AllocatedPersonDays) == l.Total.AllocatedPersonDays);

        // Ada's Cards time on the six weekdays Cards was out counts outside; Hopper's time outside and on
        // Lending's first week count outside, and the rest of Hopper's time is unallocated
        report.People.Should().HaveCount(3);
        var adaInReport = report.People.Single(p => p.Employee.Id == adaId);
        adaInReport.OutsidePersonDays.Should().Be(3m);
        adaInReport.UnallocatedPersonDays.Should().Be(0m);
        var hopperInReport = report.People.Single(p => p.Employee.Id == hopperId);
        hopperInReport.OutsidePersonDays.Should().Be(4m);
        hopperInReport.UnallocatedPersonDays.Should().Be(4m);
        hopperInReport.OutsideBuckets.Should().Equal(3m, 1m);
    }

    [Fact]
    public async Task Handle_ForATeam_MeasuresAPartTimersTimeAwayByMonthAndEndsTheUnallocatedTimeOfSomeoneWhoLeft()
    {
        // Arrange — from the last week of January to mid February. Ada works 4 hours of the system's 8, in full
        // on Cards, and is away half of the second Tuesday. Grace was on Cards until the first Friday, when she
        // was deactivated.
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        var defaultCalendarId = await _fixture.SeedHolidayCalendar(cancellationToken, Holiday);
        var from = new LocalDate(2027, 1, 25);
        Guid cardsId, adaId, graceId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var cards = await OrganizationSeeder.SeedTeam(seedContext, "CARDS", "Cards", cancellationToken);
            var ada = await OrganizationSeeder.SeedEmployee(seedContext, "E-1001", "ada@acme.example", cancellationToken);
            var grace = await OrganizationSeeder.SeedEmployee(seedContext, "E-1002", "grace@acme.example", cancellationToken);
            grace.Deactivate(EmployeeActivatableArgs.Create(EventActor.System, SqlServerDbContextFixture.FixedNow)).IsSuccess.Should().BeTrue();

            var adaCapacity = Allocate(ada.Id, new AllocationChange(cards.Id, null, LongBefore, null, 100));
            adaCapacity.ChangeWorkingDayHours(4m, EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
            adaCapacity.RecordTimeAway(new LocalDate(2027, 2, 9), new LocalDate(2027, 2, 9), 50, TimeAwayReason.Training, EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
            await seedContext.EmployeeCapacities.AddRangeAsync(
            [
                adaCapacity,
                Allocate(grace.Id, new AllocationChange(cards.Id, null, LongBefore, new LocalDate(2027, 2, 5), 100)),
            ], cancellationToken);
            await seedContext.SaveChangesAsync(cancellationToken);

            (cardsId, adaId, graceId) = (cards.Id, ada.Id, grace.Id);
        }

        await using var context = _fixture.CreateContext();

        // Act
        var report = await Handler(context, defaultCalendarId).Handle(
            new GetTeamCapacityReportQuery(cardsId, TeamType.Team, from, To, CapacityReportInterval.Month), cancellationToken);

        // Assert — fifteen weekdays, one a holiday. Ada's are half a person-day each.
        report.Should().NotBeNull();
        report!.Teams.Should().ContainSingle().Which.Team.Code.Should().Be("CARDS");
        report.Buckets.Select(b => (b.Start, b.End)).Should().Equal((from, new LocalDate(2027, 1, 31)), (new LocalDate(2027, 2, 1), To));

        var adaLine = report.Lines.Single(l => l.EmployeeId == adaId);
        adaLine.Total.Should().BeEquivalentTo(new { AllocatedPersonDays = 7.5m, HolidayPersonDays = 0.5m, TimeAwayPersonDays = 0.25m, AvailablePersonDays = 6.75m });
        adaLine.Buckets.Select(b => b.AllocatedPersonDays).Should().Equal(2.5m, 5m);

        // Grace's ten days to the first Friday; she left then, so the days after are not unallocated
        var graceLine = report.Lines.Single(l => l.EmployeeId == graceId);
        graceLine.Total.Should().BeEquivalentTo(new { AllocatedPersonDays = 10m, HolidayPersonDays = 1m, AvailablePersonDays = 9m });
        report.People.Single(p => p.Employee.Id == graceId).UnallocatedPersonDays.Should().Be(0m);
        report.People.Single(p => p.Employee.Id == adaId).UnallocatedPersonDays.Should().Be(0m);
    }

    [Fact]
    public async Task Handle_ForSomeoneWhoLeftWithTimeAfterTheRange_CountsTheGapInTheRangeAsUnallocated()
    {
        // Arrange — Grace, now inactive, was on Cards to the first Friday and again for a week after the range,
        // so she was still employed in the second week of the range
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        Guid cardsId, graceId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var cards = await OrganizationSeeder.SeedTeam(seedContext, "CARDS", "Cards", cancellationToken);
            var grace = await OrganizationSeeder.SeedEmployee(seedContext, "E-1002", "grace@acme.example", cancellationToken);
            grace.Deactivate(EmployeeActivatableArgs.Create(EventActor.System, SqlServerDbContextFixture.FixedNow)).IsSuccess.Should().BeTrue();

            await seedContext.EmployeeCapacities.AddAsync(
                Allocate(grace.Id,
                    new AllocationChange(cards.Id, null, LongBefore, new LocalDate(2027, 2, 5), 100),
                    new AllocationChange(cards.Id, null, new LocalDate(2027, 2, 22), new LocalDate(2027, 2, 26), 100)),
                cancellationToken);
            await seedContext.SaveChangesAsync(cancellationToken);

            (cardsId, graceId) = (cards.Id, grace.Id);
        }

        await using var context = _fixture.CreateContext();

        // Act
        var report = await Handler(context, null).Handle(new GetTeamCapacityReportQuery(cardsId, TeamType.Team, From, To, null), cancellationToken);

        // Assert — the five weekdays of the second week
        report.Should().NotBeNull();
        report!.People.Single(p => p.Employee.Id == graceId).UnallocatedPersonDays.Should().Be(5m);
    }

    [Fact]
    public async Task Handle_ForATeamOfTheOtherType_ReturnsNull()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        Guid teamId;
        await using (var seedContext = _fixture.CreateContext())
        {
            teamId = (await OrganizationSeeder.SeedTeam(seedContext, "CARDS", "Cards", cancellationToken)).Id;
        }

        await using var context = _fixture.CreateContext();

        // Act
        var report = await Handler(context, null).Handle(new GetTeamCapacityReportQuery(teamId, TeamType.TeamOfTeams, From, To, null), cancellationToken);

        // Assert
        report.Should().BeNull();
    }

    private static async Task<TeamOfTeams> SeedTeamOfTeams(DbContext context, string code, string name, CancellationToken cancellationToken)
    {
        var teamOfTeams = TeamOfTeams.Create(name, new TeamCode(code), null, new LocalDate(2024, 1, 1), "UTC", EventActor.System, SqlServerDbContextFixture.FixedNow);
        await context.AddAsync(teamOfTeams, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return teamOfTeams;
    }

    private static void Join(BaseTeam child, TeamOfTeams parent, LocalDate start, LocalDate? end) =>
        child.AddTeamMembership(parent, new MembershipDateRange(start, end), EventActor.System, SqlServerDbContextFixture.FixedNow)
            .IsSuccess.Should().BeTrue();

    private static EmployeeCapacity Allocate(Guid employeeId, params AllocationChange[] changes)
    {
        var capacity = EmployeeCapacity.Create(employeeId);
        capacity.ChangeAllocations(changes, EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
        return capacity;
    }

    private static GetTeamCapacityReportQueryHandler Handler(DbContext context, Guid? defaultCalendarId)
    {
        var settings = new Mock<ISettings<SchedulingSettings>>();
        settings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(new SchedulingSettings { DefaultHolidayCalendarId = defaultCalendarId });
        return new((Wayd.Infrastructure.Persistence.Context.WaydDbContext)context, settings.Object);
    }
}
