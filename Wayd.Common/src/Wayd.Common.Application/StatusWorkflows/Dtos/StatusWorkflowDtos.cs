using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.StatusWorkflows.Dtos;

/// <summary>A workflow as it appears in the list.</summary>
public sealed record StatusWorkflowListDto
{
    public required Guid Id { get; init; }
    public required int Key { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    /// <summary>The kind of record this workflow governs.</summary>
    public required WorkflowOwnerNavigationDto Owner { get; init; }

    public required string State { get; init; }
    public required bool IsSystem { get; init; }
    public required int StatusCount { get; init; }

    /// <summary>Whether any scope currently uses this workflow. Archiving is refused while it does.</summary>
    public required bool IsAssigned { get; init; }
}

/// <summary>One status within a workflow.</summary>
public sealed record WorkflowStatusDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    /// <summary>The rollup this status belongs to, as id and display name.</summary>
    public required SimpleNavigationDto Category { get; init; }

    /// <summary>The well-known meaning, or zero for none.</summary>
    public required int Alias { get; init; }

    /// <summary>The alias in the module's vocabulary, or null when it carries none.</summary>
    public string? AliasName { get; init; }

    public required int Order { get; init; }
}

/// <summary>A workflow with its statuses and what may currently be done to it.</summary>
public sealed record StatusWorkflowDetailsDto
{
    public required Guid Id { get; init; }
    public required int Key { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required WorkflowOwnerNavigationDto Owner { get; init; }
    public required string State { get; init; }
    public required bool IsSystem { get; init; }
    public required bool IsAssigned { get; init; }
    public required IReadOnlyCollection<WorkflowStatusDto> Statuses { get; init; }

    /// <summary>
    /// The required aliases this workflow does not yet carry, named rather than numbered.
    /// </summary>
    /// <remarks>
    /// Resolved server-side because the numbers mean nothing to a reader: "needs a Released status"
    /// is actionable where "missing alias 3" is not.
    /// </remarks>
    public required IReadOnlyCollection<string> MissingRequiredAliases { get; init; }

    /// <summary>
    /// Whether the statuses may be restructured — Draft, and not platform-seeded.
    /// </summary>
    /// <remarks>
    /// The read side mirrors the domain rule so the UI can disable rather than let a user discover the
    /// refusal by hitting it. The aggregate remains the authority.
    /// </remarks>
    public required bool CanEdit { get; init; }

    public required bool CanPublish { get; init; }
    public required bool CanArchive { get; init; }
}

/// <summary>
/// An owner type, as referenced from a workflow or an assignment.
/// </summary>
/// <remarks>
/// Its own shape rather than <c>NavigationDto</c> because an owner type is keyed by string — a
/// registered descriptor key like <c>product.product</c>, not a database id.
/// </remarks>
public sealed record WorkflowOwnerNavigationDto
{
    public required string Key { get; init; }
    public required string Name { get; init; }
}

/// <summary>An owner type and the vocabulary its workflows are built from.</summary>
public sealed record WorkflowOwnerTypeDto
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }

    /// <summary>Every alias this owner type recognises.</summary>
    public required IReadOnlyCollection<WorkflowAliasDto> Aliases { get; init; }

    /// <summary>The aliases a workflow must carry before it can be published.</summary>
    public required IReadOnlyCollection<int> RequiredAliases { get; init; }
}

public sealed record WorkflowAliasDto
{
    public required int Value { get; init; }
    public required string Name { get; init; }
    public required bool IsRequired { get; init; }
}

/// <summary>Which workflow a scope's records currently use.</summary>
public sealed record WorkflowAssignmentDto
{
    public required Guid Id { get; init; }
    public required WorkflowOwnerNavigationDto Owner { get; init; }

    /// <summary>Null for the organization-wide default, which is the only scope in use today.</summary>
    public Guid? ScopeId { get; init; }

    /// <summary>The workflow this scope's records use.</summary>
    public required NavigationDto Workflow { get; init; }
}
