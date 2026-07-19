using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Domain.Events.Planning.Iterations;
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
        // PPM Project → Work WorkProject.
        typeof(ProjectCreatedEvent),
        typeof(ProjectDetailsUpdatedEvent),
        typeof(ProjectDeletedEvent),

        // Planning Iteration → Work WorkIteration.
        typeof(IterationCreatedEvent),
        typeof(IterationUpdatedEvent),
        typeof(IterationDeletedEvent),

        // StrategicManagement StrategicTheme → PPM PpmStrategicThemes.
        typeof(StrategicThemeCreatedEvent),
        typeof(StrategicThemeUpdatedEvent),
        typeof(StrategicThemeDeletedEvent),

        // Work WorkProcess integration-state flip → AppIntegration connection config (no projection, no FK,
        // no in-request reader — genuinely fire-and-forget). Closed generic; routed on the concrete type.
        typeof(IntegrationStateChangedEvent<Guid>),

        // Organization Team → PlanningTeam / WorkTeam / PpmTeam. The one cross-domain edge with a downstream
        // required FK (PlanningIntervalTeam.TeamId → PlanningTeam); ManagePlanningIntervalTeamsCommand now
        // validates team existence and fails cleanly rather than FK-faulting, so async replication is safe.
        typeof(TeamCreatedEvent),
        typeof(TeamUpdatedEvent),
        typeof(TeamActivatedEvent),
        typeof(TeamDeactivatedEvent),
        typeof(TeamDeletedEvent),
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
