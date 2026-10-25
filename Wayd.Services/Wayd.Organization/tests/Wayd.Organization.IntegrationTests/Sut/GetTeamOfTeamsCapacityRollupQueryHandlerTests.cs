using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Settings;
using Wayd.Organization.Application.EmployeeCapacities.Dtos;
using Wayd.Organization.Application.EmployeeCapacities.Queries;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// A team of teams' capacity rollup against a real SQL Server container.
/// </summary>
/// <remarks>
/// The read takes the hierarchy from the dated team memberships, each team's working week from its operating
/// model, and the allocations of every team in the tree in one query. Only a real provider shows that those
/// translate.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetTeamOfTeamsCapacityRollupQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    // Monday to the Sunday two weeks on. With the holiday out, a Monday-to-Friday week works the 1st, 2nd,
    // 4th and 5th, then the 8th to the 12th.
    private static readonly LocalDate From = new(2027, 2, 1);
    private static readonly LocalDate To = new(2027, 2, 14);
    private static readonly LocalDate Holiday = new(2027, 2, 3);
    private static readonly LocalDate SecondWeek = new(2027, 2, 8);
    private static readonly LocalDate LongBefore = new(2026, 6, 1);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_RollsUpEachTeamOverItsOwnWorkingDaysForTheDaysItWasInTheHierarchy()
    {
        // Arrange — Payments holds Cards, which left and came back, and Lending, which joined in the second
        // week and holds Loans. Ada is allocated to Payments itself and to Cards.
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        var defaultCalendarId = await _fixture.SeedHolidayCalendar(cancellationToken, Holiday);
        Guid paymentsId, adaId, graceId, hopperId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var payments = await SeedTeamOfTeams(seedContext, "PAY", "Payments", cancellationToken);
            var lending = await SeedTeamOfTeams(seedContext, "LEND", "Lending", cancellationToken);
            var cards = await OrganizationSeeder.SeedTeam(seedContext, "CARDS", "Cards", cancellationToken);
            var outside = await OrganizationSeeder.SeedTeam(seedContext, "OUT", "Outside", cancellationToken);

            var mondayToThursday = WorkingWeek.Create([IsoDayOfWeek.Monday, IsoDayOfWeek.Tuesday, IsoDayOfWeek.Wednesday, IsoDayOfWeek.Thursday]).Value;
            var loans = Team.Create("Loans", new TeamCode("LOANS"), null, SqlServerDbContextFixture.FixedNow.InUtc().Date, Methodology.Kanban, SizingMethod.Count, "UTC", 1, mondayToThursday, EventActor.System, SqlServerDbContextFixture.FixedNow);
            await seedContext.Teams.AddAsync(loans, cancellationToken);
            await seedContext.SaveChangesAsync(cancellationToken);

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

            (paymentsId, adaId, graceId, hopperId) = (payments.Id, ada.Id, grace.Id, hopper.Id);
        }

        await using var context = _fixture.CreateContext();
        var handler = Handler(context, defaultCalendarId);

        // Act
        var rollup = await handler.Handle(new GetTeamOfTeamsCapacityRollupQuery(paymentsId, From, To), cancellationToken);
        var direct = await DirectHandler(context, defaultCalendarId).Handle(new GetTeamCapacityQuery(paymentsId, TeamType.TeamOfTeams, From, To), cancellationToken);

        // Assert — Ada's half of Payments' nine working days is the only direct time, and it is what the
        // direct read, which the same page shows beside the rollup, makes of it
        rollup.Should().NotBeNull();
        rollup!.DirectAllocatedPersonDays.Should().Be(4.5m);
        rollup.DirectDeliveryPersonDays.Should().Be(4.5m);
        direct!.AllocatedPersonDays.Should().Be(rollup.DirectAllocatedPersonDays);
        direct.DeliveryPersonDays.Should().Be(rollup.DirectDeliveryPersonDays);
        rollup.Children.Select(c => c.Team.Code).Should().Equal("CARDS", "LEND");

        // Cards counts for the 1st and 2nd, then the 11th and 12th: four working days
        var cardsNode = rollup.Children[0];
        cardsNode.Team.Type.Should().Be("Team");
        cardsNode.Periods.Select(p => (p.Start, p.End)).Should().Equal(
            (From, new LocalDate(2027, 2, 2)),
            (new LocalDate(2027, 2, 11), To));
        cardsNode.WorkingDays.Should().Be(4);
        cardsNode.Children.Should().BeEmpty();
        var adaOnCards = cardsNode.People.Should().ContainSingle().Subject;
        adaOnCards.Employee.Id.Should().Be(adaId);
        adaOnCards.AllocatedPersonDays.Should().Be(2m);
        adaOnCards.Allocations.Should().ContainSingle().Which.PersonDays.Should().Be(2m);
        Figures(cardsNode).Should().Be((2m, 2m, 2m, 2m));

        // Lending counts from the 8th: five default working days, and its lead is not delivery capacity
        var lendingNode = rollup.Children[1];
        lendingNode.Team.Type.Should().Be("Team of Teams");
        lendingNode.Periods.Select(p => (p.Start, p.End)).Should().Equal((SecondWeek, To));
        lendingNode.WorkingDays.Should().Be(5);
        var hopperOnLending = lendingNode.People.Should().ContainSingle().Subject;
        hopperOnLending.Employee.Id.Should().Be(hopperId);
        hopperOnLending.AllocatedPersonDays.Should().Be(2m);
        hopperOnLending.DeliveryPersonDays.Should().Be(0m);
        Figures(lendingNode).Should().Be((6m, 4m, 2m, 0m));

        // Loans has been in Lending all along but counts only while Lending was in Payments, over its own
        // Monday-to-Thursday week: the 8th to the 11th
        var loansNode = lendingNode.Children.Should().ContainSingle().Subject;
        loansNode.Team.Code.Should().Be("LOANS");
        loansNode.Periods.Select(p => (p.Start, p.End)).Should().Equal((SecondWeek, To));
        loansNode.WorkingDays.Should().Be(4);
        loansNode.People.Should().ContainSingle().Which.Employee.Id.Should().Be(graceId);
        Figures(loansNode).Should().Be((4m, 4m, 4m, 4m));

        // Ada is one person though she has time at two levels, and the team outside the hierarchy adds nothing
        rollup.People.Should().Be(3);
        rollup.AllocatedPersonDays.Should().Be(12.5m);
        rollup.DeliveryPersonDays.Should().Be(10.5m);
        rollup.Roles.Select(r => (r.Role?.Name, r.CountsTowardDeliveryCapacity, r.PersonDays)).Should().Equal(
            ("Engineer", true, 6m),
            ("Lead", false, 2m),
            (null, true, 4.5m));
    }

    [Fact]
    public async Task Handle_ListsAChildWithNothingAllocatedAndLeavesOutOneNotInTheHierarchyInTheRange()
    {
        // Arrange — Cards is in Payments with no one allocated; Loans left before the range and has someone
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        var defaultCalendarId = await _fixture.SeedHolidayCalendar(cancellationToken, Holiday);
        Guid paymentsId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var payments = await SeedTeamOfTeams(seedContext, "PAY", "Payments", cancellationToken);
            var cards = await OrganizationSeeder.SeedTeam(seedContext, "CARDS", "Cards", cancellationToken);
            var loans = await OrganizationSeeder.SeedTeam(seedContext, "LOANS", "Loans", cancellationToken);
            Join(cards, payments, LongBefore, null);
            Join(loans, payments, LongBefore, From.PlusDays(-1));
            await seedContext.SaveChangesAsync(cancellationToken);

            var ada = await OrganizationSeeder.SeedEmployee(seedContext, "E-1001", "ada@acme.example", cancellationToken);
            await seedContext.EmployeeCapacities.AddAsync(Allocate(ada.Id, new AllocationChange(loans.Id, null, LongBefore, null, 100)), cancellationToken);
            await seedContext.SaveChangesAsync(cancellationToken);

            paymentsId = payments.Id;
        }

        await using var context = _fixture.CreateContext();
        var handler = Handler(context, defaultCalendarId);

        // Act
        var rollup = await handler.Handle(new GetTeamOfTeamsCapacityRollupQuery(paymentsId, From, To), cancellationToken);

        // Assert
        var cardsNode = rollup!.Children.Should().ContainSingle().Subject;
        cardsNode.Team.Code.Should().Be("CARDS");
        cardsNode.WorkingDays.Should().Be(9);
        cardsNode.People.Should().BeEmpty();
        Figures(cardsNode).Should().Be((0m, 0m, 0m, 0m));
        rollup.People.Should().Be(0);
        rollup.AllocatedPersonDays.Should().Be(0m);
        rollup.Roles.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_LeavesOutAnAllocationThatFallsOnlyOnDaysTheTeamWasOutOfTheHierarchy()
    {
        // Arrange — Cards joins in the second week; Ada's time on it ended in the first, Grace's runs on
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        var defaultCalendarId = await _fixture.SeedHolidayCalendar(cancellationToken, Holiday);
        Guid paymentsId, graceId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var payments = await SeedTeamOfTeams(seedContext, "PAY", "Payments", cancellationToken);
            var cards = await OrganizationSeeder.SeedTeam(seedContext, "CARDS", "Cards", cancellationToken);
            Join(cards, payments, SecondWeek, null);
            await seedContext.SaveChangesAsync(cancellationToken);

            var ada = await OrganizationSeeder.SeedEmployee(seedContext, "E-1001", "ada@acme.example", cancellationToken);
            var grace = await OrganizationSeeder.SeedEmployee(seedContext, "E-1002", "grace@acme.example", cancellationToken);
            await seedContext.EmployeeCapacities.AddRangeAsync(
            [
                Allocate(ada.Id, new AllocationChange(cards.Id, null, LongBefore, new LocalDate(2027, 2, 5), 100)),
                Allocate(grace.Id, new AllocationChange(cards.Id, null, new LocalDate(2027, 2, 4), null, 100)),
            ], cancellationToken);
            await seedContext.SaveChangesAsync(cancellationToken);

            (paymentsId, graceId) = (payments.Id, grace.Id);
        }

        await using var context = _fixture.CreateContext();
        var handler = Handler(context, defaultCalendarId);

        // Act
        var rollup = await handler.Handle(new GetTeamOfTeamsCapacityRollupQuery(paymentsId, From, To), cancellationToken);

        // Assert — Grace's two days before Cards joined are not counted either
        var cardsNode = rollup!.Children.Should().ContainSingle().Subject;
        var graceOnCards = cardsNode.People.Should().ContainSingle().Subject;
        graceOnCards.Employee.Id.Should().Be(graceId);
        graceOnCards.AllocatedPersonDays.Should().Be(5m);
        rollup.People.Should().Be(1);
        rollup.AllocatedPersonDays.Should().Be(5m);
    }

    [Fact]
    public async Task Handle_ForATeamOfTeamsThatDoesNotExist_ReturnsNull()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var handler = Handler(context);

        // Act
        var rollup = await handler.Handle(new GetTeamOfTeamsCapacityRollupQuery(Guid.NewGuid(), From, To), cancellationToken);

        // Assert
        rollup.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ForATeam_ReturnsNull()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        var defaultCalendarId = await _fixture.SeedHolidayCalendar(cancellationToken, Holiday);
        Guid teamId;
        await using (var seedContext = _fixture.CreateContext())
        {
            teamId = (await OrganizationSeeder.SeedTeam(seedContext, "CARDS", "Cards", cancellationToken)).Id;
        }

        await using var context = _fixture.CreateContext();
        var handler = Handler(context, defaultCalendarId);

        // Act
        var rollup = await handler.Handle(new GetTeamOfTeamsCapacityRollupQuery(teamId, From, To), cancellationToken);

        // Assert
        rollup.Should().BeNull();
    }

    /// <summary>A node's total allocated, total delivery, direct allocated and direct delivery person-days.</summary>
    private static (decimal Allocated, decimal Delivery, decimal DirectAllocated, decimal DirectDelivery) Figures(TeamCapacityRollupNodeDto node) =>
        (node.AllocatedPersonDays, node.DeliveryPersonDays, node.DirectAllocatedPersonDays, node.DirectDeliveryPersonDays);

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

    private static GetTeamOfTeamsCapacityRollupQueryHandler Handler(DbContext context, Guid? defaultCalendarId = null) =>
        new((Wayd.Infrastructure.Persistence.Context.WaydDbContext)context, Settings(defaultCalendarId));

    /// <summary>The direct capacity read, over the same holidays and working week as <see cref="Handler"/>.</summary>
    private static GetTeamCapacityQueryHandler DirectHandler(DbContext context, Guid? defaultCalendarId)
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        principal.Setup(p => p.HoldsPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(d => d.Now).Returns(SqlServerDbContextFixture.FixedNow);

        return new GetTeamCapacityQueryHandler(
            (Wayd.Infrastructure.Persistence.Context.WaydDbContext)context,
            new Mock<IDispatcher>().Object,
            principal.Object,
            Settings(defaultCalendarId),
            clock.Object);
    }

    private static ISettings<SchedulingSettings> Settings(Guid? defaultCalendarId)
    {
        var settings = new Mock<ISettings<SchedulingSettings>>();
        settings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(new SchedulingSettings { DefaultHolidayCalendarId = defaultCalendarId });
        return settings.Object;
    }
}
