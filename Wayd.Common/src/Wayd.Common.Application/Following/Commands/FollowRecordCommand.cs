using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Following;

namespace Wayd.Common.Application.Following.Commands;

/// <summary>
/// Follows a record for the caller. There is no way to follow on someone else's behalf.
/// </summary>
/// <remarks>
/// Fails only for an owner type that cannot be followed. A record the caller cannot read — or that does
/// not exist — is <see cref="FollowRecordOutcome.NotFound"/>, never a follow: following grants nothing,
/// so it can only attach to what the caller can already see.
/// </remarks>
public sealed record FollowRecordCommand(string OwnerType, Guid OwnerId) : ICommand<FollowRecordOutcome>, IRequireLinkedEmployee;

/// <summary>What following a record did.</summary>
public enum FollowRecordOutcome
{
    /// <summary>The caller now follows the record.</summary>
    Followed,

    /// <summary>The caller already followed the record; nothing changed.</summary>
    AlreadyFollowing,

    /// <summary>The record does not exist or the caller cannot read it; nothing changed.</summary>
    NotFound,
}

/// <summary>Rejects a missing owner type or id before the handler resolves either.</summary>
public sealed class FollowRecordCommandValidator : CustomValidator<FollowRecordCommand>
{
    public FollowRecordCommandValidator()
    {
        RuleFor(c => c.OwnerType).NotEmpty().MaximumLength(FollowOwnerDescriptor.KeyMaxLength);
        RuleFor(c => c.OwnerId).NotEmpty();
    }
}

/// <summary>Follows the record for the caller once its owner type's read rule admits it.</summary>
public sealed class FollowRecordCommandHandler(
    IEnumerable<IRecordReadRule> readRules,
    IFollowStore followStore,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<FollowRecordCommand, FollowRecordOutcome>
{
    private readonly IEnumerable<IRecordReadRule> _readRules = readRules;
    private readonly IFollowStore _followStore = followStore;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<FollowRecordOutcome>> Handle(FollowRecordCommand request, CancellationToken cancellationToken)
    {
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
        {
            LinkedEmployeeRequired.Throw();
        }

        var resolved = FollowReadRules.Resolve(_readRules, request.OwnerType);
        if (resolved.IsFailure)
        {
            return Result.Failure<FollowRecordOutcome>(resolved.Error);
        }

        var (descriptor, readRule) = resolved.Value;

        if (!await FollowReadRules.CanRead(readRule, request.OwnerId, cancellationToken))
        {
            return FollowRecordOutcome.NotFound;
        }

        var follow = Follow.Create(descriptor.Key, request.OwnerId, employeeId.Value, _dateTimeProvider.Now);
        if (follow.IsFailure)
        {
            return Result.Failure<FollowRecordOutcome>(follow.Error);
        }

        return await _followStore.Add(follow.Value, cancellationToken)
            ? FollowRecordOutcome.Followed
            : FollowRecordOutcome.AlreadyFollowing;
    }
}
