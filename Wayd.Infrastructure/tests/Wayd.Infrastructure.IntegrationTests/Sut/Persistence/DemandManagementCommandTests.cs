using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.Events.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Common.Models;
using Wayd.DemandManagement.Application.IntakeDesks;
using Wayd.DemandManagement.Application.IntakeDesks.Commands;
using Wayd.DemandManagement.Application.IntakeDesks.Queries;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Queries;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Domain;
using Wayd.DemandManagement.Domain.Models;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Runs the intake commands against SQL Server: their visibility checks and lookups translate, and what they
/// change is saved and recorded.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class DemandManagementCommandTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 1, 8, 0, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;
    private readonly IDateTimeProvider _clock = Clock();

    [Fact]
    public async Task ADeskAdministratorAndARequester_CreateADeskThenSubmitEditAndShareARequest()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        Employee requester, agent, colleague;
        IntakeChannel channel;
        await using (var seeding = _fixture.CreateContext())
        {
            requester = SeedEmployee(seeding);
            agent = SeedEmployee(seeding);
            colleague = SeedEmployee(seeding);
            channel = IntakeChannel.Create($"Channel-{Guid.NewGuid():N}"[..30], null, 1);
            seeding.IntakeChannels.Add(channel);
            await seeding.SaveChangesAsync(ct);
        }

        // Act
        await using var context = _fixture.CreateContext();
        var desk = await new CreateIntakeDeskCommandHandler(context, Principal(agent.Id), User(), _clock).Handle(
            new CreateIntakeDeskCommand($"Desk-{Guid.NewGuid():N}"[..30], "Internal tools", IntakeDeskVisibility.SummaryOnly,
                true, true, true, [new IntakeDeskMemberDetails(agent.Id, IntakeDeskRole.Agent)]), ct);
        var created = await new CreateIntakeRequestCommandHandler(context, Principal(requester.Id), User(), _clock,
            await IntakeStatuses.SeededResolver(context, ct)).Handle(
            new CreateIntakeRequestCommand("Export invoices", "Finance needs a CSV export", null, desk.Value.Id, channel.Id,
                null, null), ct);
        var updated = await new UpdateIntakeRequestCommandHandler(context, Principal(requester.Id), User(), _clock).Handle(
            new UpdateIntakeRequestCommand(created.Value.Id, "Export invoices to CSV", "Finance needs a CSV export", null,
                channel.Id, new IntakeRequestNeedByInput(new LocalDate(2026, 12, 31), NeedByBasis.RequesterPreference, "Quarter end", null, null)), ct);
        var shared = await new AddIntakeRequestParticipantCommandHandler(context, Principal(requester.Id), User(), _clock).Handle(
            new AddIntakeRequestParticipantCommand(created.Value.Id, colleague.Id), ct);
        // The agent holds no claim: their membership, read from the database, is the grant.
        var assigned = await new ChangeIntakeRequestAssigneeCommandHandler(context, Principal(agent.Id), User(), _clock).Handle(
            new ChangeIntakeRequestAssigneeCommand(created.Value.Id, agent.Id), ct);

        // Assert
        new[] { desk.IsSuccess, created.IsSuccess, updated.IsSuccess, shared.IsSuccess, assigned.IsSuccess }.Should().AllBeEquivalentTo(true);

        await using var reader = _fixture.CreateContext();
        var saved = await reader.IntakeRequests.AsNoTracking().Include(r => r.Participants).SingleAsync(r => r.Id == created.Value.Id, ct);
        saved.Title.Should().Be("Export invoices to CSV");
        saved.NeedBy!.Basis.Should().Be(NeedByBasis.RequesterPreference);
        saved.Participants.Select(p => p.EmployeeId).Should().Equal(colleague.Id);
        saved.AssigneeId.Should().Be(agent.Id);
        // Started in the default workflow's Submitted, with that first move in the shared history table.
        saved.StatusAlias.Should().Be(IntakeRequestStatusAlias.Submitted);
        saved.StatusTransitionCount.Should().Be(1);
        var transition = await reader.StatusTransitions.AsNoTracking()
            .SingleAsync(t => t.OwnerType == DemandManagementWorkflowOwners.IntakeRequest.Key && t.RecordId == saved.Id, ct);
        transition.FromStatusId.Should().BeNull();
        transition.ToStatusId.Should().Be(saved.StatusId);
        transition.WorkflowId.Should().Be(saved.StatusWorkflowId);

        var events = await reader.ActivityLogs.AsNoTracking()
            .Where(a => a.AggregateId == created.Value.Id)
            .OrderBy(a => a.Timestamp).ThenBy(a => a.Ordinal)
            .Select(a => a.EventType)
            .ToListAsync(ct);
        events.Should().Equal(
            nameof(IntakeRequestCreatedEvent),
            nameof(IntakeRequestRetitledEvent),
            nameof(IntakeRequestNeedByChangedEvent),
            nameof(IntakeRequestParticipantAddedEvent),
            nameof(IntakeRequestAssigneeChangedEvent));
    }

    [Fact]
    public async Task DeleteIntakeRequest_RemovesTheRequestAndItsParticipants()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        Guid requestId;
        Employee requester, deskAdmin;
        await using (var seeding = _fixture.CreateContext())
        {
            requester = SeedEmployee(seeding);
            deskAdmin = SeedEmployee(seeding);
            var participant = SeedEmployee(seeding);
            var desk = IntakeDesk.Create($"Desk-{Guid.NewGuid():N}"[..30], "Anything", IntakeDeskVisibility.Open, true, true, true,
                [new IntakeDeskMemberDetails(deskAdmin.Id, IntakeDeskRole.Admin)], EventActor.System, Now);
            var channel = IntakeChannel.Create($"Channel-{Guid.NewGuid():N}"[..30], null, 1);
            seeding.IntakeDesks.Add(desk);
            seeding.IntakeChannels.Add(channel);
            await seeding.SaveChangesAsync(ct);
            var request = IntakeRequest.Create("Title", "Description", null, requester.Id, requester.Id, desk, channel, null, null, IntakeStatuses.Submitted(), EventActor.System, Now).Value;
            request.AddParticipant(participant.Id, null, EventActor.System, Now);
            seeding.IntakeRequests.Add(request);
            await seeding.SaveChangesAsync(ct);
            requestId = request.Id;
        }

        // Act
        await using var context = _fixture.CreateContext();
        var result = await new DeleteIntakeRequestCommandHandler(context, Principal(deskAdmin.Id), User(), _clock)
            .Handle(new DeleteIntakeRequestCommand(requestId), ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await using var reader = _fixture.CreateContext();
        (await reader.IntakeRequests.AnyAsync(r => r.Id == requestId, ct)).Should().BeFalse();
        (await reader.ActivityLogs.AnyAsync(a => a.AggregateId == requestId && a.EventType == nameof(IntakeRequestDeletedEvent), ct)).Should().BeTrue();
    }

    [Fact]
    public async Task UpdateIntakeDesk_ByADeskAdmin_ChangesItsAgentsAndUnassignsTheOneRemoved()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        Employee deskAdmin, leaving, joining;
        IntakeDesk desk;
        Guid requestId;
        await using (var seeding = _fixture.CreateContext())
        {
            deskAdmin = SeedEmployee(seeding);
            leaving = SeedEmployee(seeding);
            joining = SeedEmployee(seeding);
            desk = IntakeDesk.Create($"Desk-{Guid.NewGuid():N}"[..30], "Anything", IntakeDeskVisibility.Open, true, true, true,
                [new IntakeDeskMemberDetails(deskAdmin.Id, IntakeDeskRole.Admin), new IntakeDeskMemberDetails(leaving.Id, IntakeDeskRole.Agent)],
                EventActor.System, Now);
            var channel = IntakeChannel.Create($"Channel-{Guid.NewGuid():N}"[..30], null, 1);
            seeding.IntakeDesks.Add(desk);
            seeding.IntakeChannels.Add(channel);
            await seeding.SaveChangesAsync(ct);
            var request = IntakeRequest.Create("Title", "Description", null, deskAdmin.Id, deskAdmin.Id, desk, channel, null, null, IntakeStatuses.Submitted(), EventActor.System, Now).Value;
            seeding.IntakeRequests.Add(request);
            await seeding.SaveChangesAsync(ct);
            request.ChangeAssignee(leaving.Id, EventActor.System, Now);
            await seeding.SaveChangesAsync(ct);
            requestId = request.Id;
        }

        // Act — the Desk Admin holds no claim
        await using var context = _fixture.CreateContext();
        var result = await new UpdateIntakeDeskCommandHandler(context, Principal(deskAdmin.Id), User(), _clock).Handle(
            new UpdateIntakeDeskCommand(desk.Id, "Renamed desk", desk.Description, desk.Visibility, false, false, true,
                [new IntakeDeskMemberDetails(deskAdmin.Id, IntakeDeskRole.Admin), new IntakeDeskMemberDetails(joining.Id, IntakeDeskRole.Agent)],
                desk.Revision),
            ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await using var reader = _fixture.CreateContext();
        var saved = await reader.IntakeDesks.AsNoTracking().Include(d => d.Members).SingleAsync(d => d.Id == desk.Id, ct);
        saved.Name.Should().Be("Renamed desk");
        saved.IsOpen.Should().BeFalse();
        saved.Members.Select(m => (m.EmployeeId, m.Role)).Should().BeEquivalentTo(
            [(deskAdmin.Id, IntakeDeskRole.Admin), (joining.Id, IntakeDeskRole.Agent)]);
        (await reader.IntakeRequests.AsNoTracking().SingleAsync(r => r.Id == requestId, ct)).AssigneeId.Should().BeNull();
        (await reader.ActivityLogs.AsNoTracking().Where(a => a.AggregateId == desk.Id).Select(a => a.EventType).ToListAsync(ct))
            .Should().Contain([nameof(IntakeDeskDetailsUpdatedEvent), nameof(IntakeDeskClosedEvent), nameof(IntakeDeskMembersChangedEvent)]);
    }

    [Fact]
    public async Task UpdateIntakeDesk_TwoSavesReadAtTheSameRevision_TheSecondIsRefused()
    {
        // Arrange — both pass the handler's revision check; the database's concurrency check catches the second
        var ct = TestContext.Current.CancellationToken;
        Employee deskAdmin;
        IntakeDesk desk;
        await using (var seeding = _fixture.CreateContext())
        {
            deskAdmin = SeedEmployee(seeding);
            desk = IntakeDesk.Create($"Desk-{Guid.NewGuid():N}"[..30], "Anything", IntakeDeskVisibility.Open, true, true, true,
                [new IntakeDeskMemberDetails(deskAdmin.Id, IntakeDeskRole.Admin)], EventActor.System, Now);
            seeding.IntakeDesks.Add(desk);
            await seeding.SaveChangesAsync(ct);
        }

        UpdateIntakeDeskCommand Renamed(string name) => new(desk.Id, name, desk.Description, desk.Visibility, true, true, true,
            [new IntakeDeskMemberDetails(deskAdmin.Id, IntakeDeskRole.Admin)], desk.Revision);

        await using var first = _fixture.CreateContext();
        await using var second = _fixture.CreateContext();
        var firstDesk = await first.IntakeDesks.Include(d => d.Members).SingleAsync(d => d.Id == desk.Id, ct);
        var secondDesk = await second.IntakeDesks.Include(d => d.Members).SingleAsync(d => d.Id == desk.Id, ct);
        firstDesk.UpdateDetails("First rename", desk.Description, EventActor.System, Now);
        await first.SaveChangesAsync(ct);

        // Act — the second context still holds the desk as it was read, at the old revision
        var result = await new UpdateIntakeDeskCommandHandler(second, Principal(deskAdmin.Id), User(), _clock)
            .Handle(Renamed("Second rename"), ct);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskAccess.StaleError);
        await using var reader = _fixture.CreateContext();
        (await reader.IntakeDesks.AsNoTracking().SingleAsync(d => d.Id == desk.Id, ct)).Name.Should().Be("First rename");
    }

    [Fact]
    public async Task DeleteIntakeDesk_RacingAnUpdate_IsRefusedAndKeepsTheDesk()
    {
        // Arrange — the delete reads the desk, then an update commits before the delete saves
        var ct = TestContext.Current.CancellationToken;
        Employee administrator;
        IntakeDesk desk;
        await using (var seeding = _fixture.CreateContext())
        {
            administrator = SeedEmployee(seeding);
            desk = IntakeDesk.Create($"Desk-{Guid.NewGuid():N}"[..30], "Anything", IntakeDeskVisibility.Open, true, true, true, [],
                EventActor.System, Now);
            seeding.IntakeDesks.Add(desk);
            await seeding.SaveChangesAsync(ct);
        }

        await using var deleting = _fixture.CreateContext();
        await deleting.IntakeDesks.SingleAsync(d => d.Id == desk.Id, ct);
        await using (var updating = _fixture.CreateContext())
        {
            var changed = await updating.IntakeDesks.SingleAsync(d => d.Id == desk.Id, ct);
            changed.Close(EventActor.System, Now);
            await updating.SaveChangesAsync(ct);
        }

        // Act — the deleting context still tracks the desk as it read it
        var result = await new DeleteIntakeDeskCommandHandler(deleting, Principal(administrator.Id), User(), _clock)
            .Handle(new DeleteIntakeDeskCommand(desk.Id), ct);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskAccess.StaleError);
        await using var reader = _fixture.CreateContext();
        (await reader.IntakeDesks.AnyAsync(d => d.Id == desk.Id, ct)).Should().BeTrue();
    }

    [Fact]
    public async Task TheDatabase_RefusesAnAssigneeWhoDoesNotWorkTheRequestsDesk_EitherWayRound()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        Employee agent, outsider;
        IntakeDesk desk;
        Guid requestId;
        await using (var seeding = _fixture.CreateContext())
        {
            agent = SeedEmployee(seeding);
            outsider = SeedEmployee(seeding);
            desk = IntakeDesk.Create($"Desk-{Guid.NewGuid():N}"[..30], "Anything", IntakeDeskVisibility.Open, true, true, true,
                [new IntakeDeskMemberDetails(agent.Id, IntakeDeskRole.Agent)], EventActor.System, Now);
            var channel = IntakeChannel.Create($"Channel-{Guid.NewGuid():N}"[..30], null, 1);
            seeding.IntakeDesks.Add(desk);
            seeding.IntakeChannels.Add(channel);
            await seeding.SaveChangesAsync(ct);
            var request = IntakeRequest.Create("Title", "Description", null, agent.Id, agent.Id, desk, channel, null, null, IntakeStatuses.Submitted(), EventActor.System, Now).Value;
            seeding.IntakeRequests.Add(request);
            await seeding.SaveChangesAsync(ct);
            requestId = request.Id;
        }

        // Act — assigning someone not on the desk, as an assignment racing their removal would
        await using var assigning = _fixture.CreateContext();
        var toAssign = await assigning.IntakeRequests.SingleAsync(r => r.Id == requestId, ct);
        toAssign.ChangeAssignee(outsider.Id, EventActor.System, Now);
        var assignToOutsider = () => assigning.SaveChangesAsync(ct);

        // Act — removing a member who still owns a request on the desk, as a removal racing an assignment would
        await using var removing = _fixture.CreateContext();
        var owned = await removing.IntakeRequests.SingleAsync(r => r.Id == requestId, ct);
        owned.ChangeAssignee(agent.Id, EventActor.System, Now);
        await removing.SaveChangesAsync(ct);
        await using var removingMember = _fixture.CreateContext();
        var deskToChange = await removingMember.IntakeDesks.Include(d => d.Members).SingleAsync(d => d.Id == desk.Id, ct);
        deskToChange.ChangeMembers([], EventActor.System, Now);
        var removeOwner = () => removingMember.SaveChangesAsync(ct);

        // Assert
        await assignToOutsider.Should().ThrowAsync<DbUpdateException>();
        await removeOwner.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task DeleteIntakeDesk_RemovesAnEmptyDeskAndItsMembers()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        IntakeDesk desk;
        Employee administrator;
        await using (var seeding = _fixture.CreateContext())
        {
            administrator = SeedEmployee(seeding);
            var agent = SeedEmployee(seeding);
            desk = IntakeDesk.Create($"Desk-{Guid.NewGuid():N}"[..30], "Anything", IntakeDeskVisibility.Open, true, true, true,
                [new IntakeDeskMemberDetails(agent.Id, IntakeDeskRole.Agent)], EventActor.System, Now);
            seeding.IntakeDesks.Add(desk);
            await seeding.SaveChangesAsync(ct);
        }

        // Act
        await using var context = _fixture.CreateContext();
        var result = await new DeleteIntakeDeskCommandHandler(context, Principal(administrator.Id), User(), _clock)
            .Handle(new DeleteIntakeDeskCommand(desk.Id), ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await using var reader = _fixture.CreateContext();
        (await reader.IntakeDesks.AnyAsync(d => d.Id == desk.Id, ct)).Should().BeFalse();
        (await reader.ActivityLogs.AnyAsync(a => a.AggregateId == desk.Id && a.EventType == nameof(IntakeDeskDeletedEvent), ct)).Should().BeTrue();
    }

    [Fact]
    public async Task RerouteIntakeRequest_MovesItAndUnassignsAnAssigneeNotOnTheDestination()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        Employee agent;
        IntakeDesk from, to;
        Guid requestId;
        await using (var seeding = _fixture.CreateContext())
        {
            agent = SeedEmployee(seeding);
            from = IntakeDesk.Create($"Desk-{Guid.NewGuid():N}"[..30], "Anything", IntakeDeskVisibility.Private, true, true, true,
                [new IntakeDeskMemberDetails(agent.Id, IntakeDeskRole.Agent)], EventActor.System, Now);
            to = IntakeDesk.Create($"Desk-{Guid.NewGuid():N}"[..30], "Anything", IntakeDeskVisibility.Open, true, true, true, [],
                EventActor.System, Now);
            var channel = IntakeChannel.Create($"Channel-{Guid.NewGuid():N}"[..30], null, 1);
            seeding.IntakeDesks.AddRange(from, to);
            seeding.IntakeChannels.Add(channel);
            await seeding.SaveChangesAsync(ct);
            var request = IntakeRequest.Create("Title", "Description", null, agent.Id, agent.Id, from, channel, null, null, IntakeStatuses.Submitted(), EventActor.System, Now).Value;
            seeding.IntakeRequests.Add(request);
            await seeding.SaveChangesAsync(ct);
            request.ChangeAssignee(agent.Id, EventActor.System, Now);
            await seeding.SaveChangesAsync(ct);
            requestId = request.Id;
        }

        // Act — private to open widens who can read it, so the move is confirmed
        await using var context = _fixture.CreateContext();
        var result = await new RerouteIntakeRequestCommandHandler(context, Principal(agent.Id), User(), _clock,
                await IntakeStatuses.SeededResolver(context, ct))
            .Handle(new RerouteIntakeRequestCommand(requestId, to.Id, ConfirmWiderVisibility: true), ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await using var reader = _fixture.CreateContext();
        var saved = await reader.IntakeRequests.AsNoTracking().SingleAsync(r => r.Id == requestId, ct);
        saved.DeskId.Should().Be(to.Id);
        saved.AssigneeId.Should().BeNull();
        var rerouted = await reader.ActivityLogs.AsNoTracking()
            .SingleAsync(a => a.AggregateId == requestId && a.EventType == nameof(IntakeRequestReroutedEvent), ct);
        rerouted.Payload.Should().Contain($"\"fromDeskId\":\"{from.Id}\"").And.Contain("\"toVisibility\":\"open\"");
    }

    /// <summary>
    /// A desk with the agent on it and a request on that desk in the seeded default workflow's Submitted, as
    /// submitting one leaves it.
    /// </summary>
    private async Task<(IntakeDesk Desk, Guid RequestId, Employee Requester, Employee Agent)> SeedOnTheDefaultWorkflow(
        CancellationToken ct, IntakeDeskRole agentRole = IntakeDeskRole.Agent)
    {
        await using var seeding = _fixture.CreateContext();
        var resolver = await IntakeStatuses.SeededResolver(seeding, ct);
        var requester = SeedEmployee(seeding);
        var agent = SeedEmployee(seeding);
        var desk = IntakeDesk.Create($"Desk-{Guid.NewGuid():N}"[..30], "Anything", IntakeDeskVisibility.Open, true, true, true,
            [new IntakeDeskMemberDetails(agent.Id, agentRole)], EventActor.System, Now);
        var channel = IntakeChannel.Create($"Channel-{Guid.NewGuid():N}"[..30], null, 1);
        seeding.IntakeDesks.Add(desk);
        seeding.IntakeChannels.Add(channel);
        await seeding.SaveChangesAsync(ct);

        var submitted = await resolver.ForAlias(DemandManagementWorkflowOwners.IntakeRequest.Key, desk.Id, (int)IntakeRequestStatusAlias.Submitted, ct);
        var request = IntakeRequest.Create("Title", "Description", null, requester.Id, requester.Id, desk, channel, null, null,
            submitted.Value, EventActor.System, Now).Value;
        seeding.IntakeRequests.Add(request);
        await seeding.SaveChangesAsync(ct);

        return (desk, request.Id, requester, agent);
    }

    private static async Task<Guid> StatusId(IStatusResolver resolver, Guid deskId, string name, CancellationToken ct) =>
        (await resolver.ForScope(DemandManagementWorkflowOwners.IntakeRequest.Key, deskId, ct)).Value.Statuses.Single(s => s.Name == name).Id;

    [Fact]
    public async Task ChangeIntakeRequestStatus_ToDeclined_RecordsTheReasonOnTheRequestAndItsHistory_AndTakesItOffTheDesksWaitingCount()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (desk, requestId, _, agent) = await SeedOnTheDefaultWorkflow(ct, IntakeDeskRole.Admin);

        // Act
        await using var context = _fixture.CreateContext();
        var resolver = new StatusResolver(context);
        var result = await new ChangeIntakeRequestStatusCommandHandler(context, Principal(agent.Id), User(), _clock, resolver)
            .Handle(new ChangeIntakeRequestStatusCommand(requestId, await StatusId(resolver, desk.Id, "Declined", ct), "Budget went elsewhere", null), ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await using var reader = _fixture.CreateContext();
        var saved = await reader.IntakeRequests.AsNoTracking().SingleAsync(r => r.Id == requestId, ct);
        saved.StatusAlias.Should().Be(IntakeRequestStatusAlias.Declined);
        saved.StatusReason.Should().Be("Budget went elsewhere");
        saved.StatusTransitionCount.Should().Be(2);
        var transitions = await reader.StatusTransitions.AsNoTracking()
            .Where(t => t.OwnerType == DemandManagementWorkflowOwners.IntakeRequest.Key && t.RecordId == requestId)
            .OrderBy(t => t.Sequence)
            .ToListAsync(ct);
        transitions.Select(t => t.ToStatusName).Should().Equal("Submitted", "Declined");
        transitions[1].Reason.Should().Be("Budget went elsewhere");
        (await reader.ActivityLogs.AnyAsync(a => a.AggregateId == requestId && a.EventType == nameof(IntakeRequestStatusChangedEvent), ct))
            .Should().BeTrue();

        // A closed request is no longer waiting, and the desk still cannot be deleted while it holds one.
        var deskView = await new GetIntakeDeskQueryHandler(reader, Principal(agent.Id))
            .Handle(new GetIntakeDeskQuery(new IdOrKey(desk.Key)), ct);
        deskView!.Waiting!.Count.Should().Be(0);
        deskView.Waiting.OldestSubmittedOn.Should().BeNull();
        var asAdministrator = await new GetIntakeDeskQueryHandler(reader, Principal(agent.Id, IntakeRequestAuthorization.AdministerPermission))
            .Handle(new GetIntakeDeskQuery(new IdOrKey(desk.Key)), ct);
        asAdministrator!.CanDelete.Should().BeFalse();
    }

    [Fact]
    public async Task ChangeIntakeRequestStatus_ToDuplicate_PointsAtTheSurvivor_WhichThenCannotBeDeleted()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (desk, duplicateId, _, agent) = await SeedOnTheDefaultWorkflow(ct, IntakeDeskRole.Admin);
        Guid survivorId;
        await using (var seeding = _fixture.CreateContext())
        {
            var original = await seeding.IntakeRequests.AsNoTracking().SingleAsync(r => r.Id == duplicateId, ct);
            var submitted = await new StatusResolver(seeding).ForAlias(DemandManagementWorkflowOwners.IntakeRequest.Key, desk.Id, (int)IntakeRequestStatusAlias.Submitted, ct);
            var survivor = IntakeRequest.Create("The first ask", "Description", null, original.RequesterId, original.RequesterId,
                await seeding.IntakeDesks.SingleAsync(d => d.Id == desk.Id, ct),
                await seeding.IntakeChannels.SingleAsync(c => c.Id == original.ChannelId, ct),
                null, null, submitted.Value, EventActor.System, Now).Value;
            seeding.IntakeRequests.Add(survivor);
            await seeding.SaveChangesAsync(ct);
            survivorId = survivor.Id;
        }

        // Act
        await using var context = _fixture.CreateContext();
        var resolver = new StatusResolver(context);
        var marked = await new ChangeIntakeRequestStatusCommandHandler(context, Principal(agent.Id), User(), _clock, resolver)
            .Handle(new ChangeIntakeRequestStatusCommand(duplicateId, await StatusId(resolver, desk.Id, "Duplicate", ct), "Asked for already", survivorId), ct);
        await using var deleting = _fixture.CreateContext();
        var deleted = await new DeleteIntakeRequestCommandHandler(deleting, Principal(agent.Id), User(), _clock)
            .Handle(new DeleteIntakeRequestCommand(survivorId), ct);

        // Assert
        marked.IsSuccess.Should().BeTrue();
        deleted.IsFailure.Should().BeTrue();
        deleted.Error.Should().Be("Other requests are marked duplicates of this one, so it cannot be deleted.");
        await using var reader = _fixture.CreateContext();
        var details = await new GetIntakeRequestQueryHandler(reader, Principal(agent.Id))
            .Handle(new GetIntakeRequestQuery(new IdOrKey(duplicateId)), ct);
        details!.DuplicateOf!.Id.Should().Be(survivorId);
        details.DuplicateOf.Title.Should().Be("The first ask");
        details.Substance!.StatusReason.Should().Be("Asked for already");
        var survivorDetails = await new GetIntakeRequestQueryHandler(reader, Principal(agent.Id))
            .Handle(new GetIntakeRequestQuery(new IdOrKey(survivorId)), ct);
        survivorDetails!.CanDelete.Should().BeFalse();
    }

    [Fact]
    public async Task WithdrawIntakeRequest_ByTheRequester_ClosesItWithTheirNote()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (_, requestId, requester, _) = await SeedOnTheDefaultWorkflow(ct);

        // Act
        await using var context = _fixture.CreateContext();
        var resolver = new StatusResolver(context);
        var result = await new WithdrawIntakeRequestCommandHandler(context, Principal(requester.Id), User(), _clock, resolver)
            .Handle(new WithdrawIntakeRequestCommand(requestId, "Solved it another way"), ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await using var reader = _fixture.CreateContext();
        var saved = await reader.IntakeRequests.AsNoTracking().SingleAsync(r => r.Id == requestId, ct);
        saved.StatusAlias.Should().Be(IntakeRequestStatusAlias.Withdrawn);
        saved.StatusCategory.Should().Be(StatusCategory.Removed);
        saved.StatusReason.Should().Be("Solved it another way");
        (await reader.ActivityLogs.AnyAsync(a => a.AggregateId == requestId && a.EventType == nameof(IntakeRequestWithdrawnEvent), ct))
            .Should().BeTrue();
    }

    [Fact]
    public async Task RerouteIntakeRequest_IntoAChosenStatus_MovesBothTogether()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (desk, requestId, _, agent) = await SeedOnTheDefaultWorkflow(ct);
        IntakeDesk to;
        await using (var seeding = _fixture.CreateContext())
        {
            to = IntakeDesk.Create($"Desk-{Guid.NewGuid():N}"[..30], "Anything", IntakeDeskVisibility.Open, true, true, true, [],
                EventActor.System, Now);
            seeding.IntakeDesks.Add(to);
            await seeding.SaveChangesAsync(ct);
        }

        // Act
        await using var context = _fixture.CreateContext();
        var resolver = new StatusResolver(context);
        var result = await new RerouteIntakeRequestCommandHandler(context, Principal(agent.Id), User(), _clock, resolver)
            .Handle(new RerouteIntakeRequestCommand(requestId, to.Id, false, await StatusId(resolver, desk.Id, "In Review", ct)), ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await using var reader = _fixture.CreateContext();
        var saved = await reader.IntakeRequests.AsNoTracking().SingleAsync(r => r.Id == requestId, ct);
        saved.DeskId.Should().Be(to.Id);
        saved.StatusName.Should().Be("In Review");
        var events = await reader.ActivityLogs.AsNoTracking()
            .Where(a => a.AggregateId == requestId)
            .OrderBy(a => a.Timestamp).ThenBy(a => a.Ordinal)
            .Select(a => a.EventType)
            .ToListAsync(ct);
        // The request was created at the same test instant in an earlier save, so only the move's own events are ordered.
        events.Where(e => e != nameof(IntakeRequestCreatedEvent))
            .Should().Equal(nameof(IntakeRequestStatusChangedEvent), nameof(IntakeRequestReroutedEvent));
    }

    [Fact]
    public async Task ADeskWithItsOwnWorkflow_StartsItsRequestsThere_AndAMoveToADefaultDeskSwitchesThemBack()
    {
        // Arrange — a desk chooses a copy of the default before anything is submitted to it
        var ct = TestContext.Current.CancellationToken;
        Employee requester, deskAdmin;
        IntakeDesk ownDesk, defaultDesk;
        IntakeChannel channel;
        StatusWorkflow copy;
        await using (var seeding = _fixture.CreateContext())
        {
            await IntakeStatuses.SeededResolver(seeding, ct);
            requester = SeedEmployee(seeding);
            deskAdmin = SeedEmployee(seeding);
            ownDesk = IntakeDesk.Create($"Desk-{Guid.NewGuid():N}"[..30], "Anything", IntakeDeskVisibility.Open, true, true, true,
                [new IntakeDeskMemberDetails(deskAdmin.Id, IntakeDeskRole.Admin)], EventActor.System, Now);
            defaultDesk = IntakeDesk.Create($"Desk-{Guid.NewGuid():N}"[..30], "Anything", IntakeDeskVisibility.Open, true, true, true, [],
                EventActor.System, Now);
            channel = IntakeChannel.Create($"Channel-{Guid.NewGuid():N}"[..30], null, 1);
            seeding.IntakeDesks.AddRange(ownDesk, defaultDesk);
            seeding.IntakeChannels.Add(channel);

            var seeded = await seeding.StatusWorkflows.Include(w => w.Statuses).Include(w => w.Transitions)
                .SingleAsync(w => w.OwnerType == DemandManagementWorkflowOwners.IntakeRequest.Key && w.IsSystem, ct);
            copy = seeded.Clone($"Board Review {Guid.NewGuid():N}"[..30], null, EventActor.System, Now);
            copy.Publish(EventActor.System, Now).IsSuccess.Should().BeTrue();
            seeding.StatusWorkflows.Add(copy);
            await seeding.SaveChangesAsync(ct);
        }

        // Act
        await using var context = _fixture.CreateContext();
        var assignments = new ScopedWorkflowAssignments(context);
        var chosen = await new ChangeIntakeDeskWorkflowCommandHandler(context, Principal(deskAdmin.Id), User(), _clock, assignments)
            .Handle(new ChangeIntakeDeskWorkflowCommand(ownDesk.Id, copy.Id), ct);
        var resolver = new StatusResolver(context);
        var created = await new CreateIntakeRequestCommandHandler(context, Principal(requester.Id), User(), _clock, resolver)
            .Handle(new CreateIntakeRequestCommand("Export invoices", "Finance needs a CSV export", null, ownDesk.Id, channel.Id, null, null), ct);
        var nowHasRequests = await new ChangeIntakeDeskWorkflowCommandHandler(context, Principal(deskAdmin.Id), User(), _clock, assignments)
            .Handle(new ChangeIntakeDeskWorkflowCommand(ownDesk.Id, (await assignments.Current(DemandManagementWorkflowOwners.IntakeRequest.Key, defaultDesk.Id, ct)).Value.Workflow.Id), ct);
        var moved = await new RerouteIntakeRequestCommandHandler(context, Principal(deskAdmin.Id), User(), _clock, resolver)
            .Handle(new RerouteIntakeRequestCommand(created.Value.Id, defaultDesk.Id, false), ct);

        // Assert
        chosen.IsSuccess.Should().BeTrue();
        created.IsSuccess.Should().BeTrue();
        nowHasRequests.IsFailure.Should().BeTrue();
        nowHasRequests.Error.Should().Be(ChangeIntakeDeskWorkflowCommandHandler.DeskHasRequestsError);
        moved.IsSuccess.Should().BeTrue();

        await using var reader = _fixture.CreateContext();
        var ownView = await new GetIntakeDeskWorkflowQueryHandler(reader, Principal(deskAdmin.Id), new ScopedWorkflowAssignments(reader))
            .Handle(new GetIntakeDeskWorkflowQuery(new IdOrKey(ownDesk.Id)), ct);
        ownView!.Workflow.Id.Should().Be(copy.Id);
        ownView.FollowsOrganizationDefault.Should().BeFalse();

        var transitions = await reader.StatusTransitions.AsNoTracking()
            .Where(t => t.OwnerType == DemandManagementWorkflowOwners.IntakeRequest.Key && t.RecordId == created.Value.Id)
            .OrderBy(t => t.Sequence)
            .ToListAsync(ct);
        transitions[0].WorkflowId.Should().Be(copy.Id, "the request started in its desk's own workflow");
        transitions[1].WorkflowId.Should().NotBe(copy.Id, "moving to a desk on the default switched it onto that workflow");
        transitions[1].ToStatusName.Should().Be("Submitted");
        (await reader.ActivityLogs.AnyAsync(a => a.AggregateId == created.Value.Id && a.EventType == nameof(StatusWorkflowSwitchedEvent), ct))
            .Should().BeTrue();
    }

    private static ICurrentPrincipal Principal(Guid employeeId, params string[] permissions)
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);
        principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string permission, CancellationToken _) => permissions.Contains(permission));
        principal.Setup(p => p.HoldsPermission(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string permission, CancellationToken _) => permissions.Contains(permission));
        return principal.Object;
    }

    private static IDateTimeProvider Clock()
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.Now).Returns(Now);
        return clock.Object;
    }

    private static ICurrentUser User()
    {
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.GetUserId()).Returns("intake-probe");
        return user.Object;
    }

    private static Employee SeedEmployee(WaydDbContext context)
    {
        var employee = Employee.Create(
            new PersonName("Intake", null, "Probe"),
            Guid.NewGuid().ToString("N")[..12],
            Now,
            new EmailAddress(Guid.NewGuid().ToString("N")[..12] + "@acme.example"),
            jobTitle: null,
            department: null,
            officeLocation: null,
            managerId: null,
            isActive: true,
            employeeType: null,
            EventActor.System,
            Now);

        context.Employees.Add(employee);
        return employee;
    }
}
