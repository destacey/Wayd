using Wayd.Common.Application.Discussions.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Discussions;

namespace Wayd.Common.Application.Discussions;

/// <summary>
/// A record's conversation as one caller may read it: the channel type that answered, what it allowed, and the
/// channel when one has been opened.
/// </summary>
/// <param name="Type">The record's channel type.</param>
/// <param name="Access">What the caller may do.</param>
/// <param name="Channel">The conversation, untracked, or null when nobody has posted on the record yet.</param>
/// <param name="EmployeeId">The caller's employee, or null when they are linked to none.</param>
internal sealed record DiscussionView(IDiscussionChannelType Type, DiscussionAccess Access, DiscussionChannel? Channel, Guid? EmployeeId);

/// <summary>
/// What every discussion query shares: opening a record's conversation for the caller, and describing messages
/// as they may see them.
/// </summary>
internal static class DiscussionReading
{
    /// <summary>
    /// Opens a record's conversation for the caller. Null when they cannot read the record, which the query reports
    /// as not found; a failure only for a channel type nothing answers for.
    /// </summary>
    public static async Task<Result<DiscussionView?>> Open(
        IEnumerable<IDiscussionChannelType> channelTypes,
        IDiscussionsDbContext dbContext,
        ICurrentPrincipal currentPrincipal,
        string channelType,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var type = DiscussionChannelTypeResolver.Resolve(channelTypes, channelType);
        if (type.IsFailure)
        {
            return Result.Failure<DiscussionView?>(type.Error);
        }

        var access = await type.Value.Resolve(ownerId, cancellationToken);
        if (access is null)
        {
            return Result.Success<DiscussionView?>(null);
        }

        var key = type.Value.ChannelType;
        var channel = await dbContext.DiscussionChannels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ChannelType == key && c.OwnerId == ownerId, cancellationToken);

        var employeeId = await currentPrincipal.GetEmployeeId(cancellationToken);

        return new DiscussionView(type.Value, access, channel, employeeId);
    }

    /// <summary>
    /// Describes messages the caller may read, in the order given: names by current name, thread counts for
    /// top-level messages, and what the caller may do to each.
    /// </summary>
    /// <param name="messages">Messages already filtered to kinds the caller may read, with their mentions loaded.</param>
    public static async Task<List<DiscussionMessageDto>> Describe(
        IDiscussionsDbContext dbContext,
        IReadOnlyList<DiscussionMessage> messages,
        DiscussionView view,
        CancellationToken cancellationToken)
    {
        if (messages.Count == 0)
        {
            return [];
        }

        Guid[] topLevelIds = [.. messages.Where(m => m.IsTopLevel).Select(m => m.Id)];

        // A thread is wholly its parent's kind, so a parent the caller may read has replies they may read.
        var replies = topLevelIds.Length == 0
            ? []
            : await dbContext.DiscussionMessages
                .Where(m => m.ParentMessageId != null && topLevelIds.Contains(m.ParentMessageId.Value))
                .Select(m => new { ParentId = m.ParentMessageId!.Value, m.AuthorEmployeeId, m.PostedOn })
                .ToListAsync(cancellationToken);

        var threads = replies
            .GroupBy(r => r.ParentId)
            .ToDictionary(g => g.Key, g => new
            {
                Count = g.Count(),
                LastReplyOn = g.Max(r => r.PostedOn),
                Repliers = g.OrderByDescending(r => r.PostedOn).Select(r => r.AuthorEmployeeId).Distinct().Take(3).ToArray(),
            });

        var employeeIds = messages.Select(m => m.AuthorEmployeeId)
            .Concat(messages.Where(m => m.DeletedById is not null).Select(m => m.DeletedById!.Value))
            .Concat(messages.SelectMany(m => m.Mentions).Select(m => m.EmployeeId))
            .Concat(threads.Values.SelectMany(t => t.Repliers))
            .Distinct()
            .ToArray();

        var employees = (await dbContext.Employees
                .AsNoTracking()
                .Where(e => employeeIds.Contains(e.Id))
                .ToListAsync(cancellationToken))
            .ToDictionary(e => e.Id, EmployeeNavigationDto.From);

        EmployeeNavigationDto Name(Guid id) =>
            employees.TryGetValue(id, out var employee) ? employee : new EmployeeNavigationDto { Id = id, Key = 0, Name = "Unknown" };

        var isLocked = view.Channel?.IsLocked ?? false;

        return [.. messages.Select(m =>
        {
            threads.TryGetValue(m.Id, out var thread);
            var isAuthor = view.EmployeeId is { } me && m.AuthorEmployeeId == me;
            var mayWrite = view.Access.CanWrite(m.Kind);

            return new DiscussionMessageDto
            {
                Id = m.Id,
                ParentMessageId = m.ParentMessageId,
                Kind = m.Kind,
                // The domain clears both on delete; never relying on it alone keeps a tombstone's text unreadable.
                Subject = m.IsDeleted ? null : m.Subject,
                Body = m.IsDeleted ? null : m.Body,
                Author = Name(m.AuthorEmployeeId),
                PostedOn = m.PostedOn,
                EditedOn = m.EditedOn,
                IsDeleted = m.IsDeleted,
                DeletedOn = m.DeletedOn,
                DeletedBy = m.DeletedById is { } deletedById ? Name(deletedById) : null,
                Mentions = [.. m.Mentions.Select(x => Name(x.EmployeeId))],
                ReplyCount = thread?.Count ?? 0,
                LastReplyOn = thread?.LastReplyOn,
                RecentRepliers = [.. (thread?.Repliers ?? []).Select(Name)],
                CanEdit = !m.IsDeleted && !isLocked && isAuthor && mayWrite,
                CanDelete = !m.IsDeleted && (view.Access.CanModerate || (isAuthor && mayWrite)),
            };
        })];
    }
}
