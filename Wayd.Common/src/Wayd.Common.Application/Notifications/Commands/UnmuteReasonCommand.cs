using Wayd.Common.Domain.Notifications;

namespace Wayd.Common.Application.Notifications.Commands;

/// <summary>
/// Unmutes a reason for the caller. Succeeds whether or not they had muted it.
/// </summary>
/// <remarks>
/// Accepts any defined reason, so a mute of a reason that has since stopped being produced can still be
/// cleared. Brings back nothing written while it was muted.
/// </remarks>
public sealed record UnmuteReasonCommand(NotificationReason Reason) : ICommand, IRequireLinkedEmployee;

/// <summary>Refuses a value that is not made of defined reasons.</summary>
public sealed class UnmuteReasonCommandValidator : CustomValidator<UnmuteReasonCommand>
{
    public UnmuteReasonCommandValidator()
    {
        RuleFor(c => c.Reason).IsInEnum();
    }
}

/// <summary>Removes the caller's mute of the reason, if there is one.</summary>
public sealed class UnmuteReasonCommandHandler(
    IMuteStore muteStore,
    ICurrentPrincipal currentPrincipal)
    : ICommandHandler<UnmuteReasonCommand>
{
    private readonly IMuteStore _muteStore = muteStore;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<Result> Handle(UnmuteReasonCommand request, CancellationToken cancellationToken)
    {
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
        {
            LinkedEmployeeRequired.Throw();
        }

        await _muteStore.RemoveReasonMute(employeeId.Value, request.Reason, cancellationToken);

        return Result.Success();
    }
}
