using Wayd.Common.Application.Dtos;

namespace Wayd.Common.Application.StatusWorkflows.Dtos;

/// <summary>
/// How each status of the current workflow would land in the target, before anything is committed.
/// </summary>
/// <remarks>
/// Reassignment is the one operation that rewrites every record of an owner type, so it is built to be
/// reviewed rather than confirmed. This is the review.
/// </remarks>
public sealed record StatusRemapPreviewDto
{
    /// <summary>The workflow the scope is on now.</summary>
    public required NavigationDto From { get; init; }

    /// <summary>The workflow it would move to.</summary>
    public required NavigationDto To { get; init; }

    /// <summary>One entry per status of the current workflow, in display order.</summary>
    public required IReadOnlyCollection<StatusRemapEntryDto> Entries { get; init; }

    /// <summary>Whether every status already has a target. The domain refuses an incomplete remap.</summary>
    public required bool IsComplete { get; init; }

    /// <summary>How many records the reassignment would move.</summary>
    public required int AffectedRecordCount { get; init; }
}

/// <summary>Where one status would send its records.</summary>
public sealed record StatusRemapEntryDto
{
    /// <summary>The status records currently hold.</summary>
    public required WorkflowStatusDto From { get; init; }

    /// <summary>
    /// The status they would move to, or null when nothing could be chosen for them.
    /// </summary>
    /// <remarks>
    /// A null here is what the operator has to resolve — the domain refuses an incomplete remap.
    /// </remarks>
    public WorkflowStatusDto? To { get; init; }

    /// <summary>
    /// Why this row was matched — Alias, Name, Category, or Unresolved.
    /// </summary>
    /// <remarks>
    /// The trust signal. An alias match is unambiguous; a category match is a lone-candidate guess and
    /// deserves a second look. Without this the operator cannot tell which rows to check.
    /// </remarks>
    public required string MatchedBy { get; init; }

    /// <summary>How many records currently hold this status.</summary>
    public required int RecordCount { get; init; }
}

/// <summary>One operator decision: send this source status's records to that target status.</summary>
public sealed record StatusRemapDecisionDto
{
    public required Guid FromStatusId { get; init; }
    public required Guid ToStatusId { get; init; }
}
