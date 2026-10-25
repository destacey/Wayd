using System.Linq.Expressions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests;

/// <summary>
/// Who is reading intake requests, and the tiers of each request they may read.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Summary</strong> — on an <c>Open</c> or <c>SummaryOnly</c> desk, everyone with View. On a
/// <c>Private</c> desk, only the desk's reviewers and the request's own people (requester, submitter,
/// participants); for anyone else the request is absent from every list, search and count, never a
/// redacted row.
/// </para>
/// <para>
/// <strong>Substance</strong> — on an <c>Open</c> desk, everyone with View; otherwise the request's own
/// people, and anyone who reviews on any desk, because a reviewer cannot judge whether a request belongs on
/// another desk without reading it. A private desk still withholds even the summary from reviewers of other
/// desks: the substance rule applies only to requests the reader can see at all.
/// </para>
/// <para>
/// <strong>Internal</strong> — Review on the request's desk.
/// </para>
/// Review is the Review permission plus being named a reviewer on the desk; the Administer permission waives
/// the membership but never the permission, the same shape as <c>SprintAuthorization.CanManageTeamSprints</c>.
/// The rules are expressions so they run in the database, which keeps paging and totals honest.
/// </remarks>
/// <param name="EmployeeId">The reading employee, or null when the user is linked to none.</param>
/// <param name="HoldsReview">Whether the reader holds <c>Permissions.IntakeRequests.Review</c>.</param>
/// <param name="HoldsAdminister">Whether the reader holds <c>Permissions.IntakeRequests.Administer</c>.</param>
/// <param name="ReviewerDeskIds">The desks the reader is named a reviewer on.</param>
public sealed record IntakeRequestReader(
    Guid? EmployeeId,
    bool HoldsReview,
    bool HoldsAdminister,
    IReadOnlyCollection<Guid> ReviewerDeskIds)
{
    /// <summary>
    /// Whether the reader reviews on every desk: Review with Administer waiving the membership.
    /// </summary>
    public bool ReviewsEveryDesk => HoldsReview && HoldsAdminister;

    /// <summary>
    /// Whether the reader reviews on at least one desk.
    /// </summary>
    public bool ReviewsAnyDesk => ReviewsEveryDesk || (HoldsReview && ReviewerDeskIds.Count > 0);

    /// <summary>
    /// Whether the reader may review requests on the desk.
    /// </summary>
    public bool CanReview(Guid deskId) => ReviewsEveryDesk || (HoldsReview && ReviewerDeskIds.Contains(deskId));

    /// <summary>
    /// Whether the reader may give someone access to a request as a participant: a reviewer on its desk
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

        if (CanReview(request.DeskId))
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
        if (ReviewsEveryDesk)
            return r => true;

        var employeeId = EmployeeId ?? Guid.Empty;
        var reviewedDeskIds = HoldsReview ? ReviewerDeskIds.ToArray() : [];

        return r => desks.Any(d => d.Id == r.DeskId && d.Visibility != IntakeDeskVisibility.Private)
            || reviewedDeskIds.Contains(r.DeskId)
            || r.RequesterId == employeeId
            || r.SubmittedById == employeeId
            || r.Participants.Any(p => p.EmployeeId == employeeId);
    }

    /// <summary>
    /// Requests whose substance the reader may see. Applied to requests whose summary they can see.
    /// </summary>
    public Expression<Func<IntakeRequest, bool>> CanSeeSubstance(IQueryable<IntakeDesk> desks)
    {
        if (ReviewsAnyDesk)
            return r => true;

        var employeeId = EmployeeId ?? Guid.Empty;

        return r => desks.Any(d => d.Id == r.DeskId && d.Visibility == IntakeDeskVisibility.Open)
            || r.RequesterId == employeeId
            || r.SubmittedById == employeeId
            || r.Participants.Any(p => p.EmployeeId == employeeId);
    }
}

/// <summary>
/// The parts of an intake request that decide what its own people may do on it.
/// </summary>
/// <param name="DeskId">The desk the request is on.</param>
/// <param name="RequesterId">The employee the request is for.</param>
/// <param name="SubmittedById">The employee who entered the request.</param>
public sealed record IntakeRequestPeople(Guid DeskId, Guid RequesterId, Guid SubmittedById);
