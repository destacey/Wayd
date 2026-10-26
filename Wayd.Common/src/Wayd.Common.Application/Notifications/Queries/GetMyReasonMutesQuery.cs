using Wayd.Common.Application.Notifications.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Notifications;

namespace Wayd.Common.Application.Notifications.Queries;

/// <summary>
/// Every reason the caller can mute, with whether they have: one entry per reason something produces today,
/// in the reasons' display order.
/// </summary>
/// <remarks>
/// Not <see cref="IRequireLinkedEmployee"/>: a caller with no employee has muted nothing.
/// </remarks>
public sealed record GetMyReasonMutesQuery : IQuery<List<ReasonMuteDto>>;

/// <summary>Reads the caller's reason mutes against the reasons that can be muted.</summary>
public sealed class GetMyReasonMutesQueryHandler(
    INotificationsDbContext notificationsDbContext,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetMyReasonMutesQuery, List<ReasonMuteDto>>
{
    private readonly INotificationsDbContext _notificationsDbContext = notificationsDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<List<ReasonMuteDto>> Handle(GetMyReasonMutesQuery request, CancellationToken cancellationToken)
    {
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);

        var muted = employeeId is null
            ? []
            : await _notificationsDbContext.ReasonMutes
                .Where(m => m.EmployeeId == employeeId.Value)
                .ToDictionaryAsync(m => m.Reason, m => m.MutedOn, cancellationToken);

        // Flag values follow the enum's display order, so ascending values list the reasons in that order.
        return Enum.GetValues<NotificationReason>()
            .Where(NotificationReasons.IsMutable)
            .Select(reason => new ReasonMuteDto
            {
                Reason = reason,
                IsMuted = muted.ContainsKey(reason),
                MutedOn = muted.TryGetValue(reason, out var mutedOn) ? mutedOn : null,
            })
            .ToList();
    }
}
