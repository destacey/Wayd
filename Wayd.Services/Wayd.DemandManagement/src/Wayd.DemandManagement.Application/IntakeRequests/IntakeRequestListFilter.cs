using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests;

/// <summary>
/// The filters the requests list offers, shared by every query that answers beside it so the list and its
/// scores always cover the same requests.
/// </summary>
internal static class IntakeRequestListFilter
{
    /// <summary>
    /// The requests matching the filters, of those given; null when <paramref name="mine"/> is asked by a user
    /// linked to no employee, who has none.
    /// </summary>
    public static IQueryable<IntakeRequest>? Apply(IQueryable<IntakeRequest> query, IntakeRequestReader reader,
        Guid? deskId, bool mine, bool openOnly)
    {
        if (deskId is { } id)
            query = query.Where(r => r.DeskId == id);

        if (mine)
        {
            if (reader.EmployeeId is not { } employeeId)
                return null;

            query = query.Where(r => r.RequesterId == employeeId || r.SubmittedById == employeeId);
        }

        if (openOnly)
            query = query.Where(r => r.StatusCategory == StatusCategory.Proposed || r.StatusCategory == StatusCategory.Active);

        return query;
    }
}
