using Wayd.Common.Application.Following;
using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Following;
using Wayd.Common.Domain.Notifications;

namespace Wayd.Common.Application.Notifications.Commands;

/// <summary>
/// Mutes a record for the caller: no further notifications about it, whatever the reason. There is no way to
/// mute on someone else's behalf.
/// </summary>
/// <remarks>
/// A record the caller cannot read — or that does not exist — is <see cref="MuteRecordOutcome.NotFound"/>,
/// so a mute reveals nothing. Muting changes neither the caller's access nor whether they follow or take part
/// in the record. It also marks the caller's existing notifications about the record read, so the record
/// leaves their unread count as well as going quiet.
/// </remarks>
public sealed record MuteRecordCommand(string OwnerType, Guid OwnerId) : ICommand<MuteRecordOutcome>, IRequireLinkedEmployee;

/// <summary>What muting a record did.</summary>
public enum MuteRecordOutcome
{
    /// <summary>The record is now muted.</summary>
    Muted,

    /// <summary>The record was already muted; it stays muted.</summary>
    AlreadyMuted,

    /// <summary>The record does not exist or the caller cannot read it; nothing changed.</summary>
    NotFound,
}

/// <summary>Rejects a missing owner type or id before the handler resolves either.</summary>
public sealed class MuteRecordCommandValidator : CustomValidator<MuteRecordCommand>
{
    public MuteRecordCommandValidator()
    {
        RuleFor(c => c.OwnerType).NotEmpty().MaximumLength(FollowOwnerDescriptor.KeyMaxLength);
        RuleFor(c => c.OwnerId).NotEmpty();
    }
}

/// <summary>Mutes the record for the caller once its owner type's read rule admits it.</summary>
public sealed class MuteRecordCommandHandler(
    IEnumerable<IRecordReadRule> readRules,
    IMuteStore muteStore,
    INotificationStore notificationStore,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<MuteRecordCommand, MuteRecordOutcome>
{
    private readonly IEnumerable<IRecordReadRule> _readRules = readRules;
    private readonly IMuteStore _muteStore = muteStore;
    private readonly INotificationStore _notificationStore = notificationStore;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<MuteRecordOutcome>> Handle(MuteRecordCommand request, CancellationToken cancellationToken)
    {
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
        {
            LinkedEmployeeRequired.Throw();
        }

        var resolved = FollowReadRules.Resolve(_readRules, request.OwnerType);
        if (resolved.IsFailure)
        {
            return Result.Failure<MuteRecordOutcome>(resolved.Error);
        }

        var (descriptor, readRule) = resolved.Value;

        if (!await FollowReadRules.CanRead(readRule, request.OwnerId, cancellationToken))
        {
            return MuteRecordOutcome.NotFound;
        }

        var now = _dateTimeProvider.Now;
        var mute = RecordMute.Create(descriptor.Key, request.OwnerId, employeeId.Value, now);
        if (mute.IsFailure)
        {
            return Result.Failure<MuteRecordOutcome>(mute.Error);
        }

        var added = await _muteStore.Add(mute.Value, cancellationToken);
        await _notificationStore.MarkRecordRead(employeeId.Value, descriptor.Key, request.OwnerId, now, cancellationToken);

        return added ? MuteRecordOutcome.Muted : MuteRecordOutcome.AlreadyMuted;
    }
}
