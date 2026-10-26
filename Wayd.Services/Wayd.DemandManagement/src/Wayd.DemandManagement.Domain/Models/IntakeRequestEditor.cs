using Wayd.Common.Domain.Events;

namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// Who is editing an intake request, and whether they work the request's desk.
/// </summary>
/// <remarks>
/// Built once per command in the application layer and passed into the request, which decides whether the
/// edit is allowed: the edit lock depends on the request's status category and on whether the editor is
/// its requester, and only the request can answer both. The domain never reads claims or memberships itself.
/// </remarks>
/// <param name="EmployeeId">The editing employee, or null for the platform.</param>
/// <param name="IsDeskAgent">Whether the editor is an agent or Desk Admin on the request's desk, or the Demand Management Admin.</param>
/// <param name="Actor">Who the events the edit raises are attributed to.</param>
public sealed record IntakeRequestEditor(Guid? EmployeeId, bool IsDeskAgent, EventActor Actor)
{
    /// <summary>
    /// The platform, for importers and integrations, which edit as an agent.
    /// </summary>
    public static IntakeRequestEditor System { get; } = new(null, IsDeskAgent: true, EventActor.System);
}
