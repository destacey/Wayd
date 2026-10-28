using Wayd.ProductManagement.Application.IncidentSeveritySets.Dtos;

namespace Wayd.ProductManagement.Application.IncidentSeveritySets.Queries;

public sealed record GetIncidentSeveritySetQuery(Guid Id) : IQuery<IncidentSeveritySetDto?>;

public sealed class GetIncidentSeveritySetQueryHandler(IProductManagementDbContext context)
    : IQueryHandler<GetIncidentSeveritySetQuery, IncidentSeveritySetDto?>
{
    private readonly IProductManagementDbContext _context = context;

    public async Task<IncidentSeveritySetDto?> Handle(
        GetIncidentSeveritySetQuery request,
        CancellationToken cancellationToken)
    {
        var selectedSetId = await _context.IncidentSeveritySetAssignments
            .Select(a => a.SeveritySetId)
            .SingleAsync(cancellationToken);

        var set = await _context.IncidentSeveritySets
            .Include(s => s.Levels)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        return set is null ? null : GetIncidentSeveritySetsQueryHandler.ToDto(set, selectedSetId);
    }
}
