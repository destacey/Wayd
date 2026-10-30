using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Discussions;

namespace Wayd.Common.Application.Discussions.Commands;

/// <summary>
/// Posts a top-level message on a record for the caller, opening the record's conversation on the first post.
/// Returns the message's id.
/// </summary>
/// <param name="ChannelType">The record's registered channel type, such as <c>demand.intake-request</c>.</param>
/// <param name="OwnerId">The record's id.</param>
/// <param name="Kind">Who the message is for.</param>
/// <param name="Subject">An optional subject; blank means none.</param>
/// <param name="Body">The message as markdown, mentions written <c>@[Name](employee:{id})</c>.</param>
public sealed record PostDiscussionMessageCommand(string ChannelType, Guid OwnerId, DiscussionMessageKind Kind, string? Subject, string Body)
    : ICommand<Guid>, IRequireLinkedEmployee;

/// <summary>Rejects a missing record, an unknown kind and over-long text before anything is resolved.</summary>
public sealed class PostDiscussionMessageCommandValidator : CustomValidator<PostDiscussionMessageCommand>
{
    public PostDiscussionMessageCommandValidator()
    {
        RuleFor(c => c.ChannelType).NotEmpty().MaximumLength(DiscussionChannelTypeDescriptor.KeyMaxLength);
        RuleFor(c => c.OwnerId).NotEmpty();
        RuleFor(c => c.Kind).IsInEnum();
        RuleFor(c => c.Subject).MaximumLength(DiscussionMessage.SubjectMaxLength);
        RuleFor(c => c.Body).NotEmpty().MaximumLength(DiscussionMessage.BodyMaxLength);
    }
}

/// <summary>Posts the message through the writer and saves.</summary>
public sealed class PostDiscussionMessageCommandHandler(IDiscussionWriter writer, IDiscussionsDbContext dbContext)
    : ICommandHandler<PostDiscussionMessageCommand, Guid>
{
    private readonly IDiscussionWriter _writer = writer;
    private readonly IDiscussionsDbContext _dbContext = dbContext;

    public async Task<Result<Guid>> Handle(PostDiscussionMessageCommand command, CancellationToken cancellationToken)
    {
        var posted = await _writer.Post(
            new PostDiscussionMessage(command.ChannelType, command.OwnerId, command.Kind, command.Subject, command.Body),
            cancellationToken);
        if (posted.IsFailure)
        {
            return posted;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return posted;
    }
}
