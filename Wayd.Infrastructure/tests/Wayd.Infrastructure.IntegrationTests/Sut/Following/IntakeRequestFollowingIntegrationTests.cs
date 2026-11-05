using Moq;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Discussions;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Following.Dtos;
using Wayd.Common.Application.Following.Queries;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Notifications.Dtos;
using Wayd.Common.Application.Notifications.Queries;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.Following;
using Wayd.Common.Domain.Notifications;
using Wayd.Common.Models;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Domain;
using Wayd.DemandManagement.Domain.Models;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Notifications;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Following;

/// <summary>
/// Proves a follower stops seeing an intake request, and every notification about it, the moment they can no
/// longer read it.
/// </summary>
/// <remarks>
/// The read rule exists for these cases: a request rerouted to a private desk the follower is not on, a
/// request deleted, and a participant removed from a private desk's request. Each is reached through its real
/// command, and read back through the real Following and notification queries, so the desk-privacy rule is
/// proved where it runs — in SQL — rather than over an in-memory set.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class IntakeRequestFollowingIntegrationTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 27, 9, 0);

    private readonly SqlServerDbContextFixture _fixture = RegisterOwners(fixture);

    private static SqlServerDbContextFixture RegisterOwners(SqlServerDbContextFixture fixture)
    {
        DemandManagementWorkflowOwners.Register();
        DemandManagementFollowOwners.Register();
        DemandManagementDiscussionChannels.Register();
        return fixture;
    }

    private static string OwnerType => DemandManagementFollowOwners.IntakeRequest.Key;

    [Fact]
    public async Task AFollower_StopsSeeingARequestAndItsNotifications_WhenTheyCanNoLongerReadIt()
    {
        // Arrange — the follower browses with View and is a participant on one private desk's request. An agent
        // runs both desks. Four followed requests, each with an unread notification: one stays readable, the
        // others are rerouted to the private desk, deleted, and have the follower removed as a participant.
        var ct = TestContext.Current.CancellationToken;
        Employee follower, agent;
        Guid keptId, reroutedId, deletedId, sharedId, privateDeskId;
        await using (var seed = _fixture.CreateContext())
        {
            follower = SeedEmployee(seed);
            agent = SeedEmployee(seed);
            var openDesk = IntakeDesk.Create($"Desk-{Guid.NewGuid():N}"[..30], "Anything", IntakeDeskVisibility.Open, true, true, true,
                [new IntakeDeskMemberDetails(agent.Id, IntakeDeskRole.Admin)], EventActor.System, Now);
            var privateDesk = IntakeDesk.Create($"Desk-{Guid.NewGuid():N}"[..30], "Anything", IntakeDeskVisibility.Private, true, true, true,
                [new IntakeDeskMemberDetails(agent.Id, IntakeDeskRole.Agent)], EventActor.System, Now);
            var channel = IntakeChannel.Create($"Channel-{Guid.NewGuid():N}"[..30], null, 1, EventActor.System, Now);
            seed.IntakeDesks.AddRange(openDesk, privateDesk);
            seed.IntakeChannels.Add(channel);
            await seed.SaveChangesAsync(ct);

            IntakeRequest Request(IntakeDesk desk, string title) =>
                IntakeRequest.Create(title, "Description", null, agent.Id, agent.Id, desk, channel, null, null,
                    IntakeStatuses.Submitted(), EventActor.System, Now).Value;

            var kept = Request(openDesk, "Kept");
            var rerouted = Request(openDesk, "Rerouted");
            var deleted = Request(openDesk, "Deleted");
            var shared = Request(privateDesk, "Shared");
            shared.AddParticipant(follower.Id, agent.Id, EventActor.System, Now);
            seed.IntakeRequests.AddRange(kept, rerouted, deleted, shared);
            await seed.SaveChangesAsync(ct);

            var store = new NotificationStore(seed);
            foreach (var request in new[] { kept, rerouted, deleted, shared })
            {
                seed.Follows.Add(Follow.Create(OwnerType, request.Id, follower.Id, Now).Value);
                await store.Add([Notification.Create(follower.Id, OwnerType, request.Id, Guid.CreateVersion7(),
                    NotificationEvent.StatusChanged, EventActorKind.User, agent.Id, Now, NotificationReason.Following).Value], ct);
            }
            await seed.SaveChangesAsync(ct);

            (keptId, reroutedId, deletedId, sharedId, privateDeskId) = (kept.Id, rerouted.Id, deleted.Id, shared.Id, privateDesk.Id);
        }

        // The follower reads all four before anything changes.
        (await FollowedIds(follower.Id, ct)).Should().BeEquivalentTo([keptId, reroutedId, deletedId, sharedId]);

        await using (var act = _fixture.CreateContext())
        {
            // Act
            var resolver = await IntakeStatuses.SeededResolver(act, ct);
            var moved = await new RerouteIntakeRequestCommandHandler(act, Principal(agent.Id), User(), Clock(), resolver)
                .Handle(new RerouteIntakeRequestCommand(reroutedId, privateDeskId, ConfirmWiderVisibility: false), ct);
            var removed = await new DeleteIntakeRequestCommandHandler(act, Principal(agent.Id), User(), Clock(),
                    new DiscussionWriter(act, [], Principal(agent.Id), User(), Clock()))
                .Handle(new DeleteIntakeRequestCommand(deletedId), ct);
            var unshared = await new RemoveIntakeRequestParticipantCommandHandler(act, Principal(agent.Id), User(), Clock())
                .Handle(new RemoveIntakeRequestParticipantCommand(sharedId, follower.Id), ct);

            new[] { moved, removed, unshared }.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error : null));
        }

        // Assert
        (await FollowedIds(follower.Id, ct)).Should().Equal(keptId);

        await using var context = _fixture.CreateContext();
        var principal = Principal(follower.Id, IntakeRequestAuthorization.ViewPermission);
        var readRules = ReadRules(context, principal);
        var notifications = await new GetMyNotificationsQueryHandler(context, context, readRules, principal)
            .Handle(new GetMyNotificationsQuery(new CursorRequest()), ct);
        var unread = await new GetMyUnreadNotificationCountQueryHandler(context, readRules, principal)
            .Handle(new GetMyUnreadNotificationCountQuery(), ct);

        notifications.Items.Select(n => n.Record.Id).Should().Equal(keptId);
        unread.Count.Should().Be(1);
    }

    [Fact]
    public async Task AFollower_StopsSeeingEveryRequest_WhenDemandManagementIsTurnedOff()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        Employee follower;
        await using (var seed = _fixture.CreateContext())
        {
            follower = SeedEmployee(seed);
            var desk = IntakeDesk.Create($"Desk-{Guid.NewGuid():N}"[..30], "Anything", IntakeDeskVisibility.Open, true, true, true,
                [], EventActor.System, Now);
            var channel = IntakeChannel.Create($"Channel-{Guid.NewGuid():N}"[..30], null, 1, EventActor.System, Now);
            seed.IntakeDesks.Add(desk);
            seed.IntakeChannels.Add(channel);
            await seed.SaveChangesAsync(ct);
            var request = IntakeRequest.Create("Mine", "Description", null, follower.Id, follower.Id, desk, channel, null, null,
                IntakeStatuses.Submitted(), EventActor.System, Now).Value;
            seed.IntakeRequests.Add(request);
            await seed.SaveChangesAsync(ct);
            seed.Follows.Add(Follow.Create(OwnerType, request.Id, follower.Id, Now).Value);
            await seed.SaveChangesAsync(ct);
        }

        // Act
        var whileOn = await FollowedIds(follower.Id, ct);
        var whileOff = await FollowedIds(follower.Id, ct, demandManagementEnabled: false);

        // Assert
        whileOn.Should().ContainSingle();
        whileOff.Should().BeEmpty();
    }

    [Fact]
    public async Task AFollowerWithOnlyTheSummary_SeesNoneOfTheSubstancesHistory_InTheDetailOrWhenItLastChanged()
    {
        // Arrange — a summary-only desk: someone holding View reads the summary and none of the substance. The
        // agent assigns the request (summary), then the requester rewrites its description (substance).
        var ct = TestContext.Current.CancellationToken;
        var assignedOn = Now.Plus(Duration.FromHours(1));
        var editedOn = Now.Plus(Duration.FromHours(2));
        Employee follower, agent, requester;
        Guid requestId;
        await using (var seed = _fixture.CreateContext())
        {
            follower = SeedEmployee(seed);
            agent = SeedEmployee(seed);
            requester = SeedEmployee(seed);
            var desk = IntakeDesk.Create($"Desk-{Guid.NewGuid():N}"[..30], "Anything", IntakeDeskVisibility.SummaryOnly, true, true, true,
                [new IntakeDeskMemberDetails(agent.Id, IntakeDeskRole.Agent)], EventActor.System, Now);
            var channel = IntakeChannel.Create($"Channel-{Guid.NewGuid():N}"[..30], null, 1, EventActor.System, Now);
            seed.IntakeDesks.Add(desk);
            seed.IntakeChannels.Add(channel);
            await seed.SaveChangesAsync(ct);

            var request = IntakeRequest.Create("Export invoices", "Finance needs a CSV export", null, requester.Id, requester.Id, desk,
                channel, null, null, IntakeStatuses.Submitted(), EventActor.User("requester", requester.Id), Now).Value;
            seed.IntakeRequests.Add(request);
            await seed.SaveChangesAsync(ct);
            request.ChangeAssignee(agent.Id, EventActor.User("agent", agent.Id), assignedOn).IsSuccess.Should().BeTrue();
            request.UpdateDetails("The board's private budget figures", null,
                new IntakeRequestEditor(requester.Id, false, EventActor.User("requester", requester.Id)), editedOn).IsSuccess.Should().BeTrue();
            seed.Follows.Add(Follow.Create(OwnerType, request.Id, follower.Id, Now).Value);
            seed.Follows.Add(Follow.Create(OwnerType, request.Id, agent.Id, Now).Value);
            await seed.SaveChangesAsync(ct);
            requestId = request.Id;
        }

        // Act
        var asFollower = await FollowedRecord(follower.Id, requestId, ct, IntakeRequestAuthorization.ViewPermission);
        var asAgent = await FollowedRecord(agent.Id, requestId, ct);

        // Assert — the follower sees the assignment with nobody named on it, and the request last changed then
        asFollower.Record.LastChangedOn.Should().Be(assignedOn);
        var change = asFollower.ChangesSinceFollowed.Should().ContainSingle(c => c.Timestamp > Now).Subject;
        change.EventType.Should().Be(nameof(IntakeRequestAssigneeChangedEvent));
        change.Employee.Should().BeNull();
        asFollower.ChangesSinceFollowed.Should().NotContain(c => c.EventType == nameof(IntakeRequestDetailsUpdatedEvent));
        asFollower.ChangesSinceFollowed.Should().AllSatisfy(c => c.Payload.Should().NotContain("budget"));

        // The desk's agent holds the substance, so nothing is withheld from them.
        asAgent.Record.LastChangedOn.Should().Be(editedOn);
        asAgent.ChangesSinceFollowed.Should().Contain(c => c.EventType == nameof(IntakeRequestDetailsUpdatedEvent));
    }

    [Fact]
    public async Task AFollowerWithOnlyTheSummary_IsNotToldTheSubstanceChanged_OrWhoMadeAChange()
    {
        // Arrange — on a summary-only desk the follower was notified of an assignment (summary) and then of a
        // rewritten description (substance), both made by the requester; the agent was notified of both
        var ct = TestContext.Current.CancellationToken;
        var assignedOn = Now.Plus(Duration.FromHours(1));
        var editedOn = Now.Plus(Duration.FromHours(2));
        Employee follower, agent;
        Guid requestId;
        await using (var seed = _fixture.CreateContext())
        {
            follower = SeedEmployee(seed);
            agent = SeedEmployee(seed);
            var requester = SeedEmployee(seed);
            var desk = IntakeDesk.Create($"Desk-{Guid.NewGuid():N}"[..30], "Anything", IntakeDeskVisibility.SummaryOnly, true, true, true,
                [new IntakeDeskMemberDetails(agent.Id, IntakeDeskRole.Agent)], EventActor.System, Now);
            var channel = IntakeChannel.Create($"Channel-{Guid.NewGuid():N}"[..30], null, 1, EventActor.System, Now);
            seed.IntakeDesks.Add(desk);
            seed.IntakeChannels.Add(channel);
            await seed.SaveChangesAsync(ct);

            var request = IntakeRequest.Create("Export invoices", "Finance needs a CSV export", null, requester.Id, requester.Id, desk,
                channel, null, null, IntakeStatuses.Submitted(), EventActor.User("requester", requester.Id), Now).Value;
            seed.IntakeRequests.Add(request);
            await seed.SaveChangesAsync(ct);
            requestId = request.Id;

            var store = new NotificationStore(seed);
            foreach (var (notificationEvent, occurredOn) in new[] { (NotificationEvent.AssigneeChanged, assignedOn), (NotificationEvent.DetailsChanged, editedOn) })
            {
                var eventId = Guid.CreateVersion7();
                await store.Add([.. new[] { follower.Id, agent.Id }.Select(recipient => Notification.Create(recipient, OwnerType, requestId,
                    eventId, notificationEvent, EventActorKind.User, requester.Id, occurredOn, NotificationReason.Following).Value)], ct);
            }
        }

        // Act
        var asFollower = await Notifications(follower.Id, ct, IntakeRequestAuthorization.ViewPermission);
        var asAgent = await Notifications(agent.Id, ct);

        // Assert — the follower's entry leads with the assignment and counts it alone
        var entry = asFollower.Notifications.Items.Should().ContainSingle().Subject;
        entry.Event.Should().Be(NotificationEvent.AssigneeChanged);
        entry.OccurredOn.Should().Be(assignedOn);
        entry.Count.Should().Be(1);
        entry.Actor.Should().BeNull("who made a change is substance, which the follower cannot see");
        asFollower.Unread.Count.Should().Be(1);

        // The desk's agent holds the substance, so nothing is withheld from them.
        var agentEntry = asAgent.Notifications.Items.Should().ContainSingle().Subject;
        agentEntry.Event.Should().Be(NotificationEvent.DetailsChanged);
        agentEntry.Count.Should().Be(2);
        agentEntry.Actor.Should().NotBeNull();
    }

    /// <summary>The reader's notification list and unread count.</summary>
    private async Task<(CursorPage<NotificationDto> Notifications, UnreadNotificationCountDto Unread)> Notifications(
        Guid employeeId, CancellationToken ct, params string[] permissions)
    {
        await using var context = _fixture.CreateContext();
        var principal = Principal(employeeId, permissions);
        var readRules = ReadRules(context, principal);
        var notifications = await new GetMyNotificationsQueryHandler(context, context, readRules, principal)
            .Handle(new GetMyNotificationsQuery(new CursorRequest()), ct);
        var unread = await new GetMyUnreadNotificationCountQueryHandler(context, readRules, principal)
            .Handle(new GetMyUnreadNotificationCountQuery(), ct);
        return (notifications, unread);
    }

    /// <summary>The followed request in full, as the given reader opens it from their Following list.</summary>
    private async Task<FollowedRecordDetailDto> FollowedRecord(Guid employeeId, Guid requestId, CancellationToken ct, params string[] permissions)
    {
        await using var context = _fixture.CreateContext();
        var principal = Principal(employeeId, permissions);
        var result = await new GetFollowedRecordQueryHandler(context, context, new ActivityLogReader(context), ReadRules(context, principal), principal)
            .Handle(new GetFollowedRecordQuery(OwnerType, requestId), ct);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().NotBeNull();
        return result.Value!;
    }

    /// <summary>The ids on the follower's Following list, read as someone holding View.</summary>
    private async Task<List<Guid>> FollowedIds(Guid followerId, CancellationToken ct, bool demandManagementEnabled = true)
    {
        await using var context = _fixture.CreateContext();
        var principal = Principal(followerId, IntakeRequestAuthorization.ViewPermission);
        var result = await new GetMyFollowsQueryHandler(context, context, ReadRules(context, principal, demandManagementEnabled), principal, Clock())
            .Handle(new GetMyFollowsQuery(new PageRequest(1, 50), OwnerType), ct);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        return [.. result.Value.Items.Select(i => i.Id)];
    }

    private static IntakeRequestReadRule[] ReadRules(WaydDbContext context, ICurrentPrincipal principal, bool demandManagementEnabled = true)
    {
        var featureFlags = new Mock<IFeatureFlags>();
        featureFlags.Setup(f => f.IsEnabled(FeatureFlags.Names.DemandManagement, It.IsAny<CancellationToken>()))
            .ReturnsAsync(demandManagementEnabled);
        return [new IntakeRequestReadRule(context, principal, featureFlags.Object)];
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
        user.Setup(u => u.GetUserId()).Returns("intake-follower-probe");
        return user.Object;
    }

    private static Employee SeedEmployee(WaydDbContext context)
    {
        var employee = Employee.Create(
            new PersonName("Intake", null, "Follower"),
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
