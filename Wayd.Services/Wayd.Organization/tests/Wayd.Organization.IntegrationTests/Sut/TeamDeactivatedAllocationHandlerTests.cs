using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Organization.Application.EmployeeCapacities.EventHandlers;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// Ending allocations to a deactivated team against a real SQL Server container.
/// </summary>
/// <remarks>
/// The handler saves each employee on their own and recovers from a concurrency conflict on one of them by
/// clearing the change tracker and loading again. Only a real context has a concurrency token to conflict on.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class TeamDeactivatedAllocationHandlerTests(SqlServerDbContextFixture fixture)
{
    // The fixture's FixedNow, and so each seeded team's active date, is 2026-01-15.
    private static readonly LocalDate Jan20 = new(2026, 1, 20);
    private static readonly LocalDate Jan31 = new(2026, 1, 31);
    private static readonly LocalDate Feb1 = new(2026, 2, 1);
    private static readonly LocalDate Mar31 = new(2026, 3, 31);
    private static readonly LocalDate May1 = new(2026, 5, 1);
    private static readonly Instant DeactivatedAt = SqlServerDbContextFixture.FixedNow.Plus(Duration.FromHours(1));

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task TeamDeactivated_EndsOpenAllocationsOnTheInactiveDateAndRemovesLaterOnes()
    {
        // Arrange — one person with earlier, open and later time on the team and time on another, and a second person
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        await Allocate(seeded.FirstEmployeeId, cancellationToken,
            new AllocationChange(seeded.TeamAId, null, Jan20, Jan31, 30),
            new AllocationChange(seeded.TeamAId, null, Feb1, null, 60),
            new AllocationChange(seeded.TeamAId, seeded.RoleId, May1, null, 20),
            new AllocationChange(seeded.TeamBId, null, Feb1, null, 20));
        await Allocate(seeded.SecondEmployeeId, cancellationToken, new AllocationChange(seeded.TeamAId, null, Feb1, null, 100));
        var @event = await Deactivate(seeded.TeamAId, Mar31, DeactivatedAt, cancellationToken);

        await using var context = _fixture.CreateContext();

        // Act
        await Handler(context).Handle(@event, cancellationToken);

        // Assert
        await using var assertContext = _fixture.CreateContext();
        var first = await assertContext.EmployeeTeamAllocations.Where(a => a.EmployeeId == seeded.FirstEmployeeId).ToListAsync(cancellationToken);
        first.Select(Details).Should().BeEquivalentTo(
        [
            new EmployeeAllocationDetails(seeded.TeamAId, null, Jan20, Jan31, 30),
            new EmployeeAllocationDetails(seeded.TeamAId, null, Feb1, Mar31, 60),
            new EmployeeAllocationDetails(seeded.TeamBId, null, Feb1, null, 20),
        ]);
        var second = await assertContext.EmployeeTeamAllocations.SingleAsync(a => a.EmployeeId == seeded.SecondEmployeeId, cancellationToken);
        Details(second).Should().Be(new EmployeeAllocationDetails(seeded.TeamAId, null, Feb1, Mar31, 100));
        (await ChangesRecorded(assertContext, seeded.FirstEmployeeId, cancellationToken)).Should().Be(2);
        (await ChangesRecorded(assertContext, seeded.SecondEmployeeId, cancellationToken)).Should().Be(2);
    }

    [Fact]
    public async Task TeamDeactivated_RecordsEachChangeAgainstWhoeverDeactivatedTheTeamAndWhen()
    {
        // Arrange — the second employee deactivates the team an hour after the first was allocated to it
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        await Allocate(seeded.FirstEmployeeId, cancellationToken, new AllocationChange(seeded.TeamAId, null, Feb1, null, 60));
        var deactivatedBy = EventActor.User("user-7", seeded.SecondEmployeeId);
        var @event = await Deactivate(seeded.TeamAId, Mar31, DeactivatedAt, cancellationToken, deactivatedBy);

        await using var context = _fixture.CreateContext();

        // Act
        await Handler(context).Handle(@event, cancellationToken);

        // Assert
        await using var assertContext = _fixture.CreateContext();
        var entry = await assertContext.ActivityLogs
            .Where(a => a.AggregateId == seeded.FirstEmployeeId && a.EventType == nameof(EmployeeAllocationsChangedEvent))
            .OrderByDescending(a => a.Timestamp)
            .FirstAsync(cancellationToken);
        entry.Timestamp.Should().Be(DeactivatedAt);
        entry.UserId.Should().Be("user-7");
        entry.EmployeeId.Should().Be(seeded.SecondEmployeeId);
    }

    [Fact]
    public async Task TeamDeactivated_LetsAPersonWhoWasFullyOnTheTeamBeAllocatedElsewhereFromTheNextDay()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        await Allocate(seeded.FirstEmployeeId, cancellationToken, new AllocationChange(seeded.TeamAId, null, Feb1, null, 100));
        var @event = await Deactivate(seeded.TeamAId, Mar31, DeactivatedAt, cancellationToken);

        await using (var context = _fixture.CreateContext())
        {
            await Handler(context).Handle(@event, cancellationToken);
        }

        await using var changeContext = _fixture.CreateContext();
        var capacity = await changeContext.EmployeeCapacities.Include(c => c.Allocations).SingleAsync(c => c.Id == seeded.FirstEmployeeId, cancellationToken);

        // Act
        var result = capacity.ChangeAllocations([new AllocationChange(seeded.TeamBId, null, Mar31.PlusDays(1), null, 100)], EventActor.System, SqlServerDbContextFixture.FixedNow);
        await changeContext.SaveChangesAsync(cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await using var assertContext = _fixture.CreateContext();
        (await assertContext.EmployeeTeamAllocations.CountAsync(a => a.EmployeeId == seeded.FirstEmployeeId, cancellationToken)).Should().Be(2);
    }

    [Fact]
    public async Task TeamDeactivated_DeliveredTwice_ChangesNothingTheSecondTime()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        await Allocate(seeded.FirstEmployeeId, cancellationToken, new AllocationChange(seeded.TeamAId, null, Feb1, null, 60));
        var @event = await Deactivate(seeded.TeamAId, Mar31, DeactivatedAt, cancellationToken);

        await using (var firstContext = _fixture.CreateContext())
        {
            await Handler(firstContext).Handle(@event, cancellationToken);
        }

        await using var context = _fixture.CreateContext();

        // Act
        await Handler(context).Handle(@event, cancellationToken);

        // Assert
        await using var assertContext = _fixture.CreateContext();
        var capacity = await assertContext.EmployeeCapacities.Include(c => c.Allocations).SingleAsync(c => c.Id == seeded.FirstEmployeeId, cancellationToken);
        capacity.Revision.Should().Be(2);
        capacity.Allocations.Should().ContainSingle().Which.End.Should().Be(Mar31);
        (await ChangesRecorded(assertContext, seeded.FirstEmployeeId, cancellationToken)).Should().Be(2);
    }

    [Fact]
    public async Task TeamDeactivated_DeliveredAfterTheTeamWasReactivated_ChangesNothing()
    {
        // Arrange — deactivated, reactivated an hour later, then allocated
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        var @event = await Deactivate(seeded.TeamAId, Mar31, DeactivatedAt, cancellationToken);
        await Activate(seeded.TeamAId, DeactivatedAt.Plus(Duration.FromHours(1)), cancellationToken);
        await Allocate(seeded.FirstEmployeeId, cancellationToken, new AllocationChange(seeded.TeamAId, null, Feb1, null, 60));

        await using var context = _fixture.CreateContext();

        // Act
        await Handler(context).Handle(@event, cancellationToken);

        // Assert
        await using var assertContext = _fixture.CreateContext();
        (await assertContext.EmployeeTeamAllocations.SingleAsync(a => a.EmployeeId == seeded.FirstEmployeeId, cancellationToken)).End.Should().BeNull();
        (await ChangesRecorded(assertContext, seeded.FirstEmployeeId, cancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task TeamDeactivated_FromBeforeALaterReactivationAndDeactivation_DoesNotCutAtTheEarlierDate()
    {
        // Arrange — deactivated from the end of March, reactivated, allocated, then deactivated again from May
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        var first = await Deactivate(seeded.TeamAId, Mar31, DeactivatedAt, cancellationToken);
        await Activate(seeded.TeamAId, DeactivatedAt.Plus(Duration.FromHours(1)), cancellationToken);
        await Allocate(seeded.FirstEmployeeId, cancellationToken, new AllocationChange(seeded.TeamAId, null, Feb1, null, 60));
        var second = await Deactivate(seeded.TeamAId, May1, DeactivatedAt.Plus(Duration.FromHours(2)), cancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = Handler(context);

        // Act — the second deactivation is handled, then the first is redelivered late
        await handler.Handle(second, cancellationToken);
        await handler.Handle(first, cancellationToken);

        // Assert
        await using var assertContext = _fixture.CreateContext();
        (await assertContext.EmployeeTeamAllocations.SingleAsync(a => a.EmployeeId == seeded.FirstEmployeeId, cancellationToken)).End.Should().Be(May1);
    }

    [Fact]
    public async Task TeamDeactivated_ForATeamOfTeams_EndsAllocationsToIt()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        Guid teamOfTeamsId;
        await using (var seedContext = _fixture.CreateContext())
        {
            teamOfTeamsId = (await OrganizationSeeder.SeedTeamOfTeams(seedContext, "BANKING", "Banking", cancellationToken)).Id;
        }

        await Allocate(seeded.FirstEmployeeId, cancellationToken,
            new AllocationChange(teamOfTeamsId, null, Feb1, null, 40),
            new AllocationChange(seeded.TeamAId, null, Feb1, null, 60));
        var @event = await Deactivate(teamOfTeamsId, Mar31, DeactivatedAt, cancellationToken);

        await using var context = _fixture.CreateContext();

        // Act
        await Handler(context).Handle(@event, cancellationToken);

        // Assert
        await using var assertContext = _fixture.CreateContext();
        var allocations = await assertContext.EmployeeTeamAllocations.Where(a => a.EmployeeId == seeded.FirstEmployeeId).ToListAsync(cancellationToken);
        allocations.Select(Details).Should().BeEquivalentTo(
        [
            new EmployeeAllocationDetails(teamOfTeamsId, null, Feb1, Mar31, 40),
            new EmployeeAllocationDetails(seeded.TeamAId, null, Feb1, null, 60),
        ]);
    }

    [Fact]
    public async Task TeamDeactivated_WhenAnEmployeesAllocationsChangeMidSave_ReloadsThemAndEndsEveryones()
    {
        // Arrange — someone changes the first person's time on team B just as the handler saves them
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        await Allocate(seeded.FirstEmployeeId, cancellationToken,
            new AllocationChange(seeded.TeamAId, null, Feb1, null, 60),
            new AllocationChange(seeded.TeamBId, null, Feb1, null, 10));
        await Allocate(seeded.SecondEmployeeId, cancellationToken, new AllocationChange(seeded.TeamAId, null, Feb1, null, 100));
        var @event = await Deactivate(seeded.TeamAId, Mar31, DeactivatedAt, cancellationToken);

        var concurrentChange = new ConcurrentChange(_fixture, seeded.FirstEmployeeId, seeded.TeamBId, times: 1);
        await using var context = _fixture.CreateContext(concurrentChange);

        // Act
        await Handler(context).Handle(@event, cancellationToken);

        // Assert
        concurrentChange.Made.Should().Be(1);
        await using var assertContext = _fixture.CreateContext();
        var first = await assertContext.EmployeeTeamAllocations.Where(a => a.EmployeeId == seeded.FirstEmployeeId).ToListAsync(cancellationToken);
        first.Select(Details).Should().BeEquivalentTo(
        [
            new EmployeeAllocationDetails(seeded.TeamAId, null, Feb1, Mar31, 60),
            new EmployeeAllocationDetails(seeded.TeamBId, null, Feb1, null, 11),
        ]);
        (await assertContext.EmployeeTeamAllocations.SingleAsync(a => a.EmployeeId == seeded.SecondEmployeeId, cancellationToken)).End.Should().Be(Mar31);
    }

    [Fact]
    public async Task TeamDeactivated_WhenSomeoneIsAllocatedWhileItRuns_EndsTheirAllocationToo()
    {
        // Arrange — the second person's allocation is saved after the handler has read who is on the team
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        await Allocate(seeded.FirstEmployeeId, cancellationToken, new AllocationChange(seeded.TeamAId, null, Feb1, null, 60));
        var @event = await Deactivate(seeded.TeamAId, Mar31, DeactivatedAt, cancellationToken);

        var lateAllocation = new LateAllocation(this, seeded.SecondEmployeeId, new AllocationChange(seeded.TeamAId, null, Feb1, null, 100));
        await using var context = _fixture.CreateContext(lateAllocation);

        // Act
        await Handler(context).Handle(@event, cancellationToken);

        // Assert
        lateAllocation.Made.Should().BeTrue();
        await using var assertContext = _fixture.CreateContext();
        (await assertContext.EmployeeTeamAllocations.SingleAsync(a => a.EmployeeId == seeded.FirstEmployeeId, cancellationToken)).End.Should().Be(Mar31);
        (await assertContext.EmployeeTeamAllocations.SingleAsync(a => a.EmployeeId == seeded.SecondEmployeeId, cancellationToken)).End.Should().Be(Mar31);
    }

    [Fact]
    public async Task TeamDeactivated_WhenOneEmployeeKeepsConflicting_EndsTheRestAndFailsNamingThem()
    {
        // Arrange — the first person's allocations change on every attempt to save them
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        await Allocate(seeded.FirstEmployeeId, cancellationToken,
            new AllocationChange(seeded.TeamAId, null, Feb1, null, 60),
            new AllocationChange(seeded.TeamBId, null, Feb1, null, 10));
        await Allocate(seeded.SecondEmployeeId, cancellationToken, new AllocationChange(seeded.TeamAId, null, Feb1, null, 100));
        var @event = await Deactivate(seeded.TeamAId, Mar31, DeactivatedAt, cancellationToken);

        var concurrentChange = new ConcurrentChange(_fixture, seeded.FirstEmployeeId, seeded.TeamBId, times: int.MaxValue);
        await using var context = _fixture.CreateContext(concurrentChange);

        // Act
        var handle = () => Handler(context).Handle(@event, cancellationToken);

        // Assert
        (await handle.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain(seeded.FirstEmployeeId.ToString());
        concurrentChange.Made.Should().Be(TeamDeactivatedAllocationHandler.MaxAttemptsPerEmployee);

        await using (var assertContext = _fixture.CreateContext())
        {
            (await assertContext.EmployeeTeamAllocations.SingleAsync(a => a.EmployeeId == seeded.FirstEmployeeId && a.TeamId == seeded.TeamAId, cancellationToken)).End.Should().BeNull();
            (await assertContext.EmployeeTeamAllocations.SingleAsync(a => a.EmployeeId == seeded.SecondEmployeeId, cancellationToken)).End.Should().Be(Mar31);
        }

        // The redelivery the failure causes finishes the one that was left.
        await using (var retryContext = _fixture.CreateContext())
        {
            await Handler(retryContext).Handle(@event, cancellationToken);
        }

        await using var finalContext = _fixture.CreateContext();
        (await finalContext.EmployeeTeamAllocations.SingleAsync(a => a.EmployeeId == seeded.FirstEmployeeId && a.TeamId == seeded.TeamAId, cancellationToken)).End.Should().Be(Mar31);
        (await ChangesRecorded(finalContext, seeded.SecondEmployeeId, cancellationToken)).Should().Be(2);
    }

    private async Task<(Guid FirstEmployeeId, Guid SecondEmployeeId, Guid TeamAId, Guid TeamBId, Guid RoleId)> Seed(CancellationToken cancellationToken)
    {
        await _fixture.ResetOrganizationData(cancellationToken);

        await using var context = _fixture.CreateContext();
        var first = await OrganizationSeeder.SeedEmployee(context, "E-1001", "ada@acme.example", cancellationToken);
        var second = await OrganizationSeeder.SeedEmployee(context, "E-1002", "grace@acme.example", cancellationToken);
        var teamA = await OrganizationSeeder.SeedTeam(context, "CARDS", "Cards", cancellationToken);
        var teamB = await OrganizationSeeder.SeedTeam(context, "LOANS", "Loans", cancellationToken);
        var role = await OrganizationSeeder.SeedRole(context, "Engineer", cancellationToken);

        return (first.Id, second.Id, teamA.Id, teamB.Id, role.Id);
    }

    private async Task Allocate(Guid employeeId, CancellationToken cancellationToken, params AllocationChange[] changes)
    {
        await using var context = _fixture.CreateContext();
        var capacity = EmployeeCapacity.Create(employeeId);
        capacity.ChangeAllocations(changes, EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
        await context.EmployeeCapacities.AddAsync(capacity, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Deactivates the team as a command would, and returns the event that deactivation delivers.</summary>
    private async Task<TeamDeactivatedEvent> Deactivate(Guid teamId, LocalDate inactiveDate, Instant at, CancellationToken cancellationToken, EventActor? actor = null)
    {
        await using var context = _fixture.CreateContext();
        var team = await context.BaseTeams.SingleAsync(t => t.Id == teamId, cancellationToken);
        var args = TeamDeactivatableArgs.Create(inactiveDate, actor ?? EventActor.System, at);
        var result = team switch
        {
            TeamOfTeams teamOfTeams => teamOfTeams.Deactivate(args),
            Team plainTeam => plainTeam.Deactivate(args),
            _ => throw new InvalidOperationException($"Unexpected team type {team.GetType().Name}."),
        };
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

        // Taken before the save, which drains it.
        var raised = team.DomainEvents.OfType<TeamDeactivatedEvent>().Single();
        await context.SaveChangesAsync(cancellationToken);

        return raised;
    }

    private async Task Activate(Guid teamId, Instant at, CancellationToken cancellationToken)
    {
        await using var context = _fixture.CreateContext();
        var team = await context.Teams.SingleAsync(t => t.Id == teamId, cancellationToken);
        team.Activate(TeamActivatableArgs.Create(EventActor.System, at)).IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(cancellationToken);
    }

    private static Task<int> ChangesRecorded(WaydDbContext context, Guid employeeId, CancellationToken cancellationToken) =>
        context.ActivityLogs.CountAsync(a => a.AggregateId == employeeId && a.EventType == nameof(EmployeeAllocationsChangedEvent), cancellationToken);

    private static EmployeeAllocationDetails Details(EmployeeTeamAllocation allocation) =>
        new(allocation.TeamId, allocation.RoleId, allocation.Start, allocation.End, allocation.Percent);

    private static TeamDeactivatedAllocationHandler Handler(WaydDbContext context) =>
        new(context, context, NullLogger<TeamDeactivatedAllocationHandler>.Instance);

    /// <summary>
    /// Gives an employee their first allocation, from a second context, during the first save of the context
    /// under test: an allocation that was validated before the team was deactivated and saved after.
    /// </summary>
    private sealed class LateAllocation(TeamDeactivatedAllocationHandlerTests tests, Guid employeeId, AllocationChange change) : SaveChangesInterceptor
    {
        /// <summary>Whether the allocation was made.</summary>
        public bool Made { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Made)
            {
                Made = true;
                await tests.Allocate(employeeId, cancellationToken, change);
            }

            return result;
        }
    }

    /// <summary>
    /// Changes an employee's time on another team, from a second context, just before the context under test
    /// saves that employee: the change another user would make between the handler's load and its save.
    /// </summary>
    private sealed class ConcurrentChange(SqlServerDbContextFixture fixture, Guid employeeId, Guid teamId, int times) : SaveChangesInterceptor
    {
        /// <summary>How many times the employee's allocations were changed under the handler.</summary>
        public int Made { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            // A save writes twice, the second time for the activity log, by when the events have been taken
            // off the record. Only the first carries the employee's change.
            var savingTheEmployee = eventData.Context!.ChangeTracker.Entries<EmployeeCapacity>()
                .Any(e => e.Entity.Id == employeeId && e.Entity.DomainEvents.Count > 0);

            if (savingTheEmployee && Made < times)
            {
                Made++;

                await using var context = fixture.CreateContext();
                var capacity = await context.EmployeeCapacities.Include(c => c.Allocations).SingleAsync(c => c.Id == employeeId, cancellationToken);
                capacity.ChangeAllocations([new AllocationChange(teamId, null, Feb1, null, 10 + Made)], EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
                await context.SaveChangesAsync(cancellationToken);
            }

            return result;
        }
    }
}
