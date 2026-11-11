using Wayd.DemandManagement.Application.IntakeRequests.Dtos;

namespace Wayd.DemandManagement.Application.IntakeRequests.Queries;

/// <summary>
/// The estimates of the requests the requests list would return for the same filters, for the requests whose
/// estimate the current user may read. A request never estimated, or whose substance they may not see, is absent.
/// </summary>
/// <remarks>
/// Answered beside <see cref="GetIntakeRequestsQuery"/> rather than on its summaries, because an estimate is
/// substance and a summary carries nothing from that tier.
/// </remarks>
/// <param name="DeskId">Only requests on this desk, when given.</param>
/// <param name="Mine">Only requests the current user is the requester of or submitted.</param>
/// <param name="OpenOnly">Only requests in the Proposed or Active status category.</param>
/// <param name="AssignedToMe">Only requests assigned to the current user.</param>
/// <param name="Participating">
/// Only requests the current user was added to as a participant. Filtered here because participants are substance.
/// </param>
/// <param name="ClosedSince">Closed requests only when they closed at or after this instant; open ones are unaffected.</param>
public sealed record GetIntakeRequestEstimatesQuery(
    Guid? DeskId = null,
    bool Mine = false,
    bool OpenOnly = false,
    bool AssignedToMe = false,
    bool Participating = false,
    Instant? ClosedSince = null)
    : IQuery<List<IntakeRequestCurrentEstimateDto>>
{
    /// <summary>The filters, as every query answering beside the list applies them.</summary>
    public IntakeRequestListFilters Filters => new(DeskId, Mine, OpenOnly, AssignedToMe, Participating, ClosedSince);
}

/// <summary>
/// Applies the requests list's filters and the substance rule, then projects each estimate with its scale.
/// </summary>
public sealed class GetIntakeRequestEstimatesQueryHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetIntakeRequestEstimatesQuery, List<IntakeRequestCurrentEstimateDto>>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<List<IntakeRequestCurrentEstimateDto>> Handle(GetIntakeRequestEstimatesQuery request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        var desks = _dbContext.IntakeDesks;

        var query = IntakeRequestListFilter.Apply(
            _dbContext.IntakeRequests.AsNoTracking()
                .Where(reader.CanSeeSummary(desks))
                .Where(reader.CanSeeSubstance(desks))
                .Where(r => r.Estimate != null),
            reader, request.Filters, _dbContext);
        if (query is null)
            return [];

        return await query
            .OrderBy(r => r.Key)
            .Select(IntakeRequestProjections.Estimate(_dbContext))
            .ToListAsync(cancellationToken);
    }
}
