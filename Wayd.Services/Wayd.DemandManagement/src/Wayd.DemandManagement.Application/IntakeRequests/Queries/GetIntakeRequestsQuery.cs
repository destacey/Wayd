using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.IntakeRequests.Dtos;

namespace Wayd.DemandManagement.Application.IntakeRequests.Queries;

/// <summary>
/// The intake requests the current user may see, as summaries, optionally on one desk or only their own.
/// </summary>
/// <param name="DeskId">Only requests on this desk, when given.</param>
/// <param name="Mine">
/// Only requests the current user is the requester of or submitted. Filtered here because the submitter is
/// substance and absent from the summary, so a client cannot find requests entered on someone else's behalf.
/// A user linked to no employee has none.
/// </param>
/// <param name="OpenOnly">
/// Only requests still open: in the Proposed or Active status category, whatever the desk's workflow calls the
/// status. What reviewers work from; browsing for an existing ask wants the closed ones too.
/// </param>
public sealed record GetIntakeRequestsQuery(Guid? DeskId = null, bool Mine = false, bool OpenOnly = false) : IQuery<List<IntakeRequestSummaryDto>>;

public sealed class GetIntakeRequestsQueryHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetIntakeRequestsQuery, List<IntakeRequestSummaryDto>>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<List<IntakeRequestSummaryDto>> Handle(GetIntakeRequestsQuery request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);

        var query = _dbContext.IntakeRequests
            .AsNoTracking()
            .Where(reader.CanSeeSummary(_dbContext.IntakeDesks));

        if (request.DeskId is { } deskId)
            query = query.Where(r => r.DeskId == deskId);

        if (request.Mine)
        {
            if (reader.EmployeeId is not { } employeeId)
                return [];

            query = query.Where(r => r.RequesterId == employeeId || r.SubmittedById == employeeId);
        }

        if (request.OpenOnly)
            query = query.Where(r => r.StatusCategory == StatusCategory.Proposed || r.StatusCategory == StatusCategory.Active);

        return await query
            .OrderBy(r => r.Key)
            .Select(IntakeRequestProjections.Summary(_dbContext))
            .ToListAsync(cancellationToken);
    }
}
