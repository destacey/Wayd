namespace Wayd.Common.Application.Notifications.Commands;

/// <summary>
/// Marks every notification the caller has read, including any about records they can no longer see.
/// </summary>
public sealed record MarkAllNotificationsReadCommand : ICommand, IRequireLinkedEmployee;

/// <summary>Marks all of the caller's notifications read.</summary>
public sealed class MarkAllNotificationsReadCommandHandler(
    INotificationStore notificationStore,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<MarkAllNotificationsReadCommand>
{
    private readonly INotificationStore _notificationStore = notificationStore;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
        {
            LinkedEmployeeRequired.Throw();
        }

        await _notificationStore.MarkAllRead(employeeId.Value, _dateTimeProvider.Now, cancellationToken);

        return Result.Success();
    }
}
