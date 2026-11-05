using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Discussions;

namespace Wayd.Common.Application.Discussions.Commands;

/// <summary>
/// Edits the caller's own message, keeping the version it replaces. Nothing is recorded when nothing changed.
/// </summary>
/// <param name="ChannelType">The record's registered channel type.</param>
/// <param name="OwnerId">The record's id.</param>
/// <param name="MessageId">The message to edit.</param>
/// <param name="Subject">The new subject; blank means none. Must be blank on a reply in a thread.</param>
/// <param name="Body">The new text as markdown, mentions written <c>@[Name](employee:{id})</c>.</param>
public sealed record EditDiscussionMessageCommand(string ChannelType, Guid OwnerId, Guid MessageId, string? Subject, string Body)
    : ICommand, IRequireLinkedEmployee;

/// <summary>Rejects a missing record or message and over-long text before anything is resolved.</summary>
public sealed class EditDiscussionMessageCommandValidator : CustomValidator<EditDiscussionMessageCommand>
{
    public EditDiscussionMessageCommandValidator()
    {
        RuleFor(c => c.ChannelType).NotEmpty().MaximumLength(DiscussionChannelTypeDescriptor.KeyMaxLength);
        RuleFor(c => c.OwnerId).NotEmpty();
        RuleFor(c => c.MessageId).NotEmpty();
        RuleFor(c => c.Subject).MaximumLength(DiscussionMessage.SubjectMaxLength);
        RuleFor(c => c.Body).NotEmpty().MaximumLength(DiscussionMessage.BodyMaxLength);
    }
}

/// <summary>Edits through the writer and saves.</summary>
public sealed class EditDiscussionMessageCommandHandler(IDiscussionWriter writer, IDiscussionsDbContext dbContext)
    : ICommandHandler<EditDiscussionMessageCommand>
{
    private readonly IDiscussionWriter _writer = writer;
    private readonly IDiscussionsDbContext _dbContext = dbContext;

    public async Task<Result> Handle(EditDiscussionMessageCommand command, CancellationToken cancellationToken)
    {
        var edited = await _writer.Edit(command.ChannelType, command.OwnerId, command.MessageId, command.Subject, command.Body, cancellationToken);
        if (edited.IsFailure)
        {
            return edited;
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Deleted or edited in the same moment, which saving anyway could put text back on a tombstone; or what
            // the edit changes on the record, such as a participant its new mention adds, collided with another save.
            return Result.Failure(DiscussionAccessErrors.Conflict);
        }

        return edited;
    }
}
