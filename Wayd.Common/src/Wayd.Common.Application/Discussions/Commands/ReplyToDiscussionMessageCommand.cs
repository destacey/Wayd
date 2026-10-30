using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Discussions;

namespace Wayd.Common.Application.Discussions.Commands;

/// <summary>
/// Replies for the caller in the thread a top-level message starts. The reply takes that message's kind and has no
/// subject. Returns the reply's id.
/// </summary>
/// <param name="ChannelType">The record's registered channel type.</param>
/// <param name="OwnerId">The record's id.</param>
/// <param name="ParentMessageId">The top-level message whose thread to reply in.</param>
/// <param name="Body">The reply as markdown, mentions written <c>@[Name](employee:{id})</c>.</param>
public sealed record ReplyToDiscussionMessageCommand(string ChannelType, Guid OwnerId, Guid ParentMessageId, string Body)
    : ICommand<Guid>, IRequireLinkedEmployee;

/// <summary>Rejects a missing record or message and over-long text before anything is resolved.</summary>
public sealed class ReplyToDiscussionMessageCommandValidator : CustomValidator<ReplyToDiscussionMessageCommand>
{
    public ReplyToDiscussionMessageCommandValidator()
    {
        RuleFor(c => c.ChannelType).NotEmpty().MaximumLength(DiscussionChannelTypeDescriptor.KeyMaxLength);
        RuleFor(c => c.OwnerId).NotEmpty();
        RuleFor(c => c.ParentMessageId).NotEmpty();
        RuleFor(c => c.Body).NotEmpty().MaximumLength(DiscussionMessage.BodyMaxLength);
    }
}

/// <summary>Replies through the writer and saves.</summary>
public sealed class ReplyToDiscussionMessageCommandHandler(IDiscussionWriter writer, IDiscussionsDbContext dbContext)
    : ICommandHandler<ReplyToDiscussionMessageCommand, Guid>
{
    private readonly IDiscussionWriter _writer = writer;
    private readonly IDiscussionsDbContext _dbContext = dbContext;

    public async Task<Result<Guid>> Handle(ReplyToDiscussionMessageCommand command, CancellationToken cancellationToken)
    {
        var replied = await _writer.Reply(command.ChannelType, command.OwnerId, command.ParentMessageId, command.Body, cancellationToken);
        if (replied.IsFailure)
        {
            return replied;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return replied;
    }
}
