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
