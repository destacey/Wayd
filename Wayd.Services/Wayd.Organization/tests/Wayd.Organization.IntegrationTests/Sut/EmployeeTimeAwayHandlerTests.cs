using Wayd.Common.Domain.Settings;
using Wayd.Common.Application.SystemSettings;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Organization.Application.EmployeeCapacities;
using Wayd.Organization.Application.EmployeeCapacities.Commands;
using Wayd.Organization.Application.EmployeeCapacities.Queries;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// Employee time away and holiday calendars against a real SQL Server container.
/// </summary>
/// <remarks>
/// The time away sits behind a backing field beside the allocations, a removed stretch must be deleted with
/// no explicit remove, the reason is hidden inside the query, the manager chain is read off real rows, and a
/// change to time away must not save over a change made from the same record. Only a real context shows those.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class EmployeeTimeAwayHandlerTests(SqlServerDbContextFixture fixture)
{
    private static readonly LocalDate Feb1 = new(2027, 2, 1);
    private static readonly LocalDate Feb5 = new(2027, 2, 5);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Record_ByTheEmployee_PersistsTheTimeAwayAndKeepsTheReasonOutOfTheActivityLog()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = RecordHandler(context, Employee(seeded.EmployeeId));

        // Act
        var result = await handler.Handle(
            new RecordEmployeeTimeAwayCommand(seeded.EmployeeId, Feb1, Feb5, 50, TimeAwayReason.BusinessTravel),
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await using var assertContext = _fixture.CreateContext();
        var capacity = await assertContext.EmployeeCapacities.Include(c => c.TimeAway).SingleAsync(c => c.Id == seeded.EmployeeId, cancellationToken);
        capacity.Revision.Should().Be(1);
        var timeAway = capacity.TimeAway.Should().ContainSingle().Subject;
        timeAway.Id.Should().Be(result.Value);
        (timeAway.Start, timeAway.End, timeAway.PercentAway, timeAway.Reason).Should().Be((Feb1, Feb5, 50, TimeAwayReason.BusinessTravel));

        var entry = await assertContext.ActivityLogs.SingleAsync(a => a.AggregateId == seeded.EmployeeId && a.EventType == nameof(EmployeeTimeAwayRecordedEvent), cancellationToken);
        entry.AggregateType.Should().Be("Employee");
        entry.Payload.Should().Contain("\"percentAway\":50");
        entry.Payload.Should().NotContainEquivalentOf("reason").And.NotContainEquivalentOf("travel");
    }

    [Fact]
    public async Task Record_ByAManagerFurtherUpTheChain_Succeeds()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = RecordHandler(context, Employee(seeded.DirectorId));

        // Act
        var result = await handler.Handle(
            new RecordEmployeeTimeAwayCommand(seeded.EmployeeId, Feb1, Feb5, 100, TimeAwayReason.PaidTimeOff),
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await using var assertContext = _fixture.CreateContext();
        (await assertContext.EmployeeTimeAway.CountAsync(t => t.EmployeeId == seeded.EmployeeId, cancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task Record_ByAPeerWithoutTheAdministerPermission_FailsAndSavesNothing()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = RecordHandler(context, Employee(seeded.PeerId));

        // Act
        var result = await handler.Handle(
            new RecordEmployeeTimeAwayCommand(seeded.EmployeeId, Feb1, Feb5, 100, TimeAwayReason.PaidTimeOff),
            cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TimeAwayAuthorization.NotAllowedError);
        await using var assertContext = _fixture.CreateContext();
        (await assertContext.EmployeeCapacities.AnyAsync(c => c.Id == seeded.EmployeeId, cancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task Record_BesideAllocations_LeavesThemAsTheyWere()
    {
        // Arrange — fully allocated, so time away must not be read as allocation
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        await using (var seedContext = _fixture.CreateContext())
        {
            var allocated = EmployeeCapacity.Create(seeded.EmployeeId);
            allocated.ChangeAllocations([new AllocationChange(seeded.TeamId, null, Feb1, null, 100)], EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
            await seedContext.EmployeeCapacities.AddAsync(allocated, cancellationToken);
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        await using var context = _fixture.CreateContext();
        var handler = RecordHandler(context, Employee(seeded.EmployeeId));

        // Act
        var result = await handler.Handle(
            new RecordEmployeeTimeAwayCommand(seeded.EmployeeId, Feb1, Feb5, 100, TimeAwayReason.PaidTimeOff),
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await using var assertContext = _fixture.CreateContext();
        var capacity = await assertContext.EmployeeCapacities.Include(c => c.Allocations).Include(c => c.TimeAway).SingleAsync(c => c.Id == seeded.EmployeeId, cancellationToken);
        capacity.Allocations.Should().ContainSingle(a => a.TeamId == seeded.TeamId && a.Percent == 100 && a.End == null);
        capacity.TimeAway.Should().ContainSingle();
        capacity.Revision.Should().Be(2);
    }

    [Fact]
    public async Task ChangeThenRemove_UpdatesThenDeletesTheRow()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        var timeAwayId = await RecordAway(seeded.EmployeeId, Feb1, Feb5, 100, TimeAwayReason.PaidTimeOff, cancellationToken);

        // Act
        Result changed;
        await using (var changeContext = _fixture.CreateContext())
        {
            changed = await ChangeHandler(changeContext, Employee(seeded.ManagerId)).Handle(
                new ChangeEmployeeTimeAwayCommand(seeded.EmployeeId, timeAwayId, Feb1.PlusDays(1), Feb5, 50, TimeAwayReason.Training),
                cancellationToken);
        }

        EmployeeTimeAway afterChange;
        await using (var readContext = _fixture.CreateContext())
        {
            afterChange = await readContext.EmployeeTimeAway.SingleAsync(t => t.EmployeeId == seeded.EmployeeId, cancellationToken);
        }

        Result removed;
        await using (var removeContext = _fixture.CreateContext())
        {
            removed = await RemoveHandler(removeContext, Employee(seeded.EmployeeId)).Handle(
                new RemoveEmployeeTimeAwayCommand(seeded.EmployeeId, timeAwayId),
                cancellationToken);
        }

        // Assert
        changed.IsSuccess.Should().BeTrue(changed.IsFailure ? changed.Error : null);
        afterChange.Id.Should().Be(timeAwayId);
        (afterChange.Start, afterChange.End, afterChange.PercentAway, afterChange.Reason).Should().Be((Feb1.PlusDays(1), Feb5, 50, TimeAwayReason.Training));

        removed.IsSuccess.Should().BeTrue(removed.IsFailure ? removed.Error : null);
        await using var assertContext = _fixture.CreateContext();
        (await assertContext.EmployeeTimeAway.AnyAsync(t => t.EmployeeId == seeded.EmployeeId, cancellationToken)).Should().BeFalse();
        (await assertContext.EmployeeCapacities.SingleAsync(c => c.Id == seeded.EmployeeId, cancellationToken)).Revision.Should().Be(3);

        var changedEntry = await assertContext.ActivityLogs.SingleAsync(a => a.AggregateId == seeded.EmployeeId && a.EventType == nameof(EmployeeTimeAwayChangedEvent), cancellationToken);
        changedEntry.Payload.Should().Contain("\"reasonChanged\":true");
        changedEntry.Payload.Should().NotContainEquivalentOf("training").And.NotContainEquivalentOf("paidTimeOff");
    }

    [Fact]
    public async Task Save_OfTimeAwayWorkedOutFromARecordAnotherWriterChangedFirst_FailsAsChangedElsewhere()
    {
        // Arrange — each writer adds 60% of the same day, which together is more than the day
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        await RecordAway(seeded.EmployeeId, Feb5.PlusDays(10), Feb5.PlusDays(10), 100, TimeAwayReason.Other, cancellationToken);

        await using var firstContext = _fixture.CreateContext();
        await using var secondContext = _fixture.CreateContext();
        var (first, firstIsNew) = await EmployeeCapacityRecords.LoadOrCreate(firstContext, seeded.EmployeeId, CapacityPart.TimeAway, cancellationToken);
        var (second, secondIsNew) = await EmployeeCapacityRecords.LoadOrCreate(secondContext, seeded.EmployeeId, CapacityPart.TimeAway, cancellationToken);
        first.RecordTimeAway(Feb1, Feb1, 60, TimeAwayReason.Training, EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
        second.RecordTimeAway(Feb1, Feb1, 60, TimeAwayReason.PaidTimeOff, EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
        (await EmployeeCapacityRecords.Save(firstContext, first, firstIsNew, cancellationToken)).IsSuccess.Should().BeTrue();

        // Act
        var result = await EmployeeCapacityRecords.Save(secondContext, second, secondIsNew, cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(EmployeeCapacityRecords.ChangedElsewhereError);
        await using var assertContext = _fixture.CreateContext();
        (await assertContext.EmployeeTimeAway.Where(t => t.EmployeeId == seeded.EmployeeId && t.Start == Feb1).SumAsync(t => t.PercentAway, cancellationToken)).Should().Be(60);
    }

    [Fact]
    public async Task GetTimeAway_ShowsATeammateThatSomeoneIsAwayButNotWhy()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        await RecordAway(seeded.EmployeeId, Feb1, Feb5, 50, TimeAwayReason.BusinessTravel, cancellationToken);

        await using var context = _fixture.CreateContext();

        // Act
        var asTeammate = await new GetEmployeeTimeAwayQueryHandler(context, Employee(seeded.PeerId), SchedulingDefaults()).Handle(new GetEmployeeTimeAwayQuery(seeded.EmployeeId), cancellationToken);
        var asManager = await new GetEmployeeTimeAwayQueryHandler(context, Employee(seeded.ManagerId), SchedulingDefaults()).Handle(new GetEmployeeTimeAwayQuery(seeded.EmployeeId), cancellationToken);
        var asAdministrator = await new GetEmployeeTimeAwayQueryHandler(context, Administrator(seeded.OutsiderId), SchedulingDefaults()).Handle(new GetEmployeeTimeAwayQuery(seeded.EmployeeId), cancellationToken);

        // Assert
        asTeammate!.CanManage.Should().BeFalse();
        var seenByTeammate = asTeammate.Entries.Should().ContainSingle().Subject;
        (seenByTeammate.Start, seenByTeammate.End, seenByTeammate.PercentAway).Should().Be((Feb1, Feb5, 50));
        seenByTeammate.Reason.Should().BeNull();

        asManager!.CanManage.Should().BeTrue();
        asManager.Entries.Should().ContainSingle().Which.Reason.Should().Be(TimeAwayReason.BusinessTravel);

        asAdministrator!.CanManage.Should().BeTrue();
        asAdministrator.Entries.Should().ContainSingle().Which.Reason.Should().Be(TimeAwayReason.BusinessTravel);
    }

    [Fact]
    public async Task ChangeHolidayCalendar_PersistsTheCalendarAndTheReadReturnsIt()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        var calendarId = await _fixture.SeedHolidayCalendar(cancellationToken, Feb1);

        // Act
        Result result;
        await using (var context = _fixture.CreateContext())
        {
            var handler = new ChangeEmployeeHolidayCalendarCommandHandler(context, CurrentUser(), Administrator(seeded.OutsiderId), Clock(), NullLogger<ChangeEmployeeHolidayCalendarCommandHandler>.Instance);
            result = await handler.Handle(new ChangeEmployeeHolidayCalendarCommand(seeded.EmployeeId, calendarId), cancellationToken);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await using var assertContext = _fixture.CreateContext();
        (await assertContext.EmployeeCapacities.SingleAsync(c => c.Id == seeded.EmployeeId, cancellationToken)).HolidayCalendarId.Should().Be(calendarId);
        var read = await new GetEmployeeTimeAwayQueryHandler(assertContext, Employee(seeded.PeerId), SchedulingDefaults()).Handle(new GetEmployeeTimeAwayQuery(seeded.EmployeeId), cancellationToken);
        read!.HolidayCalendar!.Id.Should().Be(calendarId);
        (await assertContext.ActivityLogs.AnyAsync(a => a.AggregateId == seeded.EmployeeId && a.EventType == nameof(EmployeeHolidayCalendarChangedEvent), cancellationToken))
            .Should().BeTrue();
    }

    /// <summary>
    /// A director, a manager reporting to them, an employee and a peer reporting to the manager, and someone
    /// who reports to nobody and has nobody reporting to them.
    /// </summary>
    private async Task<(Guid EmployeeId, Guid PeerId, Guid ManagerId, Guid DirectorId, Guid OutsiderId, Guid TeamId)> Seed(CancellationToken cancellationToken)
    {
        await _fixture.ResetOrganizationData(cancellationToken);

        await using var context = _fixture.CreateContext();
        var director = await OrganizationSeeder.SeedEmployee(context, "E-1000", "director@acme.example", cancellationToken);
        var manager = await OrganizationSeeder.SeedEmployee(context, "E-1001", "manager@acme.example", cancellationToken, director.Id);
        var employee = await OrganizationSeeder.SeedEmployee(context, "E-1002", "ada@acme.example", cancellationToken, manager.Id);
        var peer = await OrganizationSeeder.SeedEmployee(context, "E-1003", "grace@acme.example", cancellationToken, manager.Id);
        var outsider = await OrganizationSeeder.SeedEmployee(context, "E-1004", "hopper@acme.example", cancellationToken);
        var team = await OrganizationSeeder.SeedTeam(context, "CARDS", "Cards", cancellationToken);

        return (employee.Id, peer.Id, manager.Id, director.Id, outsider.Id, team.Id);
    }

    /// <summary>Records time away as the employee themselves.</summary>
    private async Task<Guid> RecordAway(Guid employeeId, LocalDate start, LocalDate end, int percentAway, TimeAwayReason reason, CancellationToken cancellationToken)
    {
        await using var context = _fixture.CreateContext();
        var result = await RecordHandler(context, Employee(employeeId)).Handle(new RecordEmployeeTimeAwayCommand(employeeId, start, end, percentAway, reason), cancellationToken);
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        return result.Value;
    }

    private static RecordEmployeeTimeAwayCommandHandler RecordHandler(WaydDbContext context, ICurrentPrincipal principal) =>
        new(context, CurrentUser(), principal, Clock(), NullLogger<RecordEmployeeTimeAwayCommandHandler>.Instance);

    private static ChangeEmployeeTimeAwayCommandHandler ChangeHandler(WaydDbContext context, ICurrentPrincipal principal) =>
        new(context, CurrentUser(), principal, Clock(), NullLogger<ChangeEmployeeTimeAwayCommandHandler>.Instance);

    private static RemoveEmployeeTimeAwayCommandHandler RemoveHandler(WaydDbContext context, ICurrentPrincipal principal) =>
        new(context, CurrentUser(), principal, Clock(), NullLogger<RemoveEmployeeTimeAwayCommandHandler>.Instance);

    /// <summary>A caller linked to the employee, holding no permission.</summary>
    private static ICurrentPrincipal Employee(Guid employeeId)
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);
        return principal.Object;
    }

    /// <summary>A caller holding the Administer Employee Time Away permission, linked to <paramref name="employeeId"/>.</summary>
    private static ICurrentPrincipal Administrator(Guid employeeId)
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.HasPermission(TimeAwayAuthorization.AdministratorPermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);
        return principal.Object;
    }

    private static ISettings<SchedulingSettings> SchedulingDefaults()
    {
        var settings = new Mock<ISettings<SchedulingSettings>>();
        settings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(new SchedulingSettings());
        return settings.Object;
    }

    private static IDateTimeProvider Clock()
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(d => d.Now).Returns(SqlServerDbContextFixture.FixedNow);
        return clock.Object;
    }

    private static ICurrentUser CurrentUser()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns("user-1");
        return currentUser.Object;
    }
}
