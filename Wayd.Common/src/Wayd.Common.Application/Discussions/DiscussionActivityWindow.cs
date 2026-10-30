using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Records;

namespace Wayd.Common.Application.Discussions;

/// <summary>
/// The record's activity that falls between the messages of one page of its conversation.
/// </summary>
/// <remarks>
/// Read through the record's own <see cref="IRecordReadRule.ReadActivity"/>, unchanged, so the conversation can never
/// show a change — or who made it — that the record's Activity section withholds from the caller. That read pages by
/// number, newest first, so the window is found by walking pages from the newest until it passes the window's older
/// edge. The walk is capped: past <see cref="MaxEntriesWalked"/> entries the oldest activity is left out of the
/// conversation, though it stays in the record's Activity section.
/// </remarks>
internal static class DiscussionActivityWindow
{
    /// <summary>How many entries one read of the record's activity returns.</summary>
    private const int WalkPageSize = PageSizes.Max;

    /// <summary>The most activity entries one page of a conversation walks through to find its window.</summary>
    internal const int MaxEntriesWalked = WalkPageSize * 4;

    /// <summary>
    /// The activity entries with a timestamp in the window, newest first.
    /// </summary>
    /// <param name="older">The window's older edge, or null for none; included only when <paramref name="olderInclusive"/>.</param>
    /// <param name="newer">The window's newer edge, or null for none; included only when <paramref name="newerInclusive"/>.</param>
    public static async Task<List<ActivityLogDto>> Read(
        IRecordReadRule readRule,
        IActivityLogReader activityLogReader,
        Guid ownerId,
        Instant? older,
        bool olderInclusive,
        Instant? newer,
        bool newerInclusive,
        CancellationToken cancellationToken)
    {
        var inWindow = new List<ActivityLogDto>();

        for (var page = PageRequest.FirstPage; (page - PageRequest.FirstPage) * WalkPageSize < MaxEntriesWalked; page++)
        {
            var read = await readRule.ReadActivity(ownerId, activityLogReader, new PageRequest(page, WalkPageSize), cancellationToken);

            foreach (var entry in read.Items)
            {
                if (newer is { } n && (newerInclusive ? entry.Timestamp > n : entry.Timestamp >= n))
                {
                    continue;
                }

                if (older is { } o && (olderInclusive ? entry.Timestamp < o : entry.Timestamp <= o))
                {
                    return inWindow;
                }

                inWindow.Add(entry);
            }

            if (read.Items.Count < WalkPageSize)
            {
                break;
            }
        }

        return inWindow;
    }
}
