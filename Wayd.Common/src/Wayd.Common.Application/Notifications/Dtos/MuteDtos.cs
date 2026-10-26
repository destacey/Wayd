using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Notifications;

namespace Wayd.Common.Application.Notifications.Dtos;

/// <summary>Whether the caller has muted a record.</summary>
public sealed record RecordMuteStatusDto
{
    /// <summary>Whether the caller has muted the record.</summary>
    public bool IsMuted { get; init; }

    /// <summary>When the caller muted the record; null when it is not muted.</summary>
    public Instant? MutedOn { get; init; }
}

/// <summary>A record the caller has muted, as the list of their mutes shows it.</summary>
public sealed record MutedRecordDto
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

    /// <summary>Whether the caller also follows the record. Muting a record never stops following it.</summary>
    public required bool IsFollowing { get; init; }

    /// <summary>When the caller muted the record.</summary>
    public required Instant MutedOn { get; init; }

    /// <summary>A muted record as its owner type describes it, with the mute's own facts.</summary>
    internal static MutedRecordDto Create(string ownerType, Guid id, bool isFollowing, Instant mutedOn, RecordSummary summary) => new()
    {
        OwnerType = ownerType,
        Id = id,
        Key = summary.Key,
        Name = summary.Name,
        ParentKey = summary.ParentKey,
        ParentName = summary.ParentName,
        StatusName = summary.StatusName,
        Lifecycle = summary.Lifecycle,
        IsFollowing = isFollowing,
        MutedOn = mutedOn,
    };
}

/// <summary>One reason the caller can be told for, and whether they have muted it.</summary>
public sealed record ReasonMuteDto
{
    /// <summary>The reason.</summary>
    public required NotificationReason Reason { get; init; }

    /// <summary>Whether the caller has muted it, everywhere.</summary>
    public required bool IsMuted { get; init; }

    /// <summary>When the caller muted it; null when it is not muted.</summary>
    public Instant? MutedOn { get; init; }
}
