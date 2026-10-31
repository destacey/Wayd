using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.Events.Discussions;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Common.Domain.Events.Planning.PlanningIntervalObjectives;
using Wayd.Common.Domain.Events.Planning.PlanningIntervals;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.Events.StrategicManagement;

namespace Wayd.Infrastructure.Messaging;

/// <summary>
/// Decides how a raised domain event is delivered: <em>durably and asynchronously</em> (staged into the
/// Wolverine EF Core outbox, committed atomically with the entity change, dispatched post-commit on a
/// background thread) or <em>inline</em> (dispatched synchronously before the request returns, via
/// <c>EventPublisher.InvokeAsync</c>). Consumed by <c>BaseDbContext</c> when it drains domain events.
/// </summary>
/// <remarks>
/// <para>
/// Routing is an <strong>explicit allow-list, default inline</strong>. An event type absent from the list
/// is dispatched inline, which is the safe default: most domain events are cross-domain replication
/// projections (same-Id copies) that an in-request read or a follow-up command depends on, so making them
/// async would break read-your-writes. An event may only be added here if no <em>same-request</em> flow
/// reads or FK-references the projection, and its handler is idempotent (durable delivery is at-least-once).
/// </para>
/// <para>
/// A durable event may still have a <em>cross-request</em> window — a user creating an entity and then
/// immediately, in a separate request, referencing the not-yet-replicated projection. That is acceptable
/// only when it surfaces as a <em>clean failure</em> (a validation error), never a raw fault or corruption.
/// Where a downstream required FK exists, the referencing command must validate existence for the failure to
/// be clean: e.g. <c>TeamCreatedEvent</c> → <c>PlanningTeam</c> is only durable because
/// <c>ManagePlanningIntervalTeamsCommand</c> checks that each team's <c>PlanningTeam</c> projection exists
/// before inserting <c>PlanningIntervalTeam</c> rows. What must stay inline is a true <em>same-request</em>
/// dependency — a projection an in-request read or the very same command relies on.
/// </para>
/// </remarks>
public static class DurableEventRoutes
{
    /// <summary>
    /// The event types delivered durably/asynchronously through the outbox. A concrete-<see cref="Type"/>
    /// set (not string names) so a renamed or moved event is a compile error rather than a silent revert to
    /// inline dispatch. Matching is on the exact runtime type, as Wolverine dispatches on the concrete
    /// message type.
    /// </summary>
    private static readonly HashSet<Type> DurableEventTypes =
    [
        // PPM Project → Work WorkProject and Demand Management DemandProject. The key change is here for the
        // same reason as the details change: the projections store the key, and a rekeyed project would
        // otherwise keep its old one. Adding an outcome checks the DemandProject exists, so a project not yet
        // copied fails cleanly.
        typeof(ProjectCreatedEvent),
        typeof(ProjectDetailsUpdatedEvent),
        typeof(ProjectKeyChangedEventV2),
        typeof(ProjectDeletedEvent),

        // Superseded, but an envelope written as it before the switch may still be in the outbox; routed
        // durably so it is delivered to the handler that still accepts it.
#pragma warning disable CS0618
        typeof(ProjectKeyChangedEvent),
#pragma warning restore CS0618

        // Work Iteration → Planning PlanningSprint.
        typeof(IterationCreatedEventV3),
        typeof(IterationDetailsUpdatedEvent),
        typeof(IterationDateRangeChangedEventV2),
        typeof(IterationTeamChangedEvent),
        typeof(IterationDeletedEvent),

        // Superseded by the events above, or no longer raised; routed durably for envelopes written before the switch.
#pragma warning disable CS0618
        typeof(IterationCreatedEventV2),
        typeof(IterationStateChangedEvent),
        typeof(IterationCreatedEvent),
        typeof(IterationDateRangeChangedEvent),
        typeof(IterationUpdatedEvent),
#pragma warning restore CS0618

        // StrategicManagement StrategicTheme → PPM PpmStrategicThemes.
        typeof(StrategicThemeCreatedEvent),
        typeof(StrategicThemeDetailsUpdatedEvent),
        typeof(StrategicThemeActivatedEvent),
        typeof(StrategicThemeArchivedEvent),
        typeof(StrategicThemeDeletedEvent),

        // Superseded by StrategicThemeDetailsUpdatedEvent; routed durably for envelopes written before the switch.
#pragma warning disable CS0618
        typeof(StrategicThemeUpdatedEvent),
#pragma warning restore CS0618

        // Work WorkProcess integration-state flip → AppIntegration connection config (no projection, no FK,
        // no in-request reader — genuinely fire-and-forget). Closed generic; routed on the concrete type.
        typeof(IntegrationStateChangedEvent<Guid>),

        // Organization Team → PlanningTeam / WorkTeam / PpmTeam / ProductTeam. The one cross-domain edge with a downstream
        // required FK (PlanningIntervalTeam.TeamId → PlanningTeam); ManagePlanningIntervalTeamsCommand now
        // validates team existence and fails cleanly rather than FK-faulting, so async replication is safe.
        // TeamDeactivatedEvent also ends every employee's allocations to the team, in the same handler chain
        // as the copies: a failure there redelivers to all of them.
        typeof(TeamCreatedEvent),
        typeof(TeamDetailsUpdatedEvent),
        typeof(TeamActivatedEvent),
        typeof(TeamDeactivatedEvent),

        // Planning: a team joining a planning interval → its sprints mapped automatically. A follow-up, not
        // something the request that added the team reads, and a failure must not undo adding it.
        typeof(PlanningIntervalTeamsChangedEvent),

        // Organization Employee → their EmployeeCapacity, whose allocations end when they are deactivated. No
        // in-request reader; a people sync deactivates many at once and none may be lost.
        typeof(EmployeeDeactivatedEvent),

        // Changes people are told about → their notifications. Nothing in the request that made the change
        // reads a notification, and a failure to write one must not fail that request. ProjectCreatedEvent is
        // routed above for the Work projection and notifies too.
        typeof(ProjectStatusChangedEventV2),
        typeof(ProjectLifecycleChangedEventV2),
        typeof(ProjectHealthCheckAddedEventV2),
        typeof(ProjectTimelineChangedEventV2),
        typeof(ProjectRolesChangedEventV2),
        typeof(ProjectReparentedEventV2),
        typeof(ProjectScoreRecordedEventV2),
        typeof(ProgramCreatedEvent),
        typeof(ProgramStatusChangedEvent),
        typeof(ProgramTimelineChangedEvent),
        typeof(ProgramRolesChangedEvent),
        typeof(PlanningIntervalObjectiveCreatedEvent),
        typeof(PlanningIntervalObjectiveStatusChangedEvent),
        typeof(PlanningIntervalObjectiveHealthCheckAddedEvent),
        typeof(PlanningIntervalObjectiveTimelineChangedEvent),
        typeof(IntakeRequestStatusChangedEvent),
        typeof(IntakeRequestWithdrawnEvent),
        typeof(IntakeRequestRetitledEvent),
        typeof(IntakeRequestDetailsUpdatedEvent),
        typeof(IntakeRequestNeedByChangedEvent),
        typeof(IntakeRequestAssigneeChangedEvent),
        typeof(IntakeRequestReroutedEvent),
        typeof(IntakeRequestParticipantAddedEvent),
        typeof(IntakeRequestParticipantRemovedEvent),
        typeof(IntakeRequestChannelChangedEventV2),

        // Notifications only: nothing in the posting request reads what a message's handlers write.
        typeof(DiscussionMessagePostedEvent),

        // Superseded by TeamDetailsUpdatedEvent; routed durably for envelopes written before the switch.
#pragma warning disable CS0618
        typeof(TeamUpdatedEvent),
#pragma warning restore CS0618
    ];

    /// <summary>
    /// Whether <paramref name="event"/> should be enlisted in the durable outbox rather than dispatched
    /// inline. Matches on the concrete runtime type.
    /// </summary>
    public static bool IsDurable(IEvent @event) => DurableEventTypes.Contains(@event.GetType());

    /// <summary>
    /// Whether <paramref name="messageType"/> is a durable event type. Used at bootstrap to scope
    /// <see cref="DurableEventFailurePolicy"/> to the matching handler chains, so routing and failure
    /// handling stay driven by the one allow-list.
    /// </summary>
    public static bool IsDurableMessageType(Type messageType) => DurableEventTypes.Contains(messageType);
}
