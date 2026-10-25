using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Common.Domain.Settings;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Organization.Application.EmployeeCapacities.Commands;
using Wayd.Organization.Application.EmployeeCapacities.Dtos;
using Wayd.Organization.Application.EmployeeCapacities.Queries;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// A team's capacity plan against a real SQL Server container, and the allocation changes made from it.
/// </summary>
/// <remarks>
/// A plan edit is an allocation change over a week or a day, and the days around it must be left as they were
/// once saved and read back. Only a real context shows that.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetTeamCapacityPlanQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    // A two-week sprint, Monday to Friday, and the sprint after it.
    private static readonly LocalDate SprintStart = new(2027, 2, 1);
    private static readonly LocalDate SprintEnd = new(2027, 2, 12);
    private static readonly LocalDate NextSprintStart = new(2027, 2, 15);
    private static readonly LocalDate NextSprintEnd = new(2027, 2, 26);
    private static readonly LocalDate Jan1 = new(2027, 1, 1);
    private static readonly LocalDate Holiday = new(2027, 2, 3);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_ReturnsEachPersonsTimeDayByDayBesideTheirHolidaysTimeAwayAndTimeElsewhere()
    {
        // Arrange — Ada is 60% here as an engineer and 40% on another team, and away for half of the 9th
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        var defaultCalendarId = await _fixture.SeedHolidayCalendar(cancellationToken, Holiday);
        Guid teamId, adaId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var team = await OrganizationSeeder.SeedTeam(seedContext, "CARDS", "Cards", cancellationToken);
            var otherTeam = await OrganizationSeeder.SeedTeam(seedContext, "LOANS", "Loans", cancellationToken);
            var ada = await OrganizationSeeder.SeedEmployee(seedContext, "E-1001", "ada@acme.example", cancellationToken);
            var engineer = await OrganizationSeeder.SeedRole(seedContext, "Engineer", cancellationToken);

            var capacity = EmployeeCapacity.Create(ada.Id);
            capacity.ChangeAllocations(
            [
                new AllocationChange(team.Id, engineer.Id, Jan1, null, 60),
                new AllocationChange(otherTeam.Id, null, Jan1, null, 40),
            ], EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
            capacity.RecordTimeAway(new LocalDate(2027, 2, 9), new LocalDate(2027, 2, 9), 50, TimeAwayReason.Training, EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();

            await seedContext.EmployeeCapacities.AddAsync(capacity, cancellationToken);
            await seedContext.SaveChangesAsync(cancellationToken);

            (teamId, adaId) = (team.Id, ada.Id);
        }

        await using var context = _fixture.CreateContext();

        // Act
        var plan = await PlanHandler(context, defaultCalendarId).Handle(new GetTeamCapacityPlanQuery(teamId, TeamType.Team, SprintStart, SprintEnd), cancellationToken);

        // Assert — ten weekdays, holiday included; nine days there less half a day, at 60%
        plan.Should().NotBeNull();
        plan!.Days.Should().HaveCount(10).And.BeInAscendingOrder();
        plan.Days.Should().Contain(Holiday).And.NotContain(new LocalDate(2027, 2, 6));
        plan.Capacity.WorkingDays.Should().Be(9);
        plan.Capacity.AllocatedPersonDays.Should().Be(5.1m);

        var person = plan.People.Should().ContainSingle().Subject;
        person.EmployeeId.Should().Be(adaId);
        person.CanManageTimeAway.Should().BeTrue();
        plan.Capacity.WorkingDayHours.Should().Be(8m);

        var row = person.Rows.Should().ContainSingle().Subject;
        row.Role!.Name.Should().Be("Engineer");
        row.CountsTowardDeliveryCapacity.Should().BeTrue();
        row.Days.Should().HaveCount(10).And.OnlyContain(d => d.Percent == 60);
        row.Days.Single(d => d.Date == Holiday).PersonDays.Should().Be(0m);
        row.Days.Single(d => d.Date == new LocalDate(2027, 2, 9)).PersonDays.Should().Be(0.3m);
        row.Days.Single(d => d.Date == SprintStart).PersonDays.Should().Be(0.6m);
        row.Days.Sum(d => d.PersonDays).Should().Be(plan.Capacity.People.Single().AllocatedPersonDays);

        person.Days.Should().HaveCount(10).And.OnlyContain(d => d.ElsewherePercent == 40);
        person.Days.Single(d => d.Date == Holiday).IsHoliday.Should().BeTrue();
        person.Days.Single(d => d.Date == new LocalDate(2027, 2, 9)).PercentAway.Should().Be(50);
        person.Days.Count(d => d.IsHoliday || d.PercentAway > 0).Should().Be(2);
    }

    [Fact]
    public async Task Handle_ForAPartTimer_CountsTheirShareOfTheSystemsWorkingDayAndGivesTheirHours()
    {
        // Arrange — Ada works 4-hour days, in full here; Grace has the system's 8
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        var defaultCalendarId = await _fixture.SeedHolidayCalendar(cancellationToken);
        Guid teamId, adaId, graceId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var team = await OrganizationSeeder.SeedTeam(seedContext, "CARDS", "Cards", cancellationToken);
            var ada = await OrganizationSeeder.SeedEmployee(seedContext, "E-1001", "ada@acme.example", cancellationToken);
            var grace = await OrganizationSeeder.SeedEmployee(seedContext, "E-1002", "grace@acme.example", cancellationToken);

            var adaCapacity = EmployeeCapacity.Create(ada.Id);
            adaCapacity.ChangeAllocations([new AllocationChange(team.Id, null, Jan1, null, 100)], EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
            adaCapacity.ChangeWorkingDayHours(4m, EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();

            var graceCapacity = EmployeeCapacity.Create(grace.Id);
            graceCapacity.ChangeAllocations([new AllocationChange(team.Id, null, Jan1, null, 100)], EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();

            await seedContext.EmployeeCapacities.AddRangeAsync([adaCapacity, graceCapacity], cancellationToken);
            await seedContext.SaveChangesAsync(cancellationToken);

            (teamId, adaId, graceId) = (team.Id, ada.Id, grace.Id);
        }

        await using var context = _fixture.CreateContext();

        // Act
        var plan = await PlanHandler(context, defaultCalendarId).Handle(new GetTeamCapacityPlanQuery(teamId, TeamType.Team, SprintStart, SprintEnd), cancellationToken);

        // Assert — ten half days and ten whole ones
        plan.Should().NotBeNull();
        plan!.Capacity.WorkingDayHours.Should().Be(8m);
        plan.Capacity.AllocatedPersonDays.Should().Be(15m);

        var adaLine = plan.Capacity.People.Single(p => p.Employee.Id == adaId);
        adaLine.WorkingDayHours.Should().Be(4m);
        adaLine.AllocatedPersonDays.Should().Be(5m);
        plan.Capacity.People.Single(p => p.Employee.Id == graceId).WorkingDayHours.Should().Be(8m);

        var adaRow = plan.People.Single(p => p.EmployeeId == adaId).Rows.Should().ContainSingle().Subject;
        adaRow.Days.Should().OnlyContain(d => d.Percent == 100 && d.PersonDays == 0.5m);
    }

    [Fact]
    public async Task Handle_ForAWorkingDayThatDoesNotDivideTheSystems_DaysAddUpToThePersonDays()
    {
        // Arrange — a 7.5-hour system day; Ada works 7-hour days, at 35% then 65% in no role and 30% as an
        // engineer, and is away for a quarter of the 9th
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        var defaultCalendarId = await _fixture.SeedHolidayCalendar(cancellationToken, Holiday);
        Guid teamId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var team = await OrganizationSeeder.SeedTeam(seedContext, "CARDS", "Cards", cancellationToken);
            var ada = await OrganizationSeeder.SeedEmployee(seedContext, "E-1001", "ada@acme.example", cancellationToken);
            var engineer = await OrganizationSeeder.SeedRole(seedContext, "Engineer", cancellationToken);

            var capacity = EmployeeCapacity.Create(ada.Id);
            capacity.ChangeAllocations(
            [
                new AllocationChange(team.Id, null, Jan1, new LocalDate(2027, 2, 5), 35),
                new AllocationChange(team.Id, null, new LocalDate(2027, 2, 6), null, 65),
                new AllocationChange(team.Id, engineer.Id, Jan1, null, 30),
            ], EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
            capacity.RecordTimeAway(new LocalDate(2027, 2, 9), new LocalDate(2027, 2, 9), 25, TimeAwayReason.Training, EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
            capacity.ChangeWorkingDayHours(7m, EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();

            await seedContext.EmployeeCapacities.AddAsync(capacity, cancellationToken);
            await seedContext.SaveChangesAsync(cancellationToken);

            teamId = team.Id;
        }

        await using var context = _fixture.CreateContext();
        var handler = new GetTeamCapacityPlanQueryHandler(context, new Mock<IDispatcher>().Object, Administrator(), Settings(defaultCalendarId, 7.5m), Clock());

        // Act
        var plan = await handler.Handle(new GetTeamCapacityPlanQuery(teamId, TeamType.Team, SprintStart, SprintEnd), cancellationToken);

        // Assert
        plan.Should().NotBeNull();
        var person = plan!.Capacity.People.Should().ContainSingle().Subject;
        person.WorkingDayHours.Should().Be(7m);
        plan.People.Single().Rows.Sum(r => r.Days.Sum(d => d.PersonDays)).Should().Be(person.AllocatedPersonDays);
        plan.Capacity.Roles.Sum(r => r.PersonDays).Should().Be(plan.Capacity.AllocatedPersonDays);
    }

    [Fact]
    public async Task Handle_HasTheSameCapacityAsTheTeamCapacityRead()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        var defaultCalendarId = await _fixture.SeedHolidayCalendar(cancellationToken, Holiday);
        Guid teamId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var team = await OrganizationSeeder.SeedTeam(seedContext, "CARDS", "Cards", cancellationToken);
            var ada = await OrganizationSeeder.SeedEmployee(seedContext, "E-1001", "ada@acme.example", cancellationToken);
            var grace = await OrganizationSeeder.SeedEmployee(seedContext, "E-1002", "grace@acme.example", cancellationToken);
            var lead = await OrganizationSeeder.SeedRole(seedContext, "Lead", cancellationToken);
            lead.SetCountsTowardDeliveryCapacity(false);

            var adaCapacity = EmployeeCapacity.Create(ada.Id);
            adaCapacity.ChangeAllocations(
            [
                new AllocationChange(team.Id, null, Jan1, null, 70),
                new AllocationChange(team.Id, lead.Id, Jan1, null, 30),
            ], EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();

            var graceCapacity = EmployeeCapacity.Create(grace.Id);
            graceCapacity.ChangeAllocations([new AllocationChange(team.Id, null, new LocalDate(2027, 2, 8), null, 100)], EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();

            await seedContext.EmployeeCapacities.AddRangeAsync([adaCapacity, graceCapacity], cancellationToken);
            await seedContext.SaveChangesAsync(cancellationToken);
            teamId = team.Id;
        }

        await using var context = _fixture.CreateContext();

        // Act
        var plan = await PlanHandler(context, defaultCalendarId).Handle(new GetTeamCapacityPlanQuery(teamId, TeamType.Team, SprintStart, SprintEnd), cancellationToken);
        var capacity = await CapacityHandler(context, defaultCalendarId).Handle(new GetTeamCapacityQuery(teamId, TeamType.Team, SprintStart, SprintEnd), cancellationToken);

        // Assert
        plan!.Capacity.Should().BeEquivalentTo(capacity);
        plan.People.Select(p => p.EmployeeId).Should().Equal(capacity!.People.Select(p => p.Employee.Id));
        plan.People.SelectMany(p => p.Rows).Sum(r => r.Days.Sum(d => d.PersonDays)).Should().Be(capacity.AllocatedPersonDays);
        plan.People.SelectMany(p => p.Rows).Where(r => r.CountsTowardDeliveryCapacity).Sum(r => r.Days.Sum(d => d.PersonDays)).Should().Be(capacity.DeliveryPersonDays);
    }

    [Fact]
    public async Task ChangeOverTheSprintsTwoWeeks_LeavesTheAllocationAfterTheSprintUnchanged()
    {
        // Arrange — 50% from long before, with no end
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        var defaultCalendarId = await _fixture.SeedHolidayCalendar(cancellationToken);
        Guid teamId, adaId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var team = await OrganizationSeeder.SeedTeam(seedContext, "CARDS", "Cards", cancellationToken);
            var ada = await OrganizationSeeder.SeedEmployee(seedContext, "E-1001", "ada@acme.example", cancellationToken);

            var capacity = EmployeeCapacity.Create(ada.Id);
            capacity.ChangeAllocations([new AllocationChange(team.Id, null, Jan1, null, 50)], EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
            await seedContext.EmployeeCapacities.AddAsync(capacity, cancellationToken);
            await seedContext.SaveChangesAsync(cancellationToken);

            (teamId, adaId) = (team.Id, ada.Id);
        }

        // Act — 100% for the sprint's first week and 0% for its second, each week as the plan's cell covers it
        await using (var changeContext = _fixture.CreateContext())
        {
            var result = await ChangeHandler(changeContext).Handle(
                new ChangeEmployeeAllocationsCommand(adaId,
                [
                    new AllocationChange(teamId, null, SprintStart, SprintStart.PlusDays(6), 100),
                    new AllocationChange(teamId, null, SprintStart.PlusDays(7), SprintEnd, 0),
                ]),
                cancellationToken);
            result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        }

        // Assert — the sprint reads 100% then nothing; before and after it, 50% as it was
        await using var context = _fixture.CreateContext();
        var handler = PlanHandler(context, defaultCalendarId);

        var sprint = await handler.Handle(new GetTeamCapacityPlanQuery(teamId, TeamType.Team, SprintStart, SprintEnd), cancellationToken);
        sprint!.People.Single().Rows.Single().Days.Should().HaveCount(5)
            .And.OnlyContain(d => d.Percent == 100 && d.Date <= SprintStart.PlusDays(6));

        var nextSprint = await handler.Handle(new GetTeamCapacityPlanQuery(teamId, TeamType.Team, NextSprintStart, NextSprintEnd), cancellationToken);
        nextSprint!.People.Single().Rows.Single().Days.Should().HaveCount(10).And.OnlyContain(d => d.Percent == 50);

        var allocations = await context.EmployeeTeamAllocations
            .Where(a => a.EmployeeId == adaId)
            .OrderBy(a => a.Start)
            .Select(a => new { a.Start, a.End, a.Percent })
            .ToListAsync(cancellationToken);
        allocations.Should().Equal(
            new { Start = Jan1, End = (LocalDate?)new LocalDate(2027, 1, 31), Percent = 50 },
            new { Start = SprintStart, End = (LocalDate?)SprintStart.PlusDays(6), Percent = 100 },
            new { Start = SprintEnd.PlusDays(1), End = (LocalDate?)null, Percent = 50 });
    }

    [Fact]
    public async Task Handle_ForTheNextSprint_HasTheMemberWhoseTimeRunsOnAndNotTheOneAddedForASprint()
    {
        // Arrange — Ada is added with no end, as a member is; Grace for the sprint's dates, as a non-member is
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        var defaultCalendarId = await _fixture.SeedHolidayCalendar(cancellationToken);
        Guid teamId, adaId, graceId;

        await using (var seedContext = _fixture.CreateContext())
        {
            teamId = (await OrganizationSeeder.SeedTeam(seedContext, "CARDS", "Cards", cancellationToken)).Id;
            adaId = (await OrganizationSeeder.SeedEmployee(seedContext, "E-1001", "ada@acme.example", cancellationToken)).Id;
            graceId = (await OrganizationSeeder.SeedEmployee(seedContext, "E-1002", "grace@acme.example", cancellationToken)).Id;
        }

        await using (var changeContext = _fixture.CreateContext())
        {
            var handler = ChangeHandler(changeContext);
            (await handler.Handle(new ChangeEmployeeAllocationsCommand(adaId, [new AllocationChange(teamId, null, SprintStart, null, 100)]), cancellationToken))
                .IsSuccess.Should().BeTrue();
            (await handler.Handle(new ChangeEmployeeAllocationsCommand(graceId, [new AllocationChange(teamId, null, SprintStart, SprintEnd, 100)]), cancellationToken))
                .IsSuccess.Should().BeTrue();
        }

        await using var context = _fixture.CreateContext();
        var planHandler = PlanHandler(context, defaultCalendarId);

        // Act
        var sprint = await planHandler.Handle(new GetTeamCapacityPlanQuery(teamId, TeamType.Team, SprintStart, SprintEnd), cancellationToken);
        var nextSprint = await planHandler.Handle(new GetTeamCapacityPlanQuery(teamId, TeamType.Team, NextSprintStart, NextSprintEnd), cancellationToken);

        // Assert
        sprint!.People.Select(p => p.EmployeeId).Should().BeEquivalentTo([adaId, graceId]);
        nextSprint!.People.Select(p => p.EmployeeId).Should().Equal(adaId);
        nextSprint.Capacity.AllocatedPersonDays.Should().Be(10m);
    }

    [Fact]
    public async Task Handle_WithTeamDaysOff_TakesTheirShareOfEveryonesTimeAndListsThem()
    {
        // Arrange — Ada in full; the team is off the first Thursday and half of the Friday
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        var defaultCalendarId = await _fixture.SeedHolidayCalendar(cancellationToken);
        Guid teamId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var team = await OrganizationSeeder.SeedTeam(seedContext, "CARDS", "Cards", cancellationToken);
            var ada = await OrganizationSeeder.SeedEmployee(seedContext, "E-1001", "ada@acme.example", cancellationToken);
            var capacity = EmployeeCapacity.Create(ada.Id);
            capacity.ChangeAllocations([new AllocationChange(team.Id, null, Jan1, null, 100)], EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
            await seedContext.EmployeeCapacities.AddAsync(capacity, cancellationToken);
            await seedContext.SaveChangesAsync(cancellationToken);
            teamId = team.Id;
        }

        var thursday = new LocalDate(2027, 2, 4);
        var friday = new LocalDate(2027, 2, 5);
        TeamDayOff[] daysOff = [new(thursday), new(friday, 50)];
        await using var context = _fixture.CreateContext();

        // Act
        var plan = await PlanHandler(context, defaultCalendarId).Handle(
            new GetTeamCapacityPlanQuery(teamId, TeamType.Team, SprintStart, SprintEnd, daysOff), cancellationToken);

        // Assert — the half day is still a working day, worth half
        plan!.TeamDaysOff.Should().Equal(
            new TeamDayOffDto { Date = thursday, PercentOff = 100 },
            new TeamDayOffDto { Date = friday, PercentOff = 50 });
        plan.Days.Should().HaveCount(10);
        plan.Capacity.WorkingDays.Should().Be(9);
        plan.Capacity.AllocatedPersonDays.Should().Be(8.5m);
        var days = plan.People.Single().Rows.Single().Days;
        days.Should().HaveCount(10).And.OnlyContain(d => d.Percent == 100);
        days.Single(d => d.Date == thursday).PersonDays.Should().Be(0m);
        days.Single(d => d.Date == friday).PersonDays.Should().Be(0.5m);
    }

    [Fact]
    public async Task Handle_ForATeamThatDoesNotExist_ReturnsNull()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();

        // Act
        var plan = await PlanHandler(context).Handle(new GetTeamCapacityPlanQuery(Guid.NewGuid(), TeamType.Team, SprintStart, SprintEnd), cancellationToken);

        // Assert
        plan.Should().BeNull();
    }

    private static GetTeamCapacityPlanQueryHandler PlanHandler(WaydDbContext context, Guid? defaultCalendarId = null) =>
        new(context, new Mock<IDispatcher>().Object, Administrator(), Settings(defaultCalendarId), Clock());

    private static GetTeamCapacityQueryHandler CapacityHandler(WaydDbContext context, Guid? defaultCalendarId) =>
        new(context, new Mock<IDispatcher>().Object, Administrator(), Settings(defaultCalendarId), Clock());

    private static ChangeEmployeeAllocationsCommandHandler ChangeHandler(WaydDbContext context)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns("user-1");
        return new(context, new Mock<IDispatcher>().Object, currentUser.Object, Administrator(), Clock(), NullLogger<ChangeEmployeeAllocationsCommandHandler>.Instance);
    }

    /// <summary>A caller with every permission, linked to no employee.</summary>
    private static ICurrentPrincipal Administrator()
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        principal.Setup(p => p.HoldsPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);
        return principal.Object;
    }

    private static ISettings<SchedulingSettings> Settings(Guid? defaultCalendarId, decimal workingDayHours = 8m)
    {
        var settings = new Mock<ISettings<SchedulingSettings>>();
        settings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(new SchedulingSettings { DefaultHolidayCalendarId = defaultCalendarId, WorkingDayHours = workingDayHours });
        return settings.Object;
    }

    private static IDateTimeProvider Clock()
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(d => d.Now).Returns(SqlServerDbContextFixture.FixedNow);
        return clock.Object;
    }
}
