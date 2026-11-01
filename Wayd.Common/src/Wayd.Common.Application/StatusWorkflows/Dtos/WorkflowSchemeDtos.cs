namespace Wayd.Common.Application.StatusWorkflows.Dtos;

/// <summary>A record type: a variant of an owner type's records that can follow its own workflow.</summary>
public sealed record RecordTypeDto
{
    public required Guid Id { get; init; }
    public required int Key { get; init; }
    public required string Name { get; init; }

    /// <summary>The kind of record this is a type of.</summary>
    public required WorkflowOwnerNavigationDto Owner { get; init; }

    /// <summary><c>Owned</c> when administrators keep it; <c>Managed</c> when a sync keeps it from its source.</summary>
    public required string Ownership { get; init; }

    /// <summary>The source's identifier for a managed type; null for an owned one.</summary>
    public string? ExternalKey { get; init; }

    /// <summary>Whether records can still be given this type.</summary>
    public required bool IsActive { get; init; }
}

/// <summary>A workflow scheme, as listed.</summary>
public sealed record WorkflowSchemeListDto
{
    public required Guid Id { get; init; }
    public required int Key { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required WorkflowOwnerNavigationDto Owner { get; init; }

    /// <summary><c>Owned</c> or <c>Managed</c>.</summary>
    public required string Ownership { get; init; }

    /// <summary>The workflow records follow when their type has no entry.</summary>
    public required WorkflowSchemeWorkflowDto DefaultWorkflow { get; init; }

    /// <summary>How many record types the scheme sends somewhere other than its default.</summary>
    public required int EntryCount { get; init; }

    /// <summary>How many scopes point at the scheme. A scheme with any cannot be deleted.</summary>
    public required int AssignedScopeCount { get; init; }
}

/// <summary>A workflow scheme with its entries.</summary>
public sealed record WorkflowSchemeDetailsDto
{
    public required Guid Id { get; init; }
    public required int Key { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required WorkflowOwnerNavigationDto Owner { get; init; }

    /// <summary><c>Owned</c> or <c>Managed</c>.</summary>
    public required string Ownership { get; init; }

    /// <summary>The source's identifier for a managed scheme; null for an owned one.</summary>
    public string? ExternalKey { get; init; }

    /// <summary>The workflow records follow when their type has no entry.</summary>
    public required WorkflowSchemeWorkflowDto DefaultWorkflow { get; init; }

    /// <summary>The record types sent somewhere other than the default, by record type name.</summary>
    public required IReadOnlyCollection<WorkflowSchemeEntryDto> Entries { get; init; }

    /// <summary>The scopes pointing at the scheme; <c>null</c> is the organization-level assignment.</summary>
    public required IReadOnlyCollection<Guid?> AssignedScopeIds { get; init; }
}

/// <summary>One record type's workflow under a scheme.</summary>
public sealed record WorkflowSchemeEntryDto
{
    public required RecordTypeNavigationDto RecordType { get; init; }
    public required WorkflowSchemeWorkflowDto Workflow { get; init; }
}

/// <summary>A record type, named enough to show.</summary>
public sealed record RecordTypeNavigationDto
{
    public required Guid Id { get; init; }
    public required int Key { get; init; }
    public required string Name { get; init; }
}

/// <summary>A workflow a scheme names, named enough to show.</summary>
public sealed record WorkflowSchemeWorkflowDto
{
    public required Guid Id { get; init; }
    public required int Key { get; init; }
    public required string Name { get; init; }
}
