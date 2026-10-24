namespace Wayd.Common.Application.Following.Dtos;

/// <summary>
/// Whether the caller follows a record.
/// </summary>
public sealed record FollowStatusDto
{
    /// <summary>Whether the caller follows the record.</summary>
    public bool IsFollowing { get; init; }

    /// <summary>When the caller started following the record; null when they do not follow it.</summary>
    public Instant? FollowedOn { get; init; }
}

/// <summary>
/// The caller's follows of one kind of record, one page at a time.
/// </summary>
public sealed record FollowGroupDto
{
    /// <summary>The owner type of every record in the group, such as <c>ppm.project</c>.</summary>
    public required string OwnerType { get; init; }

    /// <summary>What to call this kind of record, such as "Project".</summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// One page of the followed records the caller can still read, most recently followed first. The
    /// total counts only those records.
    /// </summary>
    public required PagedResponse<FollowedRecordDto> Follows { get; init; }
}

/// <summary>
/// A record the caller follows.
/// </summary>
public sealed record FollowedRecordDto
{
    /// <summary>The record's owner type, such as <c>ppm.project</c>.</summary>
    public required string OwnerType { get; init; }

    /// <summary>The record's id.</summary>
    public required Guid Id { get; init; }

    /// <summary>The record's human-readable key, as its own pages address it.</summary>
    public required string Key { get; init; }

    /// <summary>The record's name or title.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The key of the record this one is addressed under, such as a planning interval objective's
    /// planning interval. Null when the owner type does not nest.
    /// </summary>
    public string? ParentKey { get; init; }

    /// <summary>The name of the parent record. Null when there is none.</summary>
    public string? ParentName { get; init; }

    /// <summary>When the caller started following the record.</summary>
    public required Instant FollowedOn { get; init; }
}
