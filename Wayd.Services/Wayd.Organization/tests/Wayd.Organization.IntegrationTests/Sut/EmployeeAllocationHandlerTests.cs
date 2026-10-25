using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Domain.Settings;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Organization.Application.EmployeeCapacities;
using Wayd.Organization.Application.EmployeeCapacities.Commands;
using Wayd.Organization.Application.EmployeeCapacities.EventHandlers;
using Wayd.Organization.Application.EmployeeCapacities.Queries;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// Employee allocations against a real SQL Server container.
/// </summary>
/// <remarks>
/// The allocations sit behind a backing field, a removed one must be deleted with no explicit remove, and two
/// changes worked out from the same allocations must not both save. Only a real context shows any of those.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class EmployeeAllocationHandlerTests(SqlServerDbContextFixture fixture)
{
    private static readonly LocalDate Jan1 = new(2027, 1, 1);
    private static readonly LocalDate Feb1 = new(2027, 2, 1);
    private static readonly LocalDate Feb7 = new(2027, 2, 7);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Change_PersistsTheAllocationAndRecordsItOnTheEmployee()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = ChangeHandler(context, Administrator());

        // Act
        var result = await handler.Handle(
            new ChangeEmployeeAllocationsCommand(seeded.EmployeeId, [new AllocationChange(seeded.TeamAId, seeded.RoleId, Jan1, null, 60)]),
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await using var assertContext = _fixture.CreateContext();
        var capacity = await assertContext.EmployeeCapacities.Include(c => c.Allocations).SingleAsync(c => c.Id == seeded.EmployeeId, cancellationToken);
        capacity.Revision.Should().Be(1);
        capacity.Allocations.Should().ContainSingle(a => a.TeamId == seeded.TeamAId && a.RoleId == seeded.RoleId && a.Start == Jan1 && a.End == null && a.Percent == 60);
        (await assertContext.ActivityLogs.AnyAsync(a => a.AggregateId == seeded.EmployeeId && a.EventType == nameof(EmployeeAllocationsChangedEvent), cancellationToken))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Change_ThatSplitsThenEndsAnAllocation_AddsResizesAndDeletesItsRows()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        await Allocate(seeded.EmployeeId, cancellationToken, new AllocationChange(seeded.TeamAId, null, Jan1, null, 50));

        // Act — a week at full time splits the allocation in three, then ending it all removes every row
        await using (var splitContext = _fixture.CreateContext())
        {
            var split = await ChangeHandler(splitContext, Administrator()).Handle(
                new ChangeEmployeeAllocationsCommand(seeded.EmployeeId, [new AllocationChange(seeded.TeamAId, null, Feb1, Feb7, 100)]),
                cancellationToken);
            split.IsSuccess.Should().BeTrue(split.IsFailure ? split.Error : null);
        }

        int rowsAfterSplit;
        await using (var midContext = _fixture.CreateContext())
        {
            rowsAfterSplit = await midContext.EmployeeTeamAllocations.CountAsync(a => a.EmployeeId == seeded.EmployeeId, cancellationToken);
        }

        await using (var endContext = _fixture.CreateContext())
        {
            var ended = await ChangeHandler(endContext, Administrator()).Handle(
                new ChangeEmployeeAllocationsCommand(seeded.EmployeeId, [new AllocationChange(seeded.TeamAId, null, Jan1, null, 0)]),
                cancellationToken);
            ended.IsSuccess.Should().BeTrue(ended.IsFailure ? ended.Error : null);
        }

        // Assert
        rowsAfterSplit.Should().Be(3);
        await using var assertContext = _fixture.CreateContext();
        (await assertContext.EmployeeTeamAllocations.AnyAsync(a => a.EmployeeId == seeded.EmployeeId, cancellationToken)).Should().BeFalse();
        (await assertContext.EmployeeCapacities.SingleAsync(c => c.Id == seeded.EmployeeId, cancellationToken)).Revision.Should().Be(3);
    }

    [Fact]
    public async Task Change_BeyondFullAllocation_FailsAndSavesNothing()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        await Allocate(seeded.EmployeeId, cancellationToken, new AllocationChange(seeded.TeamAId, null, Jan1, null, 60));

        await using var context = _fixture.CreateContext();
        var handler = ChangeHandler(context, Administrator());

        // Act
        var result = await handler.Handle(
            new ChangeEmployeeAllocationsCommand(seeded.EmployeeId, [new AllocationChange(seeded.TeamBId, null, Feb1, Feb7, 41)]),
            cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("101%");
        await using var assertContext = _fixture.CreateContext();
        (await assertContext.EmployeeTeamAllocations.CountAsync(a => a.EmployeeId == seeded.EmployeeId, cancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task Change_ByAUserWhoIsNotAMemberOfTheTeam_Fails()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = ChangeHandler(context, Updater(seeded.EmployeeId));

        // Act
        var result = await handler.Handle(
            new ChangeEmployeeAllocationsCommand(seeded.EmployeeId, [new AllocationChange(seeded.TeamAId, null, Jan1, null, 60)]),
            cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TeamAllocationAuthorization.NotAMemberError);
        await using var assertContext = _fixture.CreateContext();
        (await assertContext.EmployeeCapacities.AnyAsync(c => c.Id == seeded.EmployeeId, cancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task Change_ByAMemberWithTheUpdatePermission_Succeeds()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        Guid plannerId;
        await using (var seedContext = _fixture.CreateContext())
        {
            plannerId = (await OrganizationSeeder.SeedEmployee(seedContext, "E-1002", "grace@acme.example", cancellationToken)).Id;
        }

        await using var context = _fixture.CreateContext();
        var handler = ChangeHandler(context, Updater(plannerId), seeded.TeamAId);

        // Act
        var result = await handler.Handle(
            new ChangeEmployeeAllocationsCommand(seeded.EmployeeId, [new AllocationChange(seeded.TeamAId, null, Jan1, null, 60)]),
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
    }

    [Fact]
    public async Task Save_OfAChangeWorkedOutFromAllocationsAnotherChangeHasSinceReplaced_IsRejected()
    {
        // Arrange — each change fits within 100% alone, and together they would reach 120%
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        await Allocate(seeded.EmployeeId, cancellationToken, new AllocationChange(seeded.TeamAId, null, Jan1, null, 40));

        await using var firstContext = _fixture.CreateContext();
        await using var secondContext = _fixture.CreateContext();
        var first = await firstContext.EmployeeCapacities.Include(c => c.Allocations).SingleAsync(c => c.Id == seeded.EmployeeId, cancellationToken);
        var second = await secondContext.EmployeeCapacities.Include(c => c.Allocations).SingleAsync(c => c.Id == seeded.EmployeeId, cancellationToken);
        first.ChangeAllocations([new AllocationChange(seeded.TeamAId, null, Jan1, null, 80)], EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
        second.ChangeAllocations([new AllocationChange(seeded.TeamBId, null, Jan1, null, 40)], EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
        await firstContext.SaveChangesAsync(cancellationToken);

        // Act
        var save = () => secondContext.SaveChangesAsync(cancellationToken);

        // Assert
        await save.Should().ThrowAsync<DbUpdateConcurrencyException>();
        await using var assertContext = _fixture.CreateContext();
        (await assertContext.EmployeeTeamAllocations.Where(a => a.EmployeeId == seeded.EmployeeId).SumAsync(a => a.Percent, cancellationToken)).Should().Be(80);
    }

    [Fact]
    public async Task Move_PersistsBothSidesOfTheMove()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        await Allocate(seeded.EmployeeId, cancellationToken, new AllocationChange(seeded.TeamAId, null, Jan1, null, 100));

        await using var context = _fixture.CreateContext();
        var handler = MoveHandler(context, Administrator());

        // Act
        var result = await handler.Handle(
            new MoveEmployeeAllocationCommand(seeded.EmployeeId, seeded.TeamAId, null, seeded.TeamBId, seeded.RoleId, Feb1, Feb7, 50),
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await using var assertContext = _fixture.CreateContext();
        var allocations = await assertContext.EmployeeTeamAllocations.Where(a => a.EmployeeId == seeded.EmployeeId).ToListAsync(cancellationToken);
        allocations.Should().HaveCount(4);
        allocations.Should().ContainSingle(a => a.TeamId == seeded.TeamAId && a.Start == Feb1 && a.End == Feb7 && a.Percent == 50);
        allocations.Should().ContainSingle(a => a.TeamId == seeded.TeamBId && a.RoleId == seeded.RoleId && a.Start == Feb1 && a.End == Feb7 && a.Percent == 50);
    }

    [Fact]
    public async Task Change_ThatEndsForAnInactiveEmployee_PersistsTheAllocation()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        await SetActive(seeded.EmployeeId, false, cancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = ChangeHandler(context, Administrator());

        // Act
        var result = await handler.Handle(
            new ChangeEmployeeAllocationsCommand(seeded.EmployeeId, [new AllocationChange(seeded.TeamAId, seeded.RoleId, Feb1, Feb7, 60)]),
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await using var assertContext = _fixture.CreateContext();
        (await assertContext.EmployeeTeamAllocations.Where(a => a.EmployeeId == seeded.EmployeeId).ToListAsync(cancellationToken))
            .Should().ContainSingle(a => a.TeamId == seeded.TeamAId && a.Start == Feb1 && a.End == Feb7 && a.Percent == 60);
    }

    [Fact]
    public async Task Change_WithNoEndForAnInactiveEmployee_FailsAndSavesNothing()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        await SetActive(seeded.EmployeeId, false, cancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = ChangeHandler(context, Administrator());

        // Act
        var result = await handler.Handle(
            new ChangeEmployeeAllocationsCommand(seeded.EmployeeId, [new AllocationChange(seeded.TeamAId, null, Feb1, null, 60)]),
            cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AllocationTargets.InactiveEmployeeOpenEndedError);
        await using var assertContext = _fixture.CreateContext();
        (await assertContext.EmployeeTeamAllocations.AnyAsync(a => a.EmployeeId == seeded.EmployeeId, cancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task Change_ThatTakesTimeAwayWithNoEndForAnInactiveEmployee_Succeeds()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        await Allocate(seeded.EmployeeId, cancellationToken, new AllocationChange(seeded.TeamAId, null, Jan1, null, 60));
        await SetActive(seeded.EmployeeId, false, cancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = ChangeHandler(context, Administrator());

        // Act
        var result = await handler.Handle(
            new ChangeEmployeeAllocationsCommand(seeded.EmployeeId, [new AllocationChange(seeded.TeamAId, null, Feb1, null, 0)]),
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await using var assertContext = _fixture.CreateContext();
        (await assertContext.EmployeeTeamAllocations.Where(a => a.EmployeeId == seeded.EmployeeId).ToListAsync(cancellationToken))
            .Should().ContainSingle(a => a.Start == Jan1 && a.End == Feb1.PlusDays(-1));
    }

    [Fact]
    public async Task Change_ThatReplacesAnOpenAllocationWithOneThatEndsForAnInactiveEmployee_Succeeds()
    {
        // Arrange — the shape the change form sends: clear the original range, then set the new one
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        await Allocate(seeded.EmployeeId, cancellationToken, new AllocationChange(seeded.TeamAId, null, Jan1, null, 50));
        await SetActive(seeded.EmployeeId, false, cancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = ChangeHandler(context, Administrator());

        // Act
        var result = await handler.Handle(
            new ChangeEmployeeAllocationsCommand(seeded.EmployeeId,
            [
                new AllocationChange(seeded.TeamAId, null, Jan1, null, 0),
                new AllocationChange(seeded.TeamAId, null, Jan1, Feb7, 60),
            ]),
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await using var assertContext = _fixture.CreateContext();
        (await assertContext.EmployeeTeamAllocations.Where(a => a.EmployeeId == seeded.EmployeeId).ToListAsync(cancellationToken))
            .Should().ContainSingle(a => a.Start == Jan1 && a.End == Feb7 && a.Percent == 60);
    }

    [Fact]
    public async Task Move_WithNoEndForAnInactiveEmployee_FailsAndSavesNothing()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        await Allocate(seeded.EmployeeId, cancellationToken, new AllocationChange(seeded.TeamAId, null, Jan1, null, 100));
        await SetActive(seeded.EmployeeId, false, cancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = MoveHandler(context, Administrator());

        // Act
        var result = await handler.Handle(
            new MoveEmployeeAllocationCommand(seeded.EmployeeId, seeded.TeamAId, null, seeded.TeamBId, null, Feb1, null, 50),
            cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AllocationTargets.InactiveEmployeeOpenEndedError);
        await using var assertContext = _fixture.CreateContext();
        (await assertContext.EmployeeTeamAllocations.CountAsync(a => a.EmployeeId == seeded.EmployeeId, cancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task Move_ThatEndsForAnInactiveEmployee_PersistsBothSidesOfTheMove()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        await Allocate(seeded.EmployeeId, cancellationToken, new AllocationChange(seeded.TeamAId, null, Jan1, null, 100));
        await SetActive(seeded.EmployeeId, false, cancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = MoveHandler(context, Administrator());

        // Act
        var result = await handler.Handle(
            new MoveEmployeeAllocationCommand(seeded.EmployeeId, seeded.TeamAId, null, seeded.TeamBId, null, Feb1, Feb7, 50),
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await using var assertContext = _fixture.CreateContext();
        (await assertContext.EmployeeTeamAllocations.Where(a => a.EmployeeId == seeded.EmployeeId).ToListAsync(cancellationToken))
            .Should().ContainSingle(a => a.TeamId == seeded.TeamBId && a.Start == Feb1 && a.End == Feb7 && a.Percent == 50);
    }

    [Fact]
    public async Task GetEmployeeAllocations_ReturnsEachAllocationWithItsTeamAndRole()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        await Allocate(seeded.EmployeeId, cancellationToken,
            new AllocationChange(seeded.TeamAId, seeded.RoleId, Jan1, null, 70),
            new AllocationChange(seeded.TeamBId, null, Feb1, Feb7, 30));

        await using var context = _fixture.CreateContext();
        var handler = new GetEmployeeAllocationsQueryHandler(context, Dispatcher(), Administrator(), Clock());

        // Act
        var allocations = await handler.Handle(new GetEmployeeAllocationsQuery(seeded.EmployeeId), cancellationToken);

        // Assert
        allocations.Should().HaveCount(2);
        var first = allocations![0];
        first.Team.Id.Should().Be(seeded.TeamAId);
        first.Team.Name.Should().Be("Cards");
        first.Team.Code.Should().Be("CARDS");
        first.Role!.Name.Should().Be("Engineer");
        first.Start.Should().Be(Jan1);
        first.End.Should().BeNull();
        first.Percent.Should().Be(70);
        first.CanManage.Should().BeTrue();
        allocations[1].Team.Id.Should().Be(seeded.TeamBId);
        allocations[1].Role.Should().BeNull();
    }

    [Fact]
    public async Task EmployeeDeactivated_EndsOpenAllocationsThatDayAndRemovesLaterOnes()
    {
        // Arrange — FixedNow is 2026-01-15
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        var started = new LocalDate(2026, 1, 1);
        await Allocate(seeded.EmployeeId, cancellationToken,
            new AllocationChange(seeded.TeamAId, null, started, null, 60),
            new AllocationChange(seeded.TeamBId, null, Feb1, Feb7, 40));
        await SetActive(seeded.EmployeeId, false, cancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = DeactivationHandler(context);

        // Act — delivered twice, as a durable event may be
        var @event = new EmployeeDeactivatedEvent(seeded.EmployeeId, EventActor.System, SqlServerDbContextFixture.FixedNow);
        await handler.Handle(@event, cancellationToken);
        await handler.Handle(@event, cancellationToken);

        // Assert
        await using var assertContext = _fixture.CreateContext();
        var allocations = await assertContext.EmployeeTeamAllocations.Where(a => a.EmployeeId == seeded.EmployeeId).ToListAsync(cancellationToken);
        allocations.Should().ContainSingle()
            .Which.Should().Match<EmployeeTeamAllocation>(a => a.TeamId == seeded.TeamAId && a.Start == started && a.End == new LocalDate(2026, 1, 15) && a.Percent == 60);
    }

    [Fact]
    public async Task EmployeeDeactivated_DeliveredAfterTheEmployeeWasReactivated_ChangesNothing()
    {
        // Arrange — deactivated, reactivated an hour later, then allocated
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        var deactivatedAt = SqlServerDbContextFixture.FixedNow;
        await SetActive(seeded.EmployeeId, false, cancellationToken, deactivatedAt);
        await SetActive(seeded.EmployeeId, true, cancellationToken, deactivatedAt.Plus(Duration.FromHours(1)));
        await Allocate(seeded.EmployeeId, cancellationToken, new AllocationChange(seeded.TeamAId, null, Jan1, null, 60));

        await using var context = _fixture.CreateContext();
        var handler = DeactivationHandler(context);

        // Act
        await handler.Handle(new EmployeeDeactivatedEvent(seeded.EmployeeId, EventActor.System, deactivatedAt), cancellationToken);

        // Assert
        await using var assertContext = _fixture.CreateContext();
        (await assertContext.EmployeeTeamAllocations.SingleAsync(a => a.EmployeeId == seeded.EmployeeId, cancellationToken)).End.Should().BeNull();
    }

    [Fact]
    public async Task EmployeeDeactivated_FromBeforeALaterReactivationAndDeactivation_DoesNotCutWhatWasAllocatedInBetween()
    {
        // Arrange — deactivated, reactivated, allocated from the next week, then deactivated again a month on
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        var firstDeactivation = SqlServerDbContextFixture.FixedNow;
        var secondDeactivation = firstDeactivation.Plus(Duration.FromDays(30));
        var allocatedFrom = new LocalDate(2026, 1, 20);
        await SetActive(seeded.EmployeeId, false, cancellationToken, firstDeactivation);
        await SetActive(seeded.EmployeeId, true, cancellationToken, firstDeactivation.Plus(Duration.FromHours(1)));
        await Allocate(seeded.EmployeeId, cancellationToken, new AllocationChange(seeded.TeamAId, null, allocatedFrom, null, 60));
        await SetActive(seeded.EmployeeId, false, cancellationToken, secondDeactivation);

        await using var context = _fixture.CreateContext();
        var handler = DeactivationHandler(context);

        // Act — the second deactivation is handled, then the first is redelivered late
        await handler.Handle(new EmployeeDeactivatedEvent(seeded.EmployeeId, EventActor.System, secondDeactivation), cancellationToken);
        await handler.Handle(new EmployeeDeactivatedEvent(seeded.EmployeeId, EventActor.System, firstDeactivation), cancellationToken);

        // Assert
        await using var assertContext = _fixture.CreateContext();
        var allocation = await assertContext.EmployeeTeamAllocations.SingleAsync(a => a.EmployeeId == seeded.EmployeeId, cancellationToken);
        allocation.Start.Should().Be(allocatedFrom);
        allocation.End.Should().Be(secondDeactivation.InUtc().Date);
    }

    [Fact]
    public async Task EmployeeDeactivated_RedeliveredAfterTheAllocationsWereCorrected_KeepsTheCorrection()
    {
        // Arrange — deactivated a day before FixedNow; someone then extends the allocation to the real last day
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        var started = new LocalDate(2026, 1, 1);
        var realLastDay = new LocalDate(2026, 1, 31);
        var deactivatedAt = SqlServerDbContextFixture.FixedNow.Minus(Duration.FromDays(1));
        await AllocateAt(deactivatedAt.Minus(Duration.FromDays(7)), seeded.EmployeeId, cancellationToken,
            new AllocationChange(seeded.TeamAId, null, started, null, 60),
            new AllocationChange(seeded.TeamBId, null, started, null, 40));
        await SetActive(seeded.EmployeeId, false, cancellationToken, deactivatedAt);
        var @event = new EmployeeDeactivatedEvent(seeded.EmployeeId, EventActor.System, deactivatedAt);

        await using (var firstContext = _fixture.CreateContext())
        {
            await DeactivationHandler(firstContext).Handle(@event, cancellationToken);
        }

        await using (var changeContext = _fixture.CreateContext())
        {
            var corrected = await ChangeHandler(changeContext, Administrator()).Handle(
                new ChangeEmployeeAllocationsCommand(seeded.EmployeeId, [new AllocationChange(seeded.TeamAId, null, started, realLastDay, 60)]),
                cancellationToken);
            corrected.IsSuccess.Should().BeTrue(corrected.IsFailure ? corrected.Error : null);
        }

        await using var context = _fixture.CreateContext();

        // Act
        await DeactivationHandler(context).Handle(@event, cancellationToken);

        // Assert
        await using var assertContext = _fixture.CreateContext();
        var allocations = await assertContext.EmployeeTeamAllocations.Where(a => a.EmployeeId == seeded.EmployeeId).ToListAsync(cancellationToken);
        allocations.Select(a => (a.TeamId, a.Start, a.End, a.Percent)).Should().BeEquivalentTo(
        [
            (seeded.TeamAId, started, (LocalDate?)realLastDay, 60),
            (seeded.TeamBId, started, (LocalDate?)new LocalDate(2026, 1, 14), 40),
        ]);
    }

    [Fact]
    public async Task EmployeeDeactivated_DeliveredLateAfterTheAllocationsWereChanged_EndsOnlyOpenAllocations()
    {
        // Arrange — the change lands before the event is first handled, leaving team B open-ended
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        var started = new LocalDate(2026, 1, 1);
        var realLastDay = new LocalDate(2026, 1, 31);
        var deactivatedAt = SqlServerDbContextFixture.FixedNow.Minus(Duration.FromDays(1));
        await AllocateAt(deactivatedAt.Minus(Duration.FromDays(7)), seeded.EmployeeId, cancellationToken,
            new AllocationChange(seeded.TeamAId, null, started, null, 60),
            new AllocationChange(seeded.TeamBId, null, started, null, 40));
        await SetActive(seeded.EmployeeId, false, cancellationToken, deactivatedAt);

        await using (var changeContext = _fixture.CreateContext())
        {
            var corrected = await ChangeHandler(changeContext, Administrator()).Handle(
                new ChangeEmployeeAllocationsCommand(seeded.EmployeeId,
                [
                    new AllocationChange(seeded.TeamAId, null, started, null, 0),
                    new AllocationChange(seeded.TeamAId, null, started, realLastDay, 60),
                ]),
                cancellationToken);
            corrected.IsSuccess.Should().BeTrue(corrected.IsFailure ? corrected.Error : null);
        }

        await using var context = _fixture.CreateContext();

        // Act
        await DeactivationHandler(context).Handle(new EmployeeDeactivatedEvent(seeded.EmployeeId, EventActor.System, deactivatedAt), cancellationToken);

        // Assert
        await using var assertContext = _fixture.CreateContext();
        var allocations = await assertContext.EmployeeTeamAllocations.Where(a => a.EmployeeId == seeded.EmployeeId).ToListAsync(cancellationToken);
        allocations.Select(a => (a.TeamId, a.Start, a.End, a.Percent)).Should().BeEquivalentTo(
        [
            (seeded.TeamAId, started, (LocalDate?)realLastDay, 60),
            (seeded.TeamBId, started, (LocalDate?)new LocalDate(2026, 1, 14), 40),
        ]);
    }

    [Fact]
    public async Task Change_ByAnAdministratorWhoIsNotAMember_Succeeds()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = ChangeHandler(context, Administrator());

        // Act
        var result = await handler.Handle(
            new ChangeEmployeeAllocationsCommand(seeded.EmployeeId, [new AllocationChange(seeded.TeamAId, null, Jan1, null, 60)]),
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
    }

    [Fact]
    public async Task Change_ByAnUpdaterLinkedToNoEmployee_Fails()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = ChangeHandler(context, Updater(null), seeded.TeamAId);

        // Act
        var result = await handler.Handle(
            new ChangeEmployeeAllocationsCommand(seeded.EmployeeId, [new AllocationChange(seeded.TeamAId, null, Jan1, null, 60)]),
            cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TeamAllocationAuthorization.NotAMemberError);
    }

    [Fact]
    public async Task Change_ThatChangesNothingForAnEmployeeWithNoAllocations_LeavesNoRecord()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = ChangeHandler(context, Administrator());

        // Act
        var result = await handler.Handle(
            new ChangeEmployeeAllocationsCommand(seeded.EmployeeId, [new AllocationChange(seeded.TeamAId, null, Jan1, null, 0)]),
            cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await using var assertContext = _fixture.CreateContext();
        (await assertContext.EmployeeCapacities.AnyAsync(c => c.Id == seeded.EmployeeId, cancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task Save_OfAFirstAllocationAnotherWriterMadeFirst_FailsAsChangedElsewhere()
    {
        // Arrange — both writers found no record and each built a new one
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);

        await using var firstContext = _fixture.CreateContext();
        await using var secondContext = _fixture.CreateContext();
        var (first, firstIsNew) = await AllocationTargets.LoadOrCreateCapacity(firstContext, seeded.EmployeeId, cancellationToken);
        var (second, secondIsNew) = await AllocationTargets.LoadOrCreateCapacity(secondContext, seeded.EmployeeId, cancellationToken);
        first.ChangeAllocations([new AllocationChange(seeded.TeamAId, null, Jan1, null, 80)], EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
        second.ChangeAllocations([new AllocationChange(seeded.TeamBId, null, Jan1, null, 40)], EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
        (await AllocationTargets.Save(firstContext, first, firstIsNew, cancellationToken)).IsSuccess.Should().BeTrue();

        // Act
        var result = await AllocationTargets.Save(secondContext, second, secondIsNew, cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AllocationTargets.ChangedElsewhereError);
        await using var assertContext = _fixture.CreateContext();
        (await assertContext.EmployeeTeamAllocations.Where(a => a.EmployeeId == seeded.EmployeeId).SumAsync(a => a.Percent, cancellationToken)).Should().Be(80);
    }

    [Fact]
    public async Task Move_ByAMemberOfOnlyOneOfTheTwoTeams_Fails()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        await Allocate(seeded.EmployeeId, cancellationToken, new AllocationChange(seeded.TeamAId, null, Jan1, null, 100));

        await using var context = _fixture.CreateContext();
        var handler = MoveHandler(context, Updater(seeded.EmployeeId), seeded.TeamAId);

        // Act
        var result = await handler.Handle(
            new MoveEmployeeAllocationCommand(seeded.EmployeeId, seeded.TeamAId, null, seeded.TeamBId, null, Feb1, Feb7, 50),
            cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TeamAllocationAuthorization.NotAMemberError);
        await using var assertContext = _fixture.CreateContext();
        (await assertContext.EmployeeTeamAllocations.CountAsync(a => a.EmployeeId == seeded.EmployeeId, cancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task GetEmployeeAllocations_ForAnEmployeeThatDoesNotExist_ReturnsNull()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var handler = new GetEmployeeAllocationsQueryHandler(context, Dispatcher(), Administrator(), Clock());

        // Act
        var allocations = await handler.Handle(new GetEmployeeAllocationsQuery(Guid.NewGuid()), cancellationToken);

        // Assert
        allocations.Should().BeNull();
    }

    private async Task<(Guid EmployeeId, Guid TeamAId, Guid TeamBId, Guid RoleId)> Seed(CancellationToken cancellationToken)
    {
        await _fixture.ResetOrganizationData(cancellationToken);

        await using var context = _fixture.CreateContext();
        var employee = await OrganizationSeeder.SeedEmployee(context, "E-1001", "ada@acme.example", cancellationToken);
        var teamA = await OrganizationSeeder.SeedTeam(context, "CARDS", "Cards", cancellationToken);
        var teamB = await OrganizationSeeder.SeedTeam(context, "LOANS", "Loans", cancellationToken);
        var role = await OrganizationSeeder.SeedRole(context, "Engineer", cancellationToken);

        return (employee.Id, teamA.Id, teamB.Id, role.Id);
    }

    private Task Allocate(Guid employeeId, CancellationToken cancellationToken, params AllocationChange[] changes) =>
        AllocateAt(SqlServerDbContextFixture.FixedNow, employeeId, cancellationToken, changes);

    private async Task AllocateAt(Instant at, Guid employeeId, CancellationToken cancellationToken, params AllocationChange[] changes)
    {
        await using var context = _fixture.CreateContext();
        var capacity = EmployeeCapacity.Create(employeeId);
        capacity.ChangeAllocations(changes, EventActor.System, at).IsSuccess.Should().BeTrue();
        await context.EmployeeCapacities.AddAsync(capacity, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task SetActive(Guid employeeId, bool isActive, CancellationToken cancellationToken, Instant? at = null)
    {
        await using var context = _fixture.CreateContext();
        var employee = await context.Employees.SingleAsync(e => e.Id == employeeId, cancellationToken);
        var args = EmployeeActivatableArgs.Create(EventActor.System, at ?? SqlServerDbContextFixture.FixedNow);
        (isActive ? employee.Activate(args) : employee.Deactivate(args)).IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(cancellationToken);
    }

    private static ChangeEmployeeAllocationsCommandHandler ChangeHandler(WaydDbContext context, ICurrentPrincipal principal, params Guid[] memberOf) =>
        new(context, Dispatcher(memberOf), CurrentUser(), principal, Clock(), NullLogger<ChangeEmployeeAllocationsCommandHandler>.Instance);

    private static MoveEmployeeAllocationCommandHandler MoveHandler(WaydDbContext context, ICurrentPrincipal principal, params Guid[] memberOf) =>
        new(context, Dispatcher(memberOf), CurrentUser(), principal, Clock(), NullLogger<MoveEmployeeAllocationCommandHandler>.Instance);

    private static EmployeeDeactivatedAllocationHandler DeactivationHandler(WaydDbContext context)
    {
        var settings = new Mock<ISettings<SchedulingSettings>>();
        settings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(new SchedulingSettings());
        return new EmployeeDeactivatedAllocationHandler(context, context, settings.Object, NullLogger<EmployeeDeactivatedAllocationHandler>.Instance);
    }

    /// <summary>A caller with Update and Administer, linked to no employee and a member of no team.</summary>
    private static ICurrentPrincipal Administrator()
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        principal.Setup(p => p.HoldsPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);
        return principal.Object;
    }

    /// <summary>A caller with the Update permission and no Administer, acting as the given employee.</summary>
    private static ICurrentPrincipal Updater(Guid? employeeId)
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.HasPermission(TeamAllocationAuthorization.UpdatePermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        principal.Setup(p => p.HoldsPermission(TeamAllocationAuthorization.UpdatePermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        principal.Setup(p => p.HasPermission(TeamAllocationAuthorization.AdministratorPermission, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        principal.Setup(p => p.HoldsPermission(TeamAllocationAuthorization.AdministratorPermission, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);
        return principal.Object;
    }

    /// <summary>A dispatcher whose membership read answers with <paramref name="memberOf"/>.</summary>
    private static IDispatcher Dispatcher(params Guid[] memberOf)
    {
        var dispatcher = new Mock<IDispatcher>();
        dispatcher.Setup(d => d.Send(It.IsAny<IQuery<IReadOnlySet<Guid>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(memberOf.ToHashSet());
        return dispatcher.Object;
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
