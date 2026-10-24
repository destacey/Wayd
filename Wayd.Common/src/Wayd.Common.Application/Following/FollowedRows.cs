using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Activities;
using Wayd.Common.Domain.Following;

namespace Wayd.Common.Application.Following;

/// <summary>
/// One of the caller's follows of a record they can still read, with what a list filters and sorts on.
/// </summary>
/// <remarks>
/// Every column is the same store type whichever owner type the row came from — the follow's own columns,
/// the activity log's timestamp, and plain flags — so the owner types combine with <c>UNION ALL</c>.
/// </remarks>
internal sealed class FollowedRow
{
    public string OwnerType { get; init; } = default!;

    public Guid OwnerId { get; init; }

    public Instant FollowedOn { get; init; }

    /// <summary>The newest activity on the record, or null when it has none.</summary>
    public Instant? LastChangedOn { get; init; }

    public bool IsUnhealthy { get; init; }

    public bool IsClosed { get; init; }
}

/// <summary>
/// Builds the caller's readable follows across owner types as one query.
/// </summary>
internal static class FollowedRows
{
    /// <summary>How far back "changed this week" looks.</summary>
    public static readonly Duration ChangedRecently = Duration.FromDays(7);

    /// <summary>
    /// The caller's follows of records they can still read, across the given owner types, or <c>null</c> when
    /// they can read no record of any of them.
    /// </summary>
    /// <remarks>
    /// Each owner type contributes its follows joined to the records its read rule admits, so a follow of an
    /// unreadable or deleted record never appears — not in a page, not in a count. The types are concatenated,
    /// leaving filtering, counting and paging to the database.
    /// </remarks>
    public static async Task<IQueryable<FollowedRow>?> Build(
        IQueryable<Follow> follows,
        IQueryable<ActivityLogEntry> activity,
        Guid employeeId,
        IEnumerable<(FollowOwnerDescriptor Descriptor, IRecordReadRule ReadRule)> owners,
        string? search,
        CancellationToken cancellationToken)
    {
        IQueryable<FollowedRow>? all = null;

        foreach (var (descriptor, readRule) in owners)
        {
            var readable = await readRule.Readable(search, cancellationToken);
            if (readable is null)
            {
                continue;
            }

            var ownerType = descriptor.Key;
            var rows =
                from f in follows
                where f.EmployeeId == employeeId && f.OwnerType == ownerType
                join r in readable on f.OwnerId equals r.Id
                select new FollowedRow
                {
                    OwnerType = f.OwnerType,
                    OwnerId = f.OwnerId,
                    FollowedOn = f.FollowedOn,
                    LastChangedOn = activity.Where(a => a.AggregateId == f.OwnerId).Max(a => (Instant?)a.Timestamp),
                    IsUnhealthy = r.IsUnhealthy,
                    IsClosed = r.IsClosed,
                };

            all = all is null ? rows : all.Concat(rows);
        }

        return all;
    }

    /// <summary>Narrows the rows to one "needs a look" view, or leaves them all.</summary>
    public static IQueryable<FollowedRow> In(this IQueryable<FollowedRow> rows, FollowView? view, Instant now)
    {
        var since = now - ChangedRecently;

        return view switch
        {
            FollowView.Unhealthy => rows.Where(r => r.IsUnhealthy),
            FollowView.ChangedRecently => rows.Where(r => r.LastChangedOn >= since),
            FollowView.Closed => rows.Where(r => r.IsClosed),
            _ => rows,
        };
    }
}
