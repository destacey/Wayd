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
        var handler = ChangeHandler(context, Updater(seeded.EmployeeId), isMember: false);

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
        var handler = ChangeHandler(context, Updater(plannerId), isMember: true);

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
        var handler = new MoveEmployeeAllocationCommandHandler(context, Dispatcher(false), CurrentUser(), Administrator(), Clock(), NullLogger<MoveEmployeeAllocationCommandHandler>.Instance);

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
    public async Task GetEmployeeAllocations_ReturnsEachAllocationWithItsTeamAndRole()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        await Allocate(seeded.EmployeeId, cancellationToken,
            new AllocationChange(seeded.TeamAId, seeded.RoleId, Jan1, null, 70),
            new AllocationChange(seeded.TeamBId, null, Feb1, Feb7, 30));

        await using var context = _fixture.CreateContext();
        var handler = new GetEmployeeAllocationsQueryHandler(context, Dispatcher(false), Administrator(), Clock());

        // Act
        var allocations = await handler.Handle(new GetEmployeeAllocationsQuery(seeded.EmployeeId), cancellationToken);

        // Assert
        allocations.Should().HaveCount(2);
        var first = allocations[0];
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
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await Seed(cancellationToken);
        await Allocate(seeded.EmployeeId, cancellationToken, new AllocationChange(seeded.TeamAId, null, Jan1, null, 60));

        await using var context = _fixture.CreateContext();
        var handler = DeactivationHandler(context);

        // Act
        await handler.Handle(new EmployeeDeactivatedEvent(seeded.EmployeeId, EventActor.System, SqlServerDbContextFixture.FixedNow), cancellationToken);

        // Assert
        await using var assertContext = _fixture.CreateContext();
        (await assertContext.EmployeeTeamAllocations.SingleAsync(a => a.EmployeeId == seeded.EmployeeId, cancellationToken)).End.Should().BeNull();
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

    private async Task Allocate(Guid employeeId, CancellationToken cancellationToken, params AllocationChange[] changes)
    {
        await using var context = _fixture.CreateContext();
        var capacity = EmployeeCapacity.Create(employeeId);
        capacity.ChangeAllocations(changes, EventActor.System, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
        await context.EmployeeCapacities.AddAsync(capacity, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task SetActive(Guid employeeId, bool isActive, CancellationToken cancellationToken)
    {
        await using var context = _fixture.CreateContext();
        var employee = await context.Employees.SingleAsync(e => e.Id == employeeId, cancellationToken);
        var args = EmployeeActivatableArgs.Create(EventActor.System, SqlServerDbContextFixture.FixedNow);
        (isActive ? employee.Activate(args) : employee.Deactivate(args)).IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(cancellationToken);
    }

    private static ChangeEmployeeAllocationsCommandHandler ChangeHandler(WaydDbContext context, ICurrentPrincipal principal, bool isMember = false) =>
        new(context, Dispatcher(isMember), CurrentUser(), principal, Clock(), NullLogger<ChangeEmployeeAllocationsCommandHandler>.Instance);

    private static EmployeeDeactivatedAllocationHandler DeactivationHandler(WaydDbContext context)
    {
        var settings = new Mock<ISettings<SchedulingSettings>>();
        settings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(new SchedulingSettings());
        return new EmployeeDeactivatedAllocationHandler(context, settings.Object, NullLogger<EmployeeDeactivatedAllocationHandler>.Instance);
    }

    private static ICurrentPrincipal Administrator()
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return principal.Object;
    }

    /// <summary>A caller with the Update permission and no Administer, acting as the given employee.</summary>
    private static ICurrentPrincipal Updater(Guid employeeId)
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.HasPermission(TeamAllocationAuthorization.UpdatePermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        principal.Setup(p => p.HasPermission(TeamAllocationAuthorization.AdministratorPermission, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);
        return principal.Object;
    }

    private static IDispatcher Dispatcher(bool isMember)
    {
        var dispatcher = new Mock<IDispatcher>();
        dispatcher.Setup(d => d.Send(It.IsAny<IQuery<bool>>(), It.IsAny<CancellationToken>())).ReturnsAsync(isMember);
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
