using Wayd.ProductManagement.Application.IncidentSeveritySets.Dtos;

namespace Wayd.ProductManagement.Application.IncidentSeveritySets.Queries;

public sealed record GetIncidentSeveritySetsQuery(bool IncludeInactive = false)
    : IQuery<IReadOnlyList<IncidentSeveritySetDto>>;

public sealed class GetIncidentSeveritySetsQueryHandler(IProductManagementDbContext context)
    : IQueryHandler<GetIncidentSeveritySetsQuery, IReadOnlyList<IncidentSeveritySetDto>>
{
    private readonly IProductManagementDbContext _context = context;

    public async Task<IReadOnlyList<IncidentSeveritySetDto>> Handle(
        GetIncidentSeveritySetsQuery request,
        CancellationToken cancellationToken)
    {
        var selectedSetId = await _context.IncidentSeveritySetAssignments
            .Select(a => a.SeveritySetId)
            .SingleAsync(cancellationToken);

        var query = _context.IncidentSeveritySets
            .Include(s => s.Levels)
            .AsQueryable();

        if (!request.IncludeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        var sets = await query
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);

        return sets.Select(s => ToDto(s, selectedSetId)).ToList();
    }

    internal static IncidentSeveritySetDto ToDto(IncidentSeveritySet set, Guid selectedSetId) =>
        new()
        {
            Id = set.Id,
            Name = set.Name,
            Description = set.Description,
            IsSystem = set.IsSystem,
            IsActive = set.IsActive,
            IsSelected = set.Id == selectedSetId,
            Levels = set.Levels
                .OrderBy(l => l.SortOrder)
                .ThenBy(l => l.Id)
                .Select(l => new IncidentSeverityLevelDto
                {
                    Id = l.Id,
                    Name = l.Name,
                    SortOrder = l.SortOrder,
                })
                .ToList(),
        };
}
