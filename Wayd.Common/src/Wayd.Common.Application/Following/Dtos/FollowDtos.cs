using Mapster;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Enums;

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
/// A record the caller follows, as a list shows it.
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
    /// The key of the record this one is addressed under, such as a planning interval objective's planning
    /// interval. Null when the owner type does not nest.
    /// </summary>
    public string? ParentKey { get; init; }

    /// <summary>The name of the parent record. Null when there is none.</summary>
    public string? ParentName { get; init; }

    /// <summary>The record's status as its own pages name it.</summary>
    public required string StatusName { get; init; }

    /// <summary>Where the status sits in the record's lifecycle.</summary>
    public required LifecycleCategory Lifecycle { get; init; }

    /// <summary>The record's current health, or null when it has none.</summary>
    public HealthStatus? Health { get; init; }

    /// <summary>When the caller started following the record.</summary>
    public required Instant FollowedOn { get; init; }

    /// <summary>When the record last changed, from its activity; null when it has no recorded activity.</summary>
    public Instant? LastChangedOn { get; init; }

    /// <summary>
    /// A followed record as its owner type describes it, with the follow's own facts.
    /// </summary>
    public static FollowedRecordDto Create(
        string ownerType,
        Guid id,
        Instant followedOn,
        Instant? lastChangedOn,
        RecordSummary summary) =>
        summary.Adapt<FollowedRecordDto>() with
        {
            OwnerType = ownerType,
            Id = id,
            FollowedOn = followedOn,
            LastChangedOn = lastChangedOn,
        };
}

/// <summary>
/// One followed record in full: the list's facts plus what its detail shows and what has happened to it since
/// the caller followed it.
/// </summary>
public sealed record FollowedRecordDetailDto
{
    /// <summary>The record as the list shows it.</summary>
    public required FollowedRecordDto Record { get; init; }

    /// <summary>How far the record has got, 0 to 100, for an owner type that tracks it. Null otherwise.</summary>
    public double? Progress { get; init; }

    /// <summary>
    /// Who leads the record and in what role: a project's or program's owners then managers, an objective's
    /// team. Empty when no one does.
    /// </summary>
    public required List<RecordPerson> People { get; init; }

    /// <summary>When the record starts, or null.</summary>
    public LocalDate? Start { get; init; }

    /// <summary>When the record ends or is due, or null.</summary>
    public LocalDate? End { get; init; }

    /// <summary>The record's activity since the caller followed it, newest first, at most <c>ChangeLimit</c> entries.</summary>
    public required List<ActivityLogDto> ChangesSinceFollowed { get; init; }

    /// <summary>Whether more activity since following exists than <see cref="ChangesSinceFollowed"/> holds.</summary>
    public bool HasMoreChanges { get; init; }
}

/// <summary>
/// How many records the caller follows and can still read, by owner type and by "needs a look" view.
/// </summary>
public sealed record FollowCountsDto
{
    /// <summary>Every readable followed record.</summary>
    public required int Total { get; init; }

    /// <summary>One entry per owner type the caller can read, ordered by display name, including types with none followed.</summary>
    public required List<FollowTypeCountDto> Types { get; init; }

    /// <summary>One entry per <see cref="FollowView"/>, across every owner type.</summary>
    public required List<FollowViewCountDto> Views { get; init; }
}

/// <summary>How many records of one owner type the caller follows.</summary>
public sealed record FollowTypeCountDto
{
    /// <summary>The owner type, such as <c>ppm.project</c>.</summary>
    public required string OwnerType { get; init; }

    /// <summary>What to call this kind of record, such as "Project".</summary>
    public required string DisplayName { get; init; }

    /// <summary>How many readable records of this type the caller follows.</summary>
    public required int Count { get; init; }
}

/// <summary>How many followed records fall in one "needs a look" view.</summary>
public sealed record FollowViewCountDto
{
    /// <summary>The view.</summary>
    public required FollowView View { get; init; }

    /// <summary>How many readable followed records it holds.</summary>
    public required int Count { get; init; }
}
