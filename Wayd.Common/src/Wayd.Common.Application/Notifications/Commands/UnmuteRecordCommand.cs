using Wayd.Common.Domain.Following;

namespace Wayd.Common.Application.Notifications.Commands;

/// <summary>
/// Unmutes a record for the caller. Succeeds whether or not they had muted it.
/// </summary>
/// <remarks>
/// Does not ask whether the caller can still read the record: removing your own mute reveals nothing, and
/// someone who has lost access must still be able to clear it. Brings back nothing written while the record
/// was muted; only later changes notify again.
/// </remarks>
public sealed record UnmuteRecordCommand(string OwnerType, Guid OwnerId) : ICommand, IRequireLinkedEmployee;

/// <summary>Rejects a missing owner type or id before the handler resolves either.</summary>
public sealed class UnmuteRecordCommandValidator : CustomValidator<UnmuteRecordCommand>
{
    public UnmuteRecordCommandValidator()
    {
        RuleFor(c => c.OwnerType).NotEmpty().MaximumLength(FollowOwnerDescriptor.KeyMaxLength);
        RuleFor(c => c.OwnerId).NotEmpty();
    }
}

/// <summary>Removes the caller's mute of the record, if there is one.</summary>
public sealed class UnmuteRecordCommandHandler(
    IMuteStore muteStore,
    ICurrentPrincipal currentPrincipal)
    : ICommandHandler<UnmuteRecordCommand>
{
    private readonly IMuteStore _muteStore = muteStore;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<Result> Handle(UnmuteRecordCommand request, CancellationToken cancellationToken)
    {
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
        {
            LinkedEmployeeRequired.Throw();
        }

        var descriptor = FollowOwners.Resolve(request.OwnerType);
        if (descriptor.IsFailure)
        {
            return Result.Failure(descriptor.Error);
        }

        await _muteStore.RemoveRecordMute(descriptor.Value.Key, request.OwnerId, employeeId.Value, cancellationToken);

        return Result.Success();
    }
}
