using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests;

/// <summary>
/// The filters the requests list offers. Every filter given narrows the list; one left out keeps everything.
/// </summary>
/// <param name="DeskId">Only requests on this desk, when given.</param>
/// <param name="Mine">Only requests the reader is the requester of or submitted.</param>
/// <param name="OpenOnly">Only requests in the Proposed or Active status category.</param>
/// <param name="AssignedToMe">Only requests assigned to the reader.</param>
/// <param name="Participating">Only requests the reader was added to as a participant.</param>
/// <param name="ClosedSince">
/// Closed requests (Done or Removed) only when they closed at or after this instant; open requests are unaffected.
/// </param>
public sealed record IntakeRequestListFilters(
    Guid? DeskId = null,
    bool Mine = false,
    bool OpenOnly = false,
    bool AssignedToMe = false,
    bool Participating = false,
    Instant? ClosedSince = null)
{
    /// <summary>Whether a filter needs the reader's employee, so a user linked to none matches nothing.</summary>
    public bool NeedsEmployee => Mine || AssignedToMe || Participating;
}

/// <summary>
/// Applies <see cref="IntakeRequestListFilters"/>, shared by every query that answers beside the list so the list
/// and its scores, estimates and outcomes always cover the same requests.
/// </summary>
internal static class IntakeRequestListFilter
{
    /// <summary>
    /// The requests matching the filters, of those given; null when a filter about the reader's own requests is
    /// asked by a user linked to no employee, who has none.
    /// </summary>
    /// <remarks>
    /// Who submitted a request and who takes part in it are substance, absent from the summary, so these filters
    /// run here rather than in a client. Neither reveals anything: a reader can always see the substance of a
    /// request they submitted or take part in.
    /// </remarks>
    public static IQueryable<IntakeRequest>? Apply(
        IQueryable<IntakeRequest> query,
        IntakeRequestReader reader,
        IntakeRequestListFilters filters,
        IDemandManagementDbContext dbContext)
    {
        if (filters.DeskId is { } id)
            query = query.Where(r => r.DeskId == id);

        if (filters.NeedsEmployee)
        {
            if (reader.EmployeeId is not { } employeeId)
                return null;

            if (filters.Mine)
                query = query.Where(r => r.RequesterId == employeeId || r.SubmittedById == employeeId);
            if (filters.AssignedToMe)
                query = query.Where(r => r.AssigneeId == employeeId);
            if (filters.Participating)
                query = query.Where(r => r.Participants.Any(p => p.EmployeeId == employeeId));
        }

        if (filters.OpenOnly)
            query = query.Where(r => r.StatusCategory == StatusCategory.Proposed || r.StatusCategory == StatusCategory.Active);

        if (filters.ClosedSince is { } since)
        {
            // A closed request closed at its latest move out of an open status, or at its first entry when it arrived
            // closed (an import). Not at its latest move of any kind: a desk's workflow change moves closed requests
            // between closed statuses, which would make every one look recently closed.
            var transitions = dbContext.IntakeRequestStatusTransitions;
            query = query.Where(r => r.StatusCategory == StatusCategory.Proposed
                || r.StatusCategory == StatusCategory.Active
                || transitions.Where(t => t.RecordId == r.Id)
                    .Where(t => t.FromCategory == null
                        || t.FromCategory == StatusCategory.Proposed
                        || t.FromCategory == StatusCategory.Active)
                    .OrderByDescending(t => t.Sequence)
                    .Select(t => (Instant?)t.ChangedOn)
                    .FirstOrDefault() >= since);
        }

        return query;
    }
}
