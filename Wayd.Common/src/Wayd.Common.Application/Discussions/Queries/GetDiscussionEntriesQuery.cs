using System.ComponentModel.DataAnnotations;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Discussions.Dtos;
using Wayd.Common.Application.Following;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Discussions;

namespace Wayd.Common.Application.Discussions.Queries;

/// <summary>Which end of a conversation a page starts from.</summary>
public enum DiscussionOrder
{
    /// <summary>The first message first, as a conversation is read; the default.</summary>
    [Display(Name = "Oldest First", Order = 1)]
    OldestFirst = 1,

    /// <summary>The latest message first.</summary>
    [Display(Name = "Newest First", Order = 2)]
    NewestFirst = 2,
}

/// <summary>
/// One page of a record's conversation as the caller may see it: its top-level messages and, unless only messages are
/// asked for, the record's activity between them. Null when the record does not exist or the caller cannot read it.
/// </summary>
/// <remarks>
/// Pages by top-level message; a thread's replies are read with <see cref="GetDiscussionRepliesQuery"/>, and what the
/// caller may do with <see cref="GetDiscussionQuery"/>. A record nobody has posted on reads as an empty conversation,
/// with its activity.
/// </remarks>
/// <param name="ChannelType">The record's registered channel type.</param>
/// <param name="OwnerId">The record's id.</param>
/// <param name="Paging">The page to read: a cursor from the previous page, and how many top-level messages.</param>
/// <param name="Order">Which end of the conversation to start from.</param>
/// <param name="MessagesOnly">True to leave the record's activity out.</param>
public sealed record GetDiscussionEntriesQuery(
    string ChannelType,
    Guid OwnerId,
    CursorRequest Paging,
    DiscussionOrder Order = DiscussionOrder.OldestFirst,
    bool MessagesOnly = false)
    : IQuery<Result<CursorPage<DiscussionEntryDto>?>>, ICursorQuery;

/// <summary>Rejects a missing record, an unknown order, and a bad cursor or page size.</summary>
public sealed class GetDiscussionEntriesQueryValidator : CursorQueryValidator<GetDiscussionEntriesQuery, DiscussionCursor>
{
    public GetDiscussionEntriesQueryValidator()
    {
        RuleFor(q => q.ChannelType).NotEmpty().MaximumLength(DiscussionChannelTypeDescriptor.KeyMaxLength);
        RuleFor(q => q.OwnerId).NotEmpty();
        RuleFor(q => q.Order).IsInEnum();
    }
}

/// <summary>Reads a page of the conversation through the record's channel type and read rule.</summary>
public sealed class GetDiscussionEntriesQueryHandler(
    IDiscussionsDbContext dbContext,
    IEnumerable<IDiscussionChannelType> channelTypes,
    IEnumerable<IRecordReadRule> readRules,
    IActivityLogReader activityLogReader,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetDiscussionEntriesQuery, Result<CursorPage<DiscussionEntryDto>?>>
{
    private readonly IDiscussionsDbContext _dbContext = dbContext;
    private readonly IEnumerable<IDiscussionChannelType> _channelTypes = channelTypes;
    private readonly IEnumerable<IRecordReadRule> _readRules = readRules;
    private readonly IActivityLogReader _activityLogReader = activityLogReader;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<Result<CursorPage<DiscussionEntryDto>?>> Handle(GetDiscussionEntriesQuery request, CancellationToken cancellationToken)
    {
        var opened = await DiscussionReading.Open(_channelTypes, _dbContext, _currentPrincipal, request.ChannelType, request.OwnerId, cancellationToken);
        if (opened.IsFailure || opened.Value is null)
        {
            return opened.IsFailure
                ? Result.Failure<CursorPage<DiscussionEntryDto>?>(opened.Error)
                : Result.Success<CursorPage<DiscussionEntryDto>?>(null);
        }

        var view = opened.Value;
        var oldestFirst = request.Order == DiscussionOrder.OldestFirst;
        DiscussionCursor? cursor = DiscussionCursor.TryDecode(request.Paging.Before, out var decoded) ? decoded : null;

        var (page, hasMore) = await ReadMessages(view, request.Paging.PageSize, oldestFirst, cursor, cancellationToken);
        var described = await DiscussionReading.Describe(_dbContext, page, view, cancellationToken);

        var entries = described
            .Select(m => new DiscussionEntryDto { Type = DiscussionEntryType.Message, Timestamp = m.PostedOn, Message = m })
            .ToList();

        var readRule = _readRules.FirstOrDefault(r => string.Equals(r.OwnerType, view.Type.ChannelType, StringComparison.OrdinalIgnoreCase));

        // ReadActivity trusts its caller to have admitted the record through Readable. A channel type answers who may
        // discuss the record, which need not be who may read its history, so the record's own rule is asked too.
        if (!request.MessagesOnly && readRule is not null
            && await FollowReadRules.CanRead(readRule, request.OwnerId, cancellationToken))
        {
            Instant? lastPosted = page.Count > 0 ? page[^1].PostedOn : null;
            Instant? edge = hasMore ? lastPosted : null;

            // Oldest first, a page covers (cursor, last message]; newest first, [last message, cursor). Either way the
            // edge a later page starts from is exclusive there, so no change is shown twice or skipped.
            var activity = oldestFirst
                ? await DiscussionActivityWindow.Read(readRule, _activityLogReader, request.OwnerId,
                    older: cursor?.PostedOn, olderInclusive: false, newer: edge, newerInclusive: true, cancellationToken)
                : await DiscussionActivityWindow.Read(readRule, _activityLogReader, request.OwnerId,
                    older: edge, olderInclusive: true, newer: cursor?.PostedOn, newerInclusive: false, cancellationToken);

            entries.AddRange(activity.Select(a => new DiscussionEntryDto { Type = DiscussionEntryType.Activity, Timestamp = a.Timestamp, Activity = a }));

            // At one instant a message reads before the change it caused, as in "asked, and moved to Waiting".
            entries = oldestFirst
                ? [.. entries.OrderBy(e => e.Timestamp).ThenBy(e => e.Type)]
                : [.. entries.OrderByDescending(e => e.Timestamp).ThenByDescending(e => e.Type)];
        }

        var last = page.Count > 0 ? page[^1] : null;
        return new CursorPage<DiscussionEntryDto>
        {
            Items = entries,
            NextCursor = hasMore && last is not null ? new DiscussionCursor(last.PostedOn, last.Id).Encode() : null,
        };
    }

    private async Task<(List<DiscussionMessage> Page, bool HasMore)> ReadMessages(
        DiscussionView view, int pageSize, bool oldestFirst, DiscussionCursor? cursor, CancellationToken cancellationToken)
    {
        if (view.Channel is null)
        {
            return ([], false);
        }

        var channelId = view.Channel.Id;
        var kinds = view.Access.ReadableKinds();

        var query = _dbContext.DiscussionMessages
            .AsNoTracking()
            .Include(m => m.Mentions)
            .Where(m => m.ChannelId == channelId && m.ParentMessageId == null && kinds.Contains(m.Kind));

        if (cursor is { } c)
        {
            query = oldestFirst
                ? query.Where(m => m.PostedOn > c.PostedOn || (m.PostedOn == c.PostedOn && m.Id.CompareTo(c.MessageId) > 0))
                : query.Where(m => m.PostedOn < c.PostedOn || (m.PostedOn == c.PostedOn && m.Id.CompareTo(c.MessageId) < 0));
        }

        query = oldestFirst
            ? query.OrderBy(m => m.PostedOn).ThenBy(m => m.Id)
            : query.OrderByDescending(m => m.PostedOn).ThenByDescending(m => m.Id);

        // One more than asked for says whether another page follows, without counting the conversation.
        var page = await query.Take(pageSize + 1).ToListAsync(cancellationToken);
        var hasMore = page.Count > pageSize;
        if (hasMore)
        {
            page.RemoveAt(page.Count - 1);
        }

        return (page, hasMore);
    }
}
