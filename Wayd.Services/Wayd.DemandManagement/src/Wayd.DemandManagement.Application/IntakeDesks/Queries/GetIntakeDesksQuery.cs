using Wayd.DemandManagement.Application.IntakeDesks.Dtos;
using Wayd.DemandManagement.Application.IntakeRequests;

namespace Wayd.DemandManagement.Application.IntakeDesks.Queries;

/// <summary>
/// The intake desks, or only those a requester can submit to now.
/// </summary>
/// <remarks>
/// Each desk carries what the reader may do on it, and, on the desks they work, what is waiting there.
/// A desk is not a request: a private desk's existence, description and members are not hidden, only the
/// requests on it.
/// </remarks>
/// <param name="SubmittableOnly">Only desks that are open and that requesters can pick.</param>
public sealed record GetIntakeDesksQuery(bool SubmittableOnly = false) : IQuery<List<IntakeDeskDto>>;

public sealed class GetIntakeDesksQueryHandler(IDemandManagementDbContext dbContext, ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetIntakeDesksQuery, List<IntakeDeskDto>>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<List<IntakeDeskDto>> Handle(GetIntakeDesksQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.IntakeDesks.AsNoTracking();

        if (request.SubmittableOnly)
            query = query.Where(d => d.IsOpen && d.IsRequesterSelectable);

        var desks = await query
            .OrderBy(d => d.Name).ThenBy(d => d.Key)
            .Select(IntakeDeskProjections.Desk(_dbContext))
            .ToListAsync(cancellationToken);

        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        return await desks.ForReader(reader, _currentPrincipal.IsReadOnly, _dbContext, cancellationToken);
    }
}
