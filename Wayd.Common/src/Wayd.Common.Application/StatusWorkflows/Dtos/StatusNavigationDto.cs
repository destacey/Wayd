using System.ComponentModel.DataAnnotations;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.StatusWorkflows.Dtos;

/// <summary>
/// The status a record currently holds, as a reader needs it.
/// </summary>
/// <remarks>
/// Carries more than a plain <c>NavigationDto</c> because a workflow status is not just a label: the
/// category is what invariants and rollups group on, and the alias is what code keys on when a status
/// has been renamed. Both are frozen onto the record alongside the id, so this reads them straight from
/// the row rather than resolving the workflow.
/// <para>
/// No <c>Key</c>: a status is not addressable on its own, so there is nothing to navigate to. This is a
/// nav DTO in the sense of describing a referenced thing, not of linking to a page.
/// </para>
/// </remarks>
public record StatusNavigationDto
{
    [Required]
    public required Guid Id { get; init; }

    /// <summary>
    /// What the status was called when the record last moved. Frozen, so renaming a status does not
    /// rewrite what past records read as.
    /// </summary>
    [Required]
    public required string Name { get; init; }

    /// <summary>
    /// The status's bucket — Proposed, Active, Done or Removed. What rollups and invariants group on.
    /// </summary>
    [Required]
    public required StatusCategory Category { get; init; }

    /// <summary>
    /// The well-known meaning the status carries, as the consuming module's alias value, or
    /// <c>0</c> when it carries none.
    /// </summary>
    /// <remarks>
    /// An <c>int</c> rather than a module enum: the meanings belong to the consuming module, so a
    /// shared type cannot name them. Callers cast to their own alias enum.
    /// </remarks>
    [Required]
    public required int Alias { get; init; }
}
