namespace Wayd.Common.Application.Notifications.Commands;

/// <summary>
/// Marks one of the caller's notifications read, answering false when they have no notification with that id.
/// Marking one already read changes nothing.
/// </summary>
public sealed record MarkNotificationReadCommand(long NotificationId) : ICommand<bool>, IRequireLinkedEmployee;

/// <summary>Rejects an id no notification can have.</summary>
public sealed class MarkNotificationReadCommandValidator : CustomValidator<MarkNotificationReadCommand>
{
    public MarkNotificationReadCommandValidator()
    {
        RuleFor(c => c.NotificationId).GreaterThan(0);
    }
}

/// <summary>Marks the caller's notification read.</summary>
public sealed class MarkNotificationReadCommandHandler(
    INotificationStore notificationStore,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<MarkNotificationReadCommand, bool>
{
    private readonly INotificationStore _notificationStore = notificationStore;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<bool>> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
        {
            LinkedEmployeeRequired.Throw();
        }

        return await _notificationStore.MarkRead(employeeId.Value, request.NotificationId, _dateTimeProvider.Now, cancellationToken);
    }
}
