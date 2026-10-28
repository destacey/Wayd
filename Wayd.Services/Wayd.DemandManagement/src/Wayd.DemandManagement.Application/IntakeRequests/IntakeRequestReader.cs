using System.Linq.Expressions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests;

/// <summary>
/// Who is reading intake requests, and the tiers of each request they may read.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Summary</strong> — the desk's members, the request's own people (requester, submitter,
/// participants), and on an <c>Open</c> or <c>SummaryOnly</c> desk everyone with View. On a <c>Private</c>
/// desk the request is absent for anyone else from every list, search and count, never a redacted row.
/// </para>
/// <para>
/// <strong>Substance</strong> — the desk's members and the request's own people, and on an <c>Open</c> desk
/// everyone with View. Working one desk grants nothing on another.
/// </para>
/// <para>
/// <strong>Internal</strong> — the desk's members.
/// </para>
/// Membership is the grant: an agent or Desk Admin needs no claim beside it. The Demand Management Admin
/// (<c>Permissions.IntakeRequests.Administer</c>) acts as Desk Admin on every desk without being a member.
/// The rules are expressions so they run in the database, which keeps paging and totals honest.
/// </remarks>
/// <param name="EmployeeId">The reading employee, or null when the user is linked to none.</param>
/// <param name="HoldsView">Whether the reader holds <c>Permissions.IntakeRequests.View</c>, which browses desks they are not on.</param>
/// <param name="IsDemandManagementAdmin">Whether the reader holds <c>Permissions.IntakeRequests.Administer</c>.</param>
/// <param name="DeskRoles">The reader's role on each desk they are a member of. Empty for an inactive employee.</param>
public sealed record IntakeRequestReader(
    Guid? EmployeeId,
    bool HoldsView,
    bool IsDemandManagementAdmin,
    IReadOnlyDictionary<Guid, IntakeDeskRole> DeskRoles)
{
    /// <summary>
    /// Whether the reader acts on the desk's requests: an agent or Desk Admin on it, or the Demand
    /// Management Admin.
    /// </summary>
    public bool IsAgentOn(Guid deskId) => IsDemandManagementAdmin || DeskRoles.ContainsKey(deskId);

    /// <summary>
    /// Whether the reader runs the desk: a Desk Admin on it, or the Demand Management Admin.
    /// </summary>
    public bool IsDeskAdminOn(Guid deskId) =>
        IsDemandManagementAdmin || (DeskRoles.TryGetValue(deskId, out var role) && role == IntakeDeskRole.Admin);

    /// <summary>
    /// Whether the reader may give someone access to a request as a participant: an agent on its desk
    /// always, and its requester or submitter unless the desk is private and does not allow it. Participants
    /// cannot add others. Needs a linked employee, because the participant records who added them.
    /// </summary>
    /// <param name="request">The request's desk and its own people.</param>
    /// <param name="deskVisibility">The visibility of the request's desk.</param>
    /// <param name="requesterMayAddParticipants">The desk's setting for whether a requester may add participants on a private desk.</param>
    public bool CanAddParticipants(
        IntakeRequestPeople request,
        IntakeDeskVisibility deskVisibility,
        bool requesterMayAddParticipants)
    {
        if (EmployeeId is not { } employeeId)
            return false;

        if (IsAgentOn(request.DeskId))
            return true;

        var isRequesterOrSubmitter = request.RequesterId == employeeId || request.SubmittedById == employeeId;
        var deskAllows = deskVisibility != IntakeDeskVisibility.Private || requesterMayAddParticipants;
        return isRequesterOrSubmitter && deskAllows;
    }

    /// <summary>
    /// Requests whose summary the reader may see. Every other request is absent for them.
    /// </summary>
    public Expression<Func<IntakeRequest, bool>> CanSeeSummary(IQueryable<IntakeDesk> desks)
    {
        if (IsDemandManagementAdmin)
            return r => true;

        var employeeId = EmployeeId ?? Guid.Empty;
        var memberDeskIds = DeskRoles.Keys.ToArray();

        if (!HoldsView)
            return r => memberDeskIds.Contains(r.DeskId)
                || r.RequesterId == employeeId
                || r.SubmittedById == employeeId
                || r.Participants.Any(p => p.EmployeeId == employeeId);

        return r => desks.Any(d => d.Id == r.DeskId && d.Visibility != IntakeDeskVisibility.Private)
            || memberDeskIds.Contains(r.DeskId)
            || r.RequesterId == employeeId
            || r.SubmittedById == employeeId
            || r.Participants.Any(p => p.EmployeeId == employeeId);
    }

    /// <summary>
    /// Requests whose substance the reader may see. Applied only to requests whose summary they can see, which
    /// is what keeps an open desk's substance from a reader without View.
    /// </summary>
    public Expression<Func<IntakeRequest, bool>> CanSeeSubstance(IQueryable<IntakeDesk> desks)
    {
        if (IsDemandManagementAdmin)
            return r => true;

        var employeeId = EmployeeId ?? Guid.Empty;
        var memberDeskIds = DeskRoles.Keys.ToArray();

        return r => desks.Any(d => d.Id == r.DeskId && d.Visibility == IntakeDeskVisibility.Open)
            || memberDeskIds.Contains(r.DeskId)
            || r.RequesterId == employeeId
            || r.SubmittedById == employeeId
            || r.Participants.Any(p => p.EmployeeId == employeeId);
    }

    /// <summary>
    /// Requests whose internal tier the reader may see: those on the desks they work, every one for the Demand
    /// Management Admin. The query form of <see cref="IsAgentOn"/>.
    /// </summary>
    public Expression<Func<IntakeRequest, bool>> CanSeeInternal()
    {
        if (IsDemandManagementAdmin)
            return r => true;

        var memberDeskIds = DeskRoles.Keys.ToArray();
        return r => memberDeskIds.Contains(r.DeskId);
    }
}

/// <summary>
/// The parts of an intake request that decide what its own people may do on it.
/// </summary>
/// <param name="DeskId">The desk the request is on.</param>
/// <param name="RequesterId">The employee the request is for.</param>
/// <param name="SubmittedById">The employee who entered the request.</param>
public sealed record IntakeRequestPeople(Guid DeskId, Guid RequesterId, Guid SubmittedById);
