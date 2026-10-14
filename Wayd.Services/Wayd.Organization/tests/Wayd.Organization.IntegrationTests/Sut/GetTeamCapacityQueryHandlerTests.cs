using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Settings;
using Wayd.Organization.Application.EmployeeCapacities.Queries;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// A team's capacity against a real SQL Server container.
/// </summary>
/// <remarks>
/// The read joins allocations to employees and, where there is one, a role, and reads the team's working week
/// off its operating model. Only a real provider shows that those translate.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetTeamCapacityQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    // Monday to the Sunday two weeks on: ten days in a Monday-to-Friday week.
    private static readonly LocalDate From = new(2027, 2, 1);
    private static readonly LocalDate To = new(2027, 2, 14);
    private static readonly LocalDate Holiday = new(2027, 2, 3);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_ReturnsEachPersonsPersonDaysOverTheTeamsWorkingDays()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        var defaultCalendarId = await _fixture.SeedHolidayCalendar(cancellationToken, Holiday);
        Guid teamId, adaId, graceId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var team = await OrganizationSeeder.SeedTeam(seedContext, "CARDS", "Cards", cancellationToken);
            var otherTeam = await OrganizationSeeder.SeedTeam(seedContext, "LOANS", "Loans", cancellationToken);
            var ada = await OrganizationSeeder.SeedEmployee(seedContext, "E-1001", "ada@acme.example", cancellationToken);
            var grace = await OrganizationSeeder.SeedEmployee(seedContext, "E-1002", "grace@acme.example", cancellationToken);
            var engineer = await OrganizationSeeder.SeedRole(seedContext, "Engineer", cancellationToken);
            var lead = await OrganizationSeeder.SeedRole(seedContext, "Lead", cancellationToken);
            lead.SetCountsTowardDeliveryCapacity(false);

            var adaCapacity = EmployeeCapacity.Create(ada.Id);
            adaCapacity.ChangeAllocations(
            [
                new AllocationChange(team.Id, engineer.Id, new LocalDate(2027, 1, 1), null, 50),
                new AllocationChange(team.Id, lead.Id, new LocalDate(2027, 1, 1), null, 30),
                new AllocationChange(otherTeam.Id, null, new LocalDate(2027, 1, 1), null, 20),
            ], EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();

            var graceCapacity = EmployeeCapacity.Create(grace.Id);
            graceCapacity.ChangeAllocations(
                [new AllocationChange(team.Id, null, new LocalDate(2027, 2, 8), null, 100)],
                EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();

            await seedContext.EmployeeCapacities.AddRangeAsync([adaCapacity, graceCapacity], cancellationToken);
            await seedContext.SaveChangesAsync(cancellationToken);

            (teamId, adaId, graceId) = (team.Id, ada.Id, grace.Id);
        }

        await using var context = _fixture.CreateContext();
        var handler = Handler(context, defaultCalendarId);

        // Act
        var capacity = await handler.Handle(new GetTeamCapacityQuery(teamId, TeamType.Team, From, To), cancellationToken);

        // Assert — nine working days once the holiday is out
        capacity.Should().NotBeNull();
        capacity!.WorkingDays.Should().Be(9);
        capacity.CanManage.Should().BeTrue();
        capacity.People.Should().HaveCount(2);

        var adaCapacityRow = capacity.People.Single(p => p.Employee.Id == adaId);
        adaCapacityRow.Employee.Name.Should().Be("Ada Lovelace");
        adaCapacityRow.AllocatedPersonDays.Should().Be(7.2m);
        adaCapacityRow.DeliveryPersonDays.Should().Be(4.5m);
        adaCapacityRow.ElsewherePersonDays.Should().Be(1.8m);
        adaCapacityRow.Allocations.Should().HaveCount(2);
        adaCapacityRow.Allocations.Single(a => a.Role!.Name == "Lead").CountsTowardDeliveryCapacity.Should().BeFalse();
        adaCapacityRow.Allocations.Single(a => a.Role!.Name == "Lead").PersonDays.Should().Be(2.7m);
        adaCapacityRow.Allocations.Single(a => a.Role!.Name == "Engineer").PersonDays.Should().Be(4.5m);
        var adaElsewhere = adaCapacityRow.ElsewhereAllocations.Should().ContainSingle().Subject;
        adaElsewhere.Team.Code.Should().Be("LOANS");
        adaElsewhere.Percent.Should().Be(20);
        adaElsewhere.CanManage.Should().BeTrue();

        var graceCapacityRow = capacity.People.Single(p => p.Employee.Id == graceId);
        graceCapacityRow.AllocatedPersonDays.Should().Be(5m);
        graceCapacityRow.DeliveryPersonDays.Should().Be(5m);
        graceCapacityRow.ElsewherePersonDays.Should().Be(0m);
        graceCapacityRow.Allocations.Should().ContainSingle().Which.Role.Should().BeNull();
        graceCapacityRow.ElsewhereAllocations.Should().BeEmpty();

        capacity.Roles.Select(r => (r.Role?.Name, r.CountsTowardDeliveryCapacity, r.PersonDays)).Should().Equal(
            ("Engineer", true, 4.5m),
            ("Lead", false, 2.7m),
            (null, true, 5m));

        capacity.AllocatedPersonDays.Should().Be(12.2m);
        capacity.DeliveryPersonDays.Should().Be(9.5m);
    }

    [Fact]
    public async Task Handle_TakesEachPersonsHolidaysAndTimeAwayOutOfTheirPersonDays()
    {
        // Arrange — Ada takes a calendar of her own, off on the 9th and not on the team's holiday; Grace is
        // away for half of the 10th and 11th and takes the team's holidays
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        var defaultCalendarId = await _fixture.SeedHolidayCalendar(cancellationToken, Holiday);
        var adasCalendarId = await _fixture.SeedHolidayCalendar(cancellationToken, new LocalDate(2027, 2, 9));
        Guid teamId, adaId, graceId;

        await using (var seedContext = _fixture.CreateContext())
        {
            var team = await OrganizationSeeder.SeedTeam(seedContext, "CARDS", "Cards", cancellationToken);
            var otherTeam = await OrganizationSeeder.SeedTeam(seedContext, "LOANS", "Loans", cancellationToken);
            var ada = await OrganizationSeeder.SeedEmployee(seedContext, "E-1001", "ada@acme.example", cancellationToken);
            var grace = await OrganizationSeeder.SeedEmployee(seedContext, "E-1002", "grace@acme.example", cancellationToken);

            var adaCapacity = EmployeeCapacity.Create(ada.Id);
            adaCapacity.ChangeAllocations([new AllocationChange(team.Id, null, new LocalDate(2027, 1, 1), null, 100)], EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
            adaCapacity.ChangeHolidayCalendar(adasCalendarId, EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();

            var graceCapacity = EmployeeCapacity.Create(grace.Id);
            graceCapacity.ChangeAllocations(
            [
                new AllocationChange(team.Id, null, new LocalDate(2027, 1, 1), null, 60),
                new AllocationChange(otherTeam.Id, null, new LocalDate(2027, 1, 1), null, 40),
            ], EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
            graceCapacity.RecordTimeAway(new LocalDate(2027, 2, 10), new LocalDate(2027, 2, 11), 50, TimeAwayReason.Training, EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();

            await seedContext.EmployeeCapacities.AddRangeAsync([adaCapacity, graceCapacity], cancellationToken);
            await seedContext.SaveChangesAsync(cancellationToken);

            (teamId, adaId, graceId) = (team.Id, ada.Id, grace.Id);
        }

        await using var context = _fixture.CreateContext();
        var handler = Handler(context, defaultCalendarId);

        // Act
        var capacity = await handler.Handle(new GetTeamCapacityQuery(teamId, TeamType.Team, From, To), cancellationToken);

        // Assert — the team still counts nine working days of its own
        capacity!.WorkingDays.Should().Be(9);

        // Ada works the team's holiday and is off on her own: nine of the ten weekdays
        var adaRow = capacity.People.Single(p => p.Employee.Id == adaId);
        adaRow.AllocatedPersonDays.Should().Be(9m);
        adaRow.AwayPersonDays.Should().Be(0m);
        adaRow.TimeAway.Should().BeEmpty();

        // Grace has nine days less two half days, at 60% here and 40% elsewhere
        var graceRow = capacity.People.Single(p => p.Employee.Id == graceId);
        graceRow.AllocatedPersonDays.Should().Be(4.8m);
        graceRow.ElsewherePersonDays.Should().Be(3.2m);
        graceRow.AwayPersonDays.Should().Be(0.6m);
        graceRow.Allocations.Should().ContainSingle().Which.PersonDays.Should().Be(4.8m);
        var away = graceRow.TimeAway.Should().ContainSingle().Subject;
        (away.Start, away.End, away.PercentAway).Should().Be((new LocalDate(2027, 2, 10), new LocalDate(2027, 2, 11), 50));

        capacity.AllocatedPersonDays.Should().Be(13.8m);
        capacity.AwayPersonDays.Should().Be(0.6m);
    }

    [Fact]
    public async Task Handle_ForATeamThatDoesNotExist_ReturnsNull()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var handler = Handler(context);

        // Act
        var capacity = await handler.Handle(new GetTeamCapacityQuery(Guid.NewGuid(), TeamType.Team, From, To), cancellationToken);

        // Assert
        capacity.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ForATeamAskedForAsATeamOfTeams_ReturnsNull()
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
        var capacity = await handler.Handle(new GetTeamCapacityQuery(teamId, TeamType.TeamOfTeams, From, To), cancellationToken);

        // Assert
        capacity.Should().BeNull();
    }

    private static GetTeamCapacityQueryHandler Handler(DbContext context, Guid? defaultCalendarId = null)
    {
        var dispatcher = new Mock<IDispatcher>();

        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var settings = new Mock<ISettings<SchedulingSettings>>();
        settings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(new SchedulingSettings { DefaultHolidayCalendarId = defaultCalendarId });

        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(d => d.Now).Returns(SqlServerDbContextFixture.FixedNow);

        return new GetTeamCapacityQueryHandler(
            (Wayd.Infrastructure.Persistence.Context.WaydDbContext)context,
            dispatcher.Object,
            principal.Object,
            settings.Object,
            clock.Object);
    }
}
