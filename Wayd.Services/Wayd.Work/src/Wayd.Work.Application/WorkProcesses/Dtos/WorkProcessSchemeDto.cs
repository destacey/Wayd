using Wayd.Common.Application.Dtos;
using Wayd.Work.Application.WorkTypes.Dtos;

namespace Wayd.Work.Application.WorkProcesses.Dtos;

/// <summary>One work type in a work process, and the status workflow its work items follow.</summary>
public sealed record WorkProcessSchemeDto
{
    /// <summary>The id of the work type's entry in the process's scheme.</summary>
    public Guid Id { get; init; }

    /// <summary>The work type, with its level.</summary>
    public required WorkTypeDto WorkType { get; init; }

    /// <summary>
    /// The status workflow the work type follows in this process, with its statuses; null only when the workflow
    /// its scheme entry names cannot be read.
    /// </summary>
    public WorkProcessSchemeWorkflowDto? Workflow { get; init; }
}

/// <summary>A status workflow as a work process's scheme reports it.</summary>
public sealed record WorkProcessSchemeWorkflowDto
{
    /// <summary>The status workflow's id.</summary>
    public Guid Id { get; init; }

    /// <summary>The status workflow's key, as the status workflow endpoints take it.</summary>
    public int Key { get; init; }

    /// <summary>The status workflow's name.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The workflow's statuses in display order, retired ones last. A status belongs to this workflow alone;
    /// what workflows share is the category.
    /// </summary>
    public required IReadOnlyList<WorkProcessSchemeStatusDto> Statuses { get; init; }
}

/// <summary>One status of a work process's workflow.</summary>
public sealed record WorkProcessSchemeStatusDto
{
    /// <summary>The status's id, which work items in this workflow hold.</summary>
    public Guid Id { get; init; }

    /// <summary>The status's name.</summary>
    public required string Name { get; init; }

    /// <summary>The status's category: its id, and its name as work items label it.</summary>
    public required SimpleNavigationDto Category { get; init; }

    /// <summary>The status's position in the workflow.</summary>
    public int Order { get; init; }

    /// <summary>
    /// Whether the source has dropped the status. A retired status is given to no work item again, but the items
    /// and history that hold it keep it.
    /// </summary>
    public bool IsRetired { get; init; }
}
