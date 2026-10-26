using Wayd.Common.Domain.Notifications;

namespace Wayd.Common.Application.Notifications.Commands;

/// <summary>
/// Mutes a reason for the caller: they are no longer told about any record for that reason, and still told
/// for any other reason that applies.
/// </summary>
/// <remarks>
/// Only a reason something produces today can be muted (<see cref="NotificationReasons.Produced"/>). Muting
/// changes no follow, role or access, and leaves existing notifications as they are.
/// </remarks>
public sealed record MuteReasonCommand(NotificationReason Reason) : ICommand, IRequireLinkedEmployee;

/// <summary>Refuses anything but a single reason that can be muted.</summary>
public sealed class MuteReasonCommandValidator : CustomValidator<MuteReasonCommand>
{
    private static readonly string MutableNames = string.Join(
        ", ", Enum.GetValues<NotificationReason>().Where(NotificationReasons.IsMutable));

    public MuteReasonCommandValidator()
    {
        RuleFor(c => c.Reason)
            .Must(NotificationReasons.IsMutable)
            .WithMessage($"'Reason' must be one of: {MutableNames}.");
    }
}

/// <summary>Mutes the reason for the caller; muting it again changes nothing.</summary>
public sealed class MuteReasonCommandHandler(
    IMuteStore muteStore,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<MuteReasonCommand>
{
    private readonly IMuteStore _muteStore = muteStore;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(MuteReasonCommand request, CancellationToken cancellationToken)
    {
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
        {
            LinkedEmployeeRequired.Throw();
        }

        var mute = ReasonMute.Create(employeeId.Value, request.Reason, _dateTimeProvider.Now);
        if (mute.IsFailure)
        {
            return Result.Failure(mute.Error);
        }

        await _muteStore.Add(mute.Value, cancellationToken);

        return Result.Success();
    }
}
