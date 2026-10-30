using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Discussions;

namespace Wayd.Common.Application.Discussions.Commands;

/// <summary>
/// Deletes a message, leaving a tombstone in its place: the author may delete their own, a moderator anyone's.
/// </summary>
/// <param name="ChannelType">The record's registered channel type.</param>
/// <param name="OwnerId">The record's id.</param>
/// <param name="MessageId">The message to delete.</param>
public sealed record DeleteDiscussionMessageCommand(string ChannelType, Guid OwnerId, Guid MessageId)
    : ICommand, IRequireLinkedEmployee;

/// <summary>Rejects a missing record or message before anything is resolved.</summary>
public sealed class DeleteDiscussionMessageCommandValidator : CustomValidator<DeleteDiscussionMessageCommand>
{
    public DeleteDiscussionMessageCommandValidator()
    {
        RuleFor(c => c.ChannelType).NotEmpty().MaximumLength(DiscussionChannelTypeDescriptor.KeyMaxLength);
        RuleFor(c => c.OwnerId).NotEmpty();
        RuleFor(c => c.MessageId).NotEmpty();
    }
}

/// <summary>Deletes through the writer and saves.</summary>
public sealed class DeleteDiscussionMessageCommandHandler(IDiscussionWriter writer, IDiscussionsDbContext dbContext)
    : ICommandHandler<DeleteDiscussionMessageCommand>
{
    private readonly IDiscussionWriter _writer = writer;
    private readonly IDiscussionsDbContext _dbContext = dbContext;

    public async Task<Result> Handle(DeleteDiscussionMessageCommand command, CancellationToken cancellationToken)
    {
        var deleted = await _writer.Delete(command.ChannelType, command.OwnerId, command.MessageId, cancellationToken);
        if (deleted.IsFailure)
        {
            return deleted;
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Edited in the same moment; deleting from the stale version could leave the edit's revision behind.
            return Result.Failure(DiscussionAccessErrors.Conflict);
        }

        return deleted;
    }
}
