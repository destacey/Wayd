using Wayd.Common.Application.Discussions.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Discussions;

namespace Wayd.Common.Application.Discussions.Queries;

/// <summary>
/// The replies in the thread a top-level message starts, oldest first. Null when the record, its conversation or the
/// message does not exist, or the caller cannot read them.
/// </summary>
/// <param name="ChannelType">The record's registered channel type.</param>
/// <param name="OwnerId">The record's id.</param>
/// <param name="MessageId">The top-level message that starts the thread.</param>
public sealed record GetDiscussionRepliesQuery(string ChannelType, Guid OwnerId, Guid MessageId)
    : IQuery<Result<List<DiscussionMessageDto>?>>;

/// <summary>Rejects a missing record or message before anything is resolved.</summary>
public sealed class GetDiscussionRepliesQueryValidator : CustomValidator<GetDiscussionRepliesQuery>
{
    public GetDiscussionRepliesQueryValidator()
    {
        RuleFor(q => q.ChannelType).NotEmpty().MaximumLength(DiscussionChannelTypeDescriptor.KeyMaxLength);
        RuleFor(q => q.OwnerId).NotEmpty();
        RuleFor(q => q.MessageId).NotEmpty();
    }
}

/// <summary>Reads a thread through the record's channel type.</summary>
public sealed class GetDiscussionRepliesQueryHandler(
    IDiscussionsDbContext dbContext,
    IEnumerable<IDiscussionChannelType> channelTypes,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetDiscussionRepliesQuery, Result<List<DiscussionMessageDto>?>>
{
    private readonly IDiscussionsDbContext _dbContext = dbContext;
    private readonly IEnumerable<IDiscussionChannelType> _channelTypes = channelTypes;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<Result<List<DiscussionMessageDto>?>> Handle(GetDiscussionRepliesQuery request, CancellationToken cancellationToken)
    {
        var opened = await DiscussionReading.Open(_channelTypes, _dbContext, _currentPrincipal, request.ChannelType, request.OwnerId, cancellationToken);
        if (opened.IsFailure)
        {
            return Result.Failure<List<DiscussionMessageDto>?>(opened.Error);
        }

        if (opened.Value is not { Channel: { } channel } view)
        {
            return Result.Success<List<DiscussionMessageDto>?>(null);
        }

        var parentKind = await _dbContext.DiscussionMessages
            .Where(m => m.Id == request.MessageId && m.ChannelId == channel.Id && m.ParentMessageId == null)
            .Select(m => (DiscussionMessageKind?)m.Kind)
            .FirstOrDefaultAsync(cancellationToken);

        if (parentKind is not { } kind || !view.Access.CanRead(kind))
        {
            return Result.Success<List<DiscussionMessageDto>?>(null);
        }

        var replies = await _dbContext.DiscussionMessages
            .AsNoTracking()
            .Include(m => m.Mentions)
            .Where(m => m.ChannelId == channel.Id && m.ParentMessageId == request.MessageId)
            .OrderBy(m => m.PostedOn)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);

        return await DiscussionReading.Describe(_dbContext, replies, view, cancellationToken);
    }
}
