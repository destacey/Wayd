using Wayd.Common.Domain.Following;

namespace Wayd.Common.Application.Notifications.Commands;

/// <summary>
/// Marks every notification the caller has about one record read: the whole entry the list shows for it.
/// </summary>
/// <remarks>
/// Does not ask whether the caller can still read the record: marking your own notifications read reveals
/// nothing.
/// </remarks>
public sealed record MarkRecordNotificationsReadCommand(string OwnerType, Guid OwnerId) : ICommand, IRequireLinkedEmployee;

/// <summary>Rejects a missing owner type or id before the handler resolves either.</summary>
public sealed class MarkRecordNotificationsReadCommandValidator : CustomValidator<MarkRecordNotificationsReadCommand>
{
    public MarkRecordNotificationsReadCommandValidator()
    {
        RuleFor(c => c.OwnerType).NotEmpty().MaximumLength(FollowOwnerDescriptor.KeyMaxLength);
        RuleFor(c => c.OwnerId).NotEmpty();
    }
}

/// <summary>Marks the caller's notifications about the record read.</summary>
public sealed class MarkRecordNotificationsReadCommandHandler(
    INotificationStore notificationStore,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<MarkRecordNotificationsReadCommand>
{
    private readonly INotificationStore _notificationStore = notificationStore;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(MarkRecordNotificationsReadCommand request, CancellationToken cancellationToken)
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

        await _notificationStore.MarkRecordRead(employeeId.Value, descriptor.Value.Key, request.OwnerId, _dateTimeProvider.Now, cancellationToken);

        return Result.Success();
    }
}
