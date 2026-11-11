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
/// <param name="AssignedToMe">Only requests assigned to the current user.</param>
/// <param name="Participating">
/// Only requests the current user was added to as a participant. Filtered here because participants are substance.
/// </param>
/// <param name="ClosedSince">Closed requests only when they closed at or after this instant; open ones are unaffected.</param>
public sealed record GetIntakeRequestsQuery(
    Guid? DeskId = null,
    bool Mine = false,
    bool OpenOnly = false,
    bool AssignedToMe = false,
    bool Participating = false,
    Instant? ClosedSince = null) : IQuery<List<IntakeRequestSummaryDto>>
{
    /// <summary>The filters, as every query answering beside the list applies them.</summary>
    public IntakeRequestListFilters Filters => new(DeskId, Mine, OpenOnly, AssignedToMe, Participating, ClosedSince);
}

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

        var query = IntakeRequestListFilter.Apply(
            _dbContext.IntakeRequests.AsNoTracking().Where(reader.CanSeeSummary(_dbContext.IntakeDesks)),
            reader, request.Filters, _dbContext);
        if (query is null)
            return [];

        return await query
            .OrderBy(r => r.Key)
            .Select(IntakeRequestProjections.Summary(_dbContext))
            .ToListAsync(cancellationToken);
    }
}
