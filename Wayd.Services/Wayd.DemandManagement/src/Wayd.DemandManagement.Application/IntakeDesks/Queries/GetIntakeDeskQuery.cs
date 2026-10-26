using System.Linq.Expressions;
using Wayd.DemandManagement.Application.IntakeDesks.Dtos;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeDesks.Queries;

/// <summary>
/// One intake desk with what the reader may do on it, or null when there is none.
/// </summary>
public sealed record GetIntakeDeskQuery : IQuery<IntakeDeskDto?>
{
    public GetIntakeDeskQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<IntakeDesk>();
    }

    public Expression<Func<IntakeDesk, bool>> IdOrKeyFilter { get; }
}

public sealed class GetIntakeDeskQueryHandler(IDemandManagementDbContext dbContext, ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetIntakeDeskQuery, IntakeDeskDto?>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<IntakeDeskDto?> Handle(GetIntakeDeskQuery request, CancellationToken cancellationToken)
    {
        var desk = await _dbContext.IntakeDesks
            .AsNoTracking()
            .Where(request.IdOrKeyFilter)
            .Select(IntakeDeskProjections.Desk(_dbContext))
            .FirstOrDefaultAsync(cancellationToken);
        if (desk is null)
            return null;

        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        return (await new List<IntakeDeskDto> { desk }.ForReader(reader, _currentPrincipal.IsReadOnly, _dbContext, cancellationToken)).Single();
    }
}
