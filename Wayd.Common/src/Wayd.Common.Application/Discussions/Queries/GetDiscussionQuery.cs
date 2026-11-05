using Wayd.Common.Application.Discussions.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Discussions;

namespace Wayd.Common.Application.Discussions.Queries;

/// <summary>
/// A record's conversation as the caller may use it: what they may do in it, whether it is locked, and how much of it they
/// can read. Null when the record does not exist or the caller cannot read it. Its messages are read with <see cref="GetDiscussionEntriesQuery"/>.
/// </summary>
/// <param name="ChannelType">The record's registered channel type.</param>
/// <param name="OwnerId">The record's id.</param>
public sealed record GetDiscussionQuery(string ChannelType, Guid OwnerId) : IQuery<Result<DiscussionDto?>>;

/// <summary>Rejects a missing record before anything is resolved.</summary>
public sealed class GetDiscussionQueryValidator : CustomValidator<GetDiscussionQuery>
{
    public GetDiscussionQueryValidator()
    {
        RuleFor(q => q.ChannelType).NotEmpty().MaximumLength(DiscussionChannelTypeDescriptor.KeyMaxLength);
        RuleFor(q => q.OwnerId).NotEmpty();
    }
}

/// <summary>Answers what the caller may do in the conversation, through the record's channel type.</summary>
public sealed class GetDiscussionQueryHandler(
    IDiscussionsDbContext dbContext,
    IEnumerable<IDiscussionChannelType> channelTypes,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetDiscussionQuery, Result<DiscussionDto?>>
{
    private readonly IDiscussionsDbContext _dbContext = dbContext;
    private readonly IEnumerable<IDiscussionChannelType> _channelTypes = channelTypes;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<Result<DiscussionDto?>> Handle(GetDiscussionQuery request, CancellationToken cancellationToken)
    {
        var opened = await DiscussionReading.Open(_channelTypes, _dbContext, _currentPrincipal, request.ChannelType, request.OwnerId, cancellationToken);
        if (opened.IsFailure)
        {
            return Result.Failure<DiscussionDto?>(opened.Error);
        }

        if (opened.Value is not { } view)
        {
            return Result.Success<DiscussionDto?>(null);
        }

        // Counted over the kinds the caller may read, so the numbers never reveal a hidden note.
        var counts = new { Threads = 0, Messages = 0 };
        if (view.Channel is { } channel)
        {
            var kinds = view.Access.ReadableKinds();
            var standing = _dbContext.DiscussionMessages
                .Where(m => m.ChannelId == channel.Id && m.DeletedOn == null && kinds.Contains(m.Kind));
            counts = new
            {
                Threads = await standing.CountAsync(m => m.ParentMessageId == null, cancellationToken),
                Messages = await standing.CountAsync(cancellationToken),
            };
        }

        return new DiscussionDto
        {
            CanPost = view.Access.CanPost,
            CanPostInternal = view.CanPostInternal,
            CanModerate = view.Access.CanModerate,
            IsLocked = view.Channel?.IsLocked ?? false,
            ThreadCount = counts.Threads,
            MessageCount = counts.Messages,
        };
    }
}
