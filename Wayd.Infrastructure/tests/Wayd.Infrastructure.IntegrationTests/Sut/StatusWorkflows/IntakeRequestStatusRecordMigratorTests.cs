using Microsoft.EntityFrameworkCore;
using NodaTime;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Models;
using Wayd.DemandManagement.Domain;
using Wayd.DemandManagement.Domain.Models;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.StatusWorkflows;

namespace Wayd.Infrastructure.IntegrationTests.Sut.StatusWorkflows;

/// <summary>
/// The intake request migrator against SQL Server: a desk's scope moves that desk's requests, and the
/// organization's moves only those on desks without an assignment of their own.
/// </summary>
/// <remarks>
/// Each test moves requests off a workflow it creates, so requests other tests in the collection leave on the
/// shared database are never selected.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class IntakeRequestStatusRecordMigratorTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 1, 8, 0, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    private sealed record Scenario(Guid FromId, Guid ToId, Guid FollowingDeskId, Guid OwnDeskId);

    /// <summary>
    /// Two published copies of the seeded intake workflow, a desk following the default and a desk with an
    /// assignment of its own, and Submitted requests on the first copy: <paramref name="onFollowing"/> on the
    /// desk following the default and one on the other.
    /// </summary>
    private async Task<Scenario> Seed(CancellationToken ct, int onFollowing = 1)
    {
        await using var seeding = _fixture.CreateContext();
        await IntakeStatuses.SeededResolver(seeding, ct);

        var seeded = await seeding.StatusWorkflows.Include(w => w.Statuses).Include(w => w.Transitions)
            .SingleAsync(w => w.OwnerType == DemandManagementWorkflowOwners.IntakeRequest.Key && w.IsSystem, ct);
        StatusWorkflow Copy(string name)
        {
            var copy = seeded.Clone($"{name} {Guid.NewGuid():N}"[..30], null, EventActor.System, Now);
            copy.Publish(EventActor.System, Now);
            seeding.StatusWorkflows.Add(copy);
            return copy;
        }
        var from = Copy("From");
        var to = Copy("To");

        var requester = Employee.Create(new PersonName("Intake", null, "Probe"), $"{Guid.NewGuid():N}"[..12], Now,
            new EmailAddress($"{Guid.NewGuid():N}"[..12] + "@acme.example"), null, null, null, null, true, null, EventActor.System, Now);
        IntakeDesk Desk() => IntakeDesk.Create($"Desk-{Guid.NewGuid():N}"[..30], "Anything", IntakeDeskVisibility.Open, true, true, true, [],
            EventActor.System, Now);
        var following = Desk();
        var own = Desk();
        var channel = IntakeChannel.Create($"Channel-{Guid.NewGuid():N}"[..30], null, 1, EventActor.System, Now);
        seeding.Employees.Add(requester);
        seeding.IntakeDesks.AddRange(following, own);
        seeding.IntakeChannels.Add(channel);
        await seeding.SaveChangesAsync(ct);

        // Pointed at the target with no migrator, so only the assignment changes.
        (await new ScopedWorkflowAssignments(seeding, [], [])
            .Change(DemandManagementWorkflowOwners.IntakeRequest.Key, own.Id, to.Id, [], EventActor.System, Now, ct))
            .IsSuccess.Should().BeTrue();

        var submitted = from.TargetRef(from.Statuses.Single(s => s.Alias == (int)IntakeRequestStatusAlias.Submitted).Id)!;
        foreach (var desk in Enumerable.Repeat(following, onFollowing).Append(own))
        {
            seeding.IntakeRequests.Add(IntakeRequest.Create("Title", "Description", null, requester.Id, requester.Id, desk, channel,
                null, null, submitted, EventActor.System, Now).Value);
        }
        await seeding.SaveChangesAsync(ct);

        return new Scenario(from.Id, to.Id, following.Id, own.Id);
    }

    private static async Task<StatusRemap> Remap(Wayd.Infrastructure.Persistence.Context.WaydDbContext context, Scenario scenario, CancellationToken ct)
    {
        var from = await context.StatusWorkflows.Include(w => w.Statuses).SingleAsync(w => w.Id == scenario.FromId, ct);
        var to = await context.StatusWorkflows.Include(w => w.Statuses).SingleAsync(w => w.Id == scenario.ToId, ct);
        return StatusRemap.AutoMap(from, to).Value;
    }

    [Fact]
    public async Task Migrate_ForADesk_MovesOnlyThatDesksRequests_AndSavesThem()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = await Seed(ct);
        await using var context = _fixture.CreateContext();
        var sut = new IntakeRequestStatusRecordMigrator(context, context);

        // Act
        var migrated = await sut.Migrate(await Remap(context, scenario, ct), scenario.FollowingDeskId, EventActor.System, Now, ct);
        await context.SaveChangesAsync(ct);

        // Assert
        migrated.Value.Should().Be(1);
        await using var reader = _fixture.CreateContext();
        var requests = await reader.IntakeRequests.AsNoTracking()
            .Where(r => r.DeskId == scenario.FollowingDeskId || r.DeskId == scenario.OwnDeskId)
            .ToListAsync(ct);
        requests.Single(r => r.DeskId == scenario.FollowingDeskId).StatusWorkflowId.Should().Be(scenario.ToId);
        requests.Single(r => r.DeskId == scenario.OwnDeskId).StatusWorkflowId.Should().Be(scenario.FromId);
        (await reader.StatusTransitions.CountAsync(t => t.WorkflowId == scenario.ToId, ct)).Should().Be(1);
    }

    [Fact]
    public async Task Migrate_ForTheOrganization_SkipsADeskWithItsOwnAssignment()
    {
        // Arrange — even when that desk's requests are on the workflow being left
        var ct = TestContext.Current.CancellationToken;
        var scenario = await Seed(ct);
        await using var context = _fixture.CreateContext();
        var sut = new IntakeRequestStatusRecordMigrator(context, context);

        // Act
        var counted = await sut.CountByStatus(scenario.FromId, null, ct);
        var migrated = await sut.Migrate(await Remap(context, scenario, ct), null, EventActor.System, Now, ct);

        // Assert
        counted.Value.Values.Sum().Should().Be(1);
        migrated.Value.Should().Be(1);
        context.ChangeTracker.Entries<IntakeRequest>()
            .Where(e => e.Entity.StatusWorkflowId == scenario.ToId)
            .Should().ContainSingle().Which.Entity.DeskId.Should().Be(scenario.FollowingDeskId);
    }

    [Fact]
    public async Task CountByStatus_ForADesk_CountsOnlyThatDesksRequests()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = await Seed(ct);
        await using var context = _fixture.CreateContext();

        // Act
        var counted = await new IntakeRequestStatusRecordMigrator(context, context).CountByStatus(scenario.FromId, scenario.OwnDeskId, ct);

        // Assert
        counted.Value.Should().ContainSingle().Which.Value.Should().Be(1);
    }

    [Fact]
    public async Task Migrate_ForADeskHoldingMoreThanOneBatch_MovesEveryRequest()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = await Seed(ct, onFollowing: 501);
        await using var context = _fixture.CreateContext();
        var sut = new IntakeRequestStatusRecordMigrator(context, context);

        // Act
        var migrated = await sut.Migrate(await Remap(context, scenario, ct), scenario.FollowingDeskId, EventActor.System, Now, ct);
        await context.SaveChangesAsync(ct);

        // Assert
        migrated.Value.Should().Be(501);
        await using var reader = _fixture.CreateContext();
        (await reader.IntakeRequests.CountAsync(r => r.DeskId == scenario.FollowingDeskId && r.StatusWorkflowId == scenario.ToId, ct))
            .Should().Be(501);
    }

    [Fact]
    public async Task Migrate_WhenRequestsCannotLandWhereTheyAreMapped_NamesEveryOne_AndMovesNone()
    {
        // Arrange — Submitted sent to Duplicate, which none of them names a surviving request for
        var ct = TestContext.Current.CancellationToken;
        var scenario = await Seed(ct, onFollowing: 2);
        await using var context = _fixture.CreateContext();
        var remap = await Remap(context, scenario, ct);
        var from = await context.StatusWorkflows.Include(w => w.Statuses).SingleAsync(w => w.Id == scenario.FromId, ct);
        var to = await context.StatusWorkflows.Include(w => w.Statuses).SingleAsync(w => w.Id == scenario.ToId, ct);
        remap.Resolve(from.Statuses.Single(s => s.Alias == (int)IntakeRequestStatusAlias.Submitted).Id,
            to.Statuses.Single(s => s.Alias == (int)IntakeRequestStatusAlias.Duplicate)).IsSuccess.Should().BeTrue();
        var keys = await context.IntakeRequests.Where(r => r.DeskId == scenario.FollowingDeskId).Select(r => r.Key).ToListAsync(ct);

        // Act
        var migrated = await new IntakeRequestStatusRecordMigrator(context, context)
            .Migrate(remap, scenario.FollowingDeskId, EventActor.System, Now, ct);

        // Assert
        migrated.IsFailure.Should().BeTrue();
        migrated.Error.Should().StartWith("2 requests cannot move");
        foreach (var key in keys)
            migrated.Error.Should().Contain($"Request {key} ");
        context.ChangeTracker.Entries<IntakeRequest>().Should().NotContain(e => e.Entity.StatusWorkflowId == scenario.ToId);
    }

    [Fact]
    public async Task WorkflowsBesides_ForADesk_FindsTheWorkflowsItsRequestsAreLeftOn()
    {
        // Arrange — the desk with its own assignment follows the second copy, its request is still on the first
        var ct = TestContext.Current.CancellationToken;
        var scenario = await Seed(ct);
        await using var context = _fixture.CreateContext();

        // Act
        var others = await new IntakeRequestStatusRecordMigrator(context, context).WorkflowsBesides(scenario.ToId, scenario.OwnDeskId, ct);

        // Assert
        others.Value.Should().Equal(scenario.FromId);
    }
}
