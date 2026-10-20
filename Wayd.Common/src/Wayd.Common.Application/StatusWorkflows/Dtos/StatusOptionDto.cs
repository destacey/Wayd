using System.ComponentModel.DataAnnotations;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.StatusWorkflows.Dtos;

/// <summary>
/// A status a record can be moved to, with what the move needs.
/// </summary>
public sealed record StatusOptionDto : StatusNavigationDto
{
    /// <summary>
    /// What moving the record to this status needs, in kind order — <c>RequiresReason</c> means the move
    /// must carry a reason. Empty when it needs nothing, and always empty when the options were asked for
    /// without a record, since conditions belong to the move from the record's current status.
    /// </summary>
    [Required]
    public required IReadOnlyCollection<TransitionConditionKind> Conditions { get; init; }
}
