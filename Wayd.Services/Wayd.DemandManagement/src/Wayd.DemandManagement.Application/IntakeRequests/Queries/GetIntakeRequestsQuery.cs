using Wayd.DemandManagement.Application.IntakeRequests.Dtos;

namespace Wayd.DemandManagement.Application.IntakeRequests.Queries;

/// <summary>
/// The intake requests the current user may see, as summaries, optionally on one desk.
/// </summary>
/// <param name="DeskId">Only requests on this desk, when given.</param>
public sealed record GetIntakeRequestsQuery(Guid? DeskId = null) : IQuery<List<IntakeRequestSummaryDto>>;

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

        return await query
            .OrderBy(r => r.Key)
            .Select(IntakeRequestProjections.Summary(_dbContext))
            .ToListAsync(cancellationToken);
    }
}
