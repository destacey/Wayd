using System.Linq.Expressions;
using Wayd.DemandManagement.Application.IntakeDesks.Dtos;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeDesks.Queries;

/// <summary>
/// One intake desk, or null when there is none.
/// </summary>
public sealed record GetIntakeDeskQuery : IQuery<IntakeDeskDto?>
{
    public GetIntakeDeskQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<IntakeDesk>();
    }

    public Expression<Func<IntakeDesk, bool>> IdOrKeyFilter { get; }
}

public sealed class GetIntakeDeskQueryHandler(IDemandManagementDbContext dbContext)
    : IQueryHandler<GetIntakeDeskQuery, IntakeDeskDto?>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;

    public async Task<IntakeDeskDto?> Handle(GetIntakeDeskQuery request, CancellationToken cancellationToken) =>
        await _dbContext.IntakeDesks
            .AsNoTracking()
            .Where(request.IdOrKeyFilter)
            .Select(IntakeDeskProjections.Desk(_dbContext))
            .FirstOrDefaultAsync(cancellationToken);
}
