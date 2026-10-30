using Wayd.Common.Application.Discussions.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Discussions;

namespace Wayd.Common.Application.Discussions.Queries;

/// <summary>
/// The versions of a message its edits replaced, newest first. Read under the same rule as the message: a revision of
/// an internal note is an internal note. Null when the record, its conversation or the message does not exist, or the
/// caller cannot read them; empty for a message never edited, or deleted.
/// </summary>
/// <param name="ChannelType">The record's registered channel type.</param>
/// <param name="OwnerId">The record's id.</param>
/// <param name="MessageId">The message.</param>
public sealed record GetDiscussionMessageRevisionsQuery(string ChannelType, Guid OwnerId, Guid MessageId)
    : IQuery<Result<List<DiscussionMessageRevisionDto>?>>;

/// <summary>Rejects a missing record or message before anything is resolved.</summary>
public sealed class GetDiscussionMessageRevisionsQueryValidator : CustomValidator<GetDiscussionMessageRevisionsQuery>
{
    public GetDiscussionMessageRevisionsQueryValidator()
    {
        RuleFor(q => q.ChannelType).NotEmpty().MaximumLength(DiscussionChannelTypeDescriptor.KeyMaxLength);
        RuleFor(q => q.OwnerId).NotEmpty();
        RuleFor(q => q.MessageId).NotEmpty();
    }
}

/// <summary>Reads a message's revisions through the record's channel type.</summary>
public sealed class GetDiscussionMessageRevisionsQueryHandler(
    IDiscussionsDbContext dbContext,
    IEnumerable<IDiscussionChannelType> channelTypes,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetDiscussionMessageRevisionsQuery, Result<List<DiscussionMessageRevisionDto>?>>
{
    private readonly IDiscussionsDbContext _dbContext = dbContext;
    private readonly IEnumerable<IDiscussionChannelType> _channelTypes = channelTypes;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<Result<List<DiscussionMessageRevisionDto>?>> Handle(GetDiscussionMessageRevisionsQuery request, CancellationToken cancellationToken)
    {
        var opened = await DiscussionReading.Open(_channelTypes, _dbContext, _currentPrincipal, request.ChannelType, request.OwnerId, cancellationToken);
        if (opened.IsFailure)
        {
            return Result.Failure<List<DiscussionMessageRevisionDto>?>(opened.Error);
        }

        if (opened.Value is not { Channel: { } channel } view)
        {
            return Result.Success<List<DiscussionMessageRevisionDto>?>(null);
        }

        var message = await _dbContext.DiscussionMessages
            .AsNoTracking()
            .Include(m => m.Revisions)
            .FirstOrDefaultAsync(m => m.Id == request.MessageId && m.ChannelId == channel.Id, cancellationToken);

        if (message is null || !view.Access.CanRead(message.Kind))
        {
            return Result.Success<List<DiscussionMessageRevisionDto>?>(null);
        }

        if (message.IsDeleted)
        {
            return new List<DiscussionMessageRevisionDto>();
        }

        return message.Revisions
            .OrderByDescending(r => r.ReplacedOn)
            .ThenByDescending(r => r.Id)
            .Select(r => new DiscussionMessageRevisionDto { Subject = r.Subject, Body = r.Body, ReplacedOn = r.ReplacedOn })
            .ToList();
    }
}
