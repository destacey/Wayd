using Wayd.Common.Domain.Events;

namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// Who is editing an intake request, and whether they hold the Update permission.
/// </summary>
/// <remarks>
/// Built once per command in the application layer and passed into the request, which decides whether the
/// edit is allowed: the edit lock depends on the request's status category and on whether the editor is
/// its requester, and only the request can answer both. The domain never reads claims itself.
/// </remarks>
/// <param name="EmployeeId">The editing employee, or null for the platform.</param>
/// <param name="HoldsUpdate">Whether the editor holds <c>Permissions.IntakeRequests.Update</c>.</param>
/// <param name="Actor">Who the events the edit raises are attributed to.</param>
public sealed record IntakeRequestEditor(Guid? EmployeeId, bool HoldsUpdate, EventActor Actor)
{
    /// <summary>
    /// The platform, for importers and integrations, which edit as an Update holder.
    /// </summary>
    public static IntakeRequestEditor System { get; } = new(null, HoldsUpdate: true, EventActor.System);
}
