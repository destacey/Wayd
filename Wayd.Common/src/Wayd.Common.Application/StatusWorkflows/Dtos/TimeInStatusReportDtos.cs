using System.ComponentModel.DataAnnotations;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.StatusWorkflows.Dtos;

/// <summary>
/// How long records of one type spent in each status, over a window.
/// </summary>
/// <remarks>
/// Counts completed stays only: a stay is in the window when the record left the status within it. A
/// stay still open has no final length, and averaging in a partial one would pull every figure down.
/// Stays that began or ended with a workflow reassignment are left out too, because the reassignment,
/// not the status, set their length.
/// </remarks>
public sealed record TimeInStatusReportDto
{
    /// <summary>Start of the window, inclusive.</summary>
    [Required]
    public required Instant From { get; init; }

    /// <summary>End of the window, inclusive.</summary>
    [Required]
    public required Instant To { get; init; }

    /// <summary>
    /// The workflow that governs this record type now, or <c>null</c> when none is assigned.
    /// </summary>
    public NavigationDto? Workflow { get; init; }

    /// <summary>
    /// One row per status: the current workflow's statuses in their workflow order, then any status
    /// records left in the window that the current workflow no longer has.
    /// </summary>
    [Required]
    public required List<TimeInStatusReportRowDto> Statuses { get; init; }
}

/// <summary>
/// The stays in one status that ended within the report's window.
/// </summary>
public sealed record TimeInStatusReportRowDto
{
    /// <summary>The status, by the id frozen on the transitions — stable across renames.</summary>
    [Required]
    public required Guid StatusId { get; init; }

    /// <summary>
    /// The status's current name, or for a status the workflow no longer has, the name it carried on
    /// the latest stay in the window.
    /// </summary>
    [Required]
    public required string Name { get; init; }

    /// <summary>The status's category: its current one, or the one frozen on its latest stay.</summary>
    [Required]
    public required StatusCategory Category { get; init; }

    /// <summary>
    /// Whether the status belongs to the workflow governing this record type now.
    /// </summary>
    /// <remarks>
    /// False for history from before a reassignment to another workflow, or for a status since removed.
    /// </remarks>
    [Required]
    public required bool InCurrentWorkflow { get; init; }

    /// <summary>The number of stays that ended in the window. A record that returned counts each stay.</summary>
    [Required]
    public required int Visits { get; init; }

    /// <summary>The mean length of those stays in whole seconds, or <c>null</c> when there were none.</summary>
    public long? AverageSeconds { get; init; }

    /// <summary>The median length of those stays in whole seconds, or <c>null</c> when there were none.</summary>
    public long? MedianSeconds { get; init; }
}
