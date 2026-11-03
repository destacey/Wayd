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
public sealed record GetIntakeRequestEstimatesQuery(Guid? DeskId = null, bool Mine = false, bool OpenOnly = false)
    : IQuery<List<IntakeRequestCurrentEstimateDto>>;

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
            reader, request.DeskId, request.Mine, request.OpenOnly);
        if (query is null)
            return [];

        return await query
            .OrderBy(r => r.Key)
            .Select(IntakeRequestProjections.Estimate(_dbContext))
            .ToListAsync(cancellationToken);
    }
}
