using Wayd.Common.Domain.Following;

namespace Wayd.Common.Application.Following.Commands;

/// <summary>
/// Stops the caller following a record. Succeeds whether or not they followed it.
/// </summary>
/// <remarks>
/// Does not ask whether the caller can still read the record: removing your own follow reveals nothing,
/// and someone who has lost access must still be able to stop following.
/// </remarks>
public sealed record UnfollowRecordCommand(string OwnerType, Guid OwnerId) : ICommand, IRequireLinkedEmployee;

/// <summary>Rejects a missing owner type or id before the handler resolves either.</summary>
public sealed class UnfollowRecordCommandValidator : CustomValidator<UnfollowRecordCommand>
{
    public UnfollowRecordCommandValidator()
    {
        RuleFor(c => c.OwnerType).NotEmpty().MaximumLength(FollowOwnerDescriptor.KeyMaxLength);
        RuleFor(c => c.OwnerId).NotEmpty();
    }
}

/// <summary>Removes the caller's follow of the record, if there is one.</summary>
public sealed class UnfollowRecordCommandHandler(
    IFollowStore followStore,
    ICurrentPrincipal currentPrincipal)
    : ICommandHandler<UnfollowRecordCommand>
{
    private readonly IFollowStore _followStore = followStore;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<Result> Handle(UnfollowRecordCommand request, CancellationToken cancellationToken)
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

        await _followStore.Remove(descriptor.Value.Key, request.OwnerId, employeeId.Value, cancellationToken);

        return Result.Success();
    }
}
