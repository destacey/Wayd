using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Discussions;

namespace Wayd.Common.Application.Discussions.Commands;

/// <summary>
/// Locks or unlocks a record's conversation. Moderators only. While locked, nothing can be posted, replied or edited.
/// </summary>
/// <param name="ChannelType">The record's registered channel type.</param>
/// <param name="OwnerId">The record's id.</param>
/// <param name="Locked">True to lock the conversation, false to unlock it.</param>
public sealed record SetDiscussionLockCommand(string ChannelType, Guid OwnerId, bool Locked)
    : ICommand, IRequireLinkedEmployee;

/// <summary>Rejects a missing record before anything is resolved.</summary>
public sealed class SetDiscussionLockCommandValidator : CustomValidator<SetDiscussionLockCommand>
{
    public SetDiscussionLockCommandValidator()
    {
        RuleFor(c => c.ChannelType).NotEmpty().MaximumLength(DiscussionChannelTypeDescriptor.KeyMaxLength);
        RuleFor(c => c.OwnerId).NotEmpty();
    }
}

/// <summary>Locks or unlocks through the writer and saves.</summary>
public sealed class SetDiscussionLockCommandHandler(IDiscussionWriter writer, IDiscussionsDbContext dbContext)
    : ICommandHandler<SetDiscussionLockCommand>
{
    private readonly IDiscussionWriter _writer = writer;
    private readonly IDiscussionsDbContext _dbContext = dbContext;

    public async Task<Result> Handle(SetDiscussionLockCommand command, CancellationToken cancellationToken)
    {
        var changed = await _writer.SetLocked(command.ChannelType, command.OwnerId, command.Locked, cancellationToken);
        if (changed.IsFailure)
        {
            return changed;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return changed;
    }
}
