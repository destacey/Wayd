using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.DemandManagement.Application.IntakeDesks.Dtos;

/// <summary>
/// The review workflow a desk's requests follow, and whether the reader may point the desk at another.
/// </summary>
public sealed record IntakeDeskWorkflowDto
{
    /// <summary>The workflow governing the desk.</summary>
    public required IntakeWorkflowChoiceDto Workflow { get; init; }

    /// <summary>
    /// Whether the desk follows the organization's default workflow rather than one chosen for it, so a change
    /// to the default reaches it.
    /// </summary>
    public bool FollowsOrganizationDefault { get; init; }

    /// <summary>Whether any request is on the desk, which stops its workflow changing.</summary>
    public bool HasRequests { get; init; }

    /// <summary>
    /// Whether the reader may choose the desk's workflow: a Desk Admin on it or the Demand Management Admin,
    /// while no request is on it.
    /// </summary>
    public bool CanChange { get; init; }

    /// <summary>
    /// The workflow's statuses in its own order: the stages a request on the desk moves through, the columns of
    /// the Requests board, so a stage with no request in it still shows, and the path a requester is shown
    /// before submitting.
    /// </summary>
    public List<IntakeWorkflowStatusDto> Statuses { get; init; } = [];
}

/// <summary>One status of a desk's review workflow, with what it means in the workflow author's words.</summary>
public sealed record IntakeWorkflowStatusDto
{
    /// <summary>The status's id, which a request's current status is matched against.</summary>
    public Guid Id { get; init; }

    /// <summary>What the workflow calls the status.</summary>
    public required string Name { get; init; }

    /// <summary>The status's bucket — Proposed, Active, Done or Removed — which decides whether a request in it is open.</summary>
    public StatusCategory Category { get; init; }

    /// <summary>What intake requests call the category. For display only; group and compare on the category.</summary>
    public required string CategoryLabel { get; init; }

    /// <summary>The well-known meaning the status carries, as an intake request status alias value, or 0 for none.</summary>
    public int Alias { get; init; }

    /// <summary>What the status means, when the workflow's author said, written for anyone following a request.</summary>
    public string? Description { get; init; }
}

/// <summary>A published intake request workflow a desk can follow.</summary>
public sealed record IntakeWorkflowChoiceDto
{
    /// <summary>The workflow's id.</summary>
    public Guid Id { get; init; }

    /// <summary>The workflow's key, which its page in Settings is addressed by.</summary>
    public int Key { get; init; }

    /// <summary>The workflow's name.</summary>
    public required string Name { get; init; }

    /// <summary>What the workflow is for, when its author said.</summary>
    public string? Description { get; init; }
}
