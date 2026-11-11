using Wayd.DemandManagement.Application.IntakeRequests.Dtos;

namespace Wayd.DemandManagement.Application.IntakeRequests.Queries;

/// <summary>
/// The outcomes of the requests the requests list would return for the same filters, so a list can show what
/// each request became without reading every request's details. A request with no outcome is absent.
/// </summary>
/// <remarks>
/// Outcomes are summary tier, so every request the list returns has its outcomes here.
/// </remarks>
/// <param name="DeskId">Only requests on this desk, when given.</param>
/// <param name="Mine">Only requests the current user is the requester of or submitted.</param>
/// <param name="OpenOnly">Only requests in the Proposed or Active status category.</param>
/// <param name="AssignedToMe">Only requests assigned to the current user.</param>
/// <param name="Participating">Only requests the current user was added to as a participant.</param>
/// <param name="ClosedSince">Closed requests only when they closed at or after this instant; open ones are unaffected.</param>
public sealed record GetIntakeRequestOutcomesQuery(
    Guid? DeskId = null,
    bool Mine = false,
    bool OpenOnly = false,
    bool AssignedToMe = false,
    bool Participating = false,
    Instant? ClosedSince = null)
    : IQuery<List<IntakeRequestOutcomesDto>>
{
    /// <summary>The filters, as every query answering beside the list applies them.</summary>
    public IntakeRequestListFilters Filters => new(DeskId, Mine, OpenOnly, AssignedToMe, Participating, ClosedSince);
}

/// <summary>
/// Applies the requests list's filters and the summary rule, then reads the matching requests' outcomes.
/// </summary>
public sealed class GetIntakeRequestOutcomesQueryHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetIntakeRequestOutcomesQuery, List<IntakeRequestOutcomesDto>>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<List<IntakeRequestOutcomesDto>> Handle(GetIntakeRequestOutcomesQuery request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);

        var query = IntakeRequestListFilter.Apply(
            _dbContext.IntakeRequests.AsNoTracking().Where(reader.CanSeeSummary(_dbContext.IntakeDesks)),
            reader, request.Filters, _dbContext);
        if (query is null)
            return [];

        var outcomes = await IntakeRequestOutcomeReader.Read(_dbContext, query, cancellationToken);

        return
        [
            .. outcomes.Select(r => new IntakeRequestOutcomesDto
            {
                RequestId = r.RequestId,
                Outcomes = r.Outcomes,
                DeliverablesMet = IntakeRequestOutcomeReader.DeliverablesMet(r.Outcomes),
                DeliveryState = IntakeRequestDelivery.StateOf(r.Outcomes),
            }),
        ];
    }
}
