using Wayd.Common.Application.Discussions.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Discussions;

namespace Wayd.Common.Application.Discussions.Queries;

/// <summary>
/// One message in a record's conversation, a top-level message or a reply, as the caller may see it. Null when the
/// record, its conversation or the message does not exist, or the caller cannot read them.
/// </summary>
/// <remarks>
/// What lets a link open a message wherever it sits in the conversation: a reply carries the top-level message its
/// thread hangs from, which is read the same way.
/// </remarks>
/// <param name="ChannelType">The record's registered channel type.</param>
/// <param name="OwnerId">The record's id.</param>
/// <param name="MessageId">The message.</param>
public sealed record GetDiscussionMessageQuery(string ChannelType, Guid OwnerId, Guid MessageId)
    : IQuery<Result<DiscussionMessageDto?>>;

/// <summary>Rejects a missing record or message before anything is resolved.</summary>
public sealed class GetDiscussionMessageQueryValidator : CustomValidator<GetDiscussionMessageQuery>
{
    public GetDiscussionMessageQueryValidator()
    {
        RuleFor(q => q.ChannelType).NotEmpty().MaximumLength(DiscussionChannelTypeDescriptor.KeyMaxLength);
        RuleFor(q => q.OwnerId).NotEmpty();
        RuleFor(q => q.MessageId).NotEmpty();
    }
}

/// <summary>Reads one message through the record's channel type.</summary>
public sealed class GetDiscussionMessageQueryHandler(
    IDiscussionsDbContext dbContext,
    IEnumerable<IDiscussionChannelType> channelTypes,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetDiscussionMessageQuery, Result<DiscussionMessageDto?>>
{
    private readonly IDiscussionsDbContext _dbContext = dbContext;
    private readonly IEnumerable<IDiscussionChannelType> _channelTypes = channelTypes;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<Result<DiscussionMessageDto?>> Handle(GetDiscussionMessageQuery request, CancellationToken cancellationToken)
    {
        var opened = await DiscussionReading.Open(_channelTypes, _dbContext, _currentPrincipal, request.ChannelType, request.OwnerId, cancellationToken);
        if (opened.IsFailure)
        {
            return Result.Failure<DiscussionMessageDto?>(opened.Error);
        }

        if (opened.Value is not { Channel: { } channel } view)
        {
            return Result.Success<DiscussionMessageDto?>(null);
        }

        var message = await _dbContext.DiscussionMessages
            .AsNoTracking()
            .Include(m => m.Mentions)
            .FirstOrDefaultAsync(m => m.Id == request.MessageId && m.ChannelId == channel.Id, cancellationToken);

        // A reply takes its thread's kind, so its own kind answers for the thread too.
        if (message is null || !view.Access.CanRead(message.Kind))
        {
            return Result.Success<DiscussionMessageDto?>(null);
        }

        var described = await DiscussionReading.Describe(_dbContext, [message], view, cancellationToken);
        return Result.Success<DiscussionMessageDto?>(described[0]);
    }
}
