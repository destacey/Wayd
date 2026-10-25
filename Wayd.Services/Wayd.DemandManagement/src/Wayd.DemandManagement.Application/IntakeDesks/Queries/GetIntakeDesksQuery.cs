using Wayd.DemandManagement.Application.IntakeDesks.Dtos;

namespace Wayd.DemandManagement.Application.IntakeDesks.Queries;

/// <summary>
/// The intake desks, or only those a requester can submit to now.
/// </summary>
/// <remarks>
/// A desk is not a request: a private desk's existence, description and members are not hidden, only the
/// requests on it.
/// </remarks>
/// <param name="SubmittableOnly">Only desks that are open and that requesters can pick.</param>
public sealed record GetIntakeDesksQuery(bool SubmittableOnly = false) : IQuery<List<IntakeDeskDto>>;

public sealed class GetIntakeDesksQueryHandler(IDemandManagementDbContext dbContext)
    : IQueryHandler<GetIntakeDesksQuery, List<IntakeDeskDto>>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;

    public async Task<List<IntakeDeskDto>> Handle(GetIntakeDesksQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.IntakeDesks.AsNoTracking();

        if (request.SubmittableOnly)
            query = query.Where(d => d.IsOpen && d.IsRequesterSelectable);

        return await query
            .OrderBy(d => d.Name).ThenBy(d => d.Key)
            .Select(IntakeDeskProjections.Desk(_dbContext))
            .ToListAsync(cancellationToken);
    }
}
