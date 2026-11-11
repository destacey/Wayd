using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Application.DemandProjects.Dtos;

namespace Wayd.DemandManagement.Application.IntakeRequests.Dtos;

/// <summary>
/// One thing an intake request became. Summary tier.
/// </summary>
public sealed record IntakeRequestOutcomeDto
{
    /// <summary>What the request became.</summary>
    public IntakeRequestOutcomeKind Kind { get; init; }

    /// <summary>The id of the project or work item the outcome points at, which identifies the outcome on the request.</summary>
    public Guid TargetId { get; init; }

    /// <summary>
    /// The project, for a project outcome; null for a work item outcome, or when the project has since been
    /// deleted, since the outcome stays as the record of what was decided.
    /// </summary>
    public DemandProjectNavigationDto? Project { get; init; }

    /// <summary>
    /// The work item, for a work item outcome; null for a project outcome, or when the work item has since been
    /// deleted.
    /// </summary>
    public IntakeRequestOutcomeWorkItemDto? WorkItem { get; init; }

    /// <summary>What the outcome covers, in the agent's words. Always present for existing project scope.</summary>
    public string? Note { get; init; }

    /// <summary>
    /// For existing project scope, the project's deliverables that meet the request, by name; empty for the other
    /// kinds.
    /// </summary>
    public IReadOnlyList<IntakeRequestOutcomeDeliverableDto> Deliverables { get; init; } = [];

    /// <summary>
    /// Whether the mapped deliverables meet the request: true when every one still on the project is Accepted,
    /// false while any is not (a descoped one included), and null when none is mapped. Derived when read.
    /// </summary>
    public bool? IsMet { get; init; }

    /// <summary>
    /// How far delivery of the outcome has got. A new project reports its lifecycle; a work item its status
    /// category; existing project scope rolls up the mapped deliverables still on the project, or reports the
    /// project's lifecycle when none is (see <c>isScopeUndefined</c>). Null once the project or work item
    /// has been deleted. Derived when read.
    /// </summary>
    public DeliverySignal? DeliverySignal { get; init; }

    /// <summary>
    /// Whether existing project scope has yet to say which of the project's deliverables meet the request: none
    /// mapped is still on the project, so <c>deliverySignal</c> follows the project as a whole. False for
    /// the other kinds and once the project has been deleted.
    /// </summary>
    public bool IsScopeUndefined { get; init; }

    /// <summary>The employee who recorded the outcome.</summary>
    public required EmployeeNavigationDto AddedBy { get; init; }

    /// <summary>When the outcome was recorded.</summary>
    public Instant AddedOn { get; init; }
}
