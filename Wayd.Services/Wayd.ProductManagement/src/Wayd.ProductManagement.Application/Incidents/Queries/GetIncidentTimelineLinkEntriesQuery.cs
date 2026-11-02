using System.Linq.Expressions;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Models;
using Wayd.ProductManagement.Application.Incidents.Dtos;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Incidents.Queries;

public sealed record GetIncidentTimelineLinkEntriesQuery : IQuery<IReadOnlyList<IncidentTimelineLinkEntryDto>>
{
    public GetIncidentTimelineLinkEntriesQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Incident>();
    }

    public Expression<Func<Incident, bool>> IdOrKeyFilter { get; }
}

public sealed class GetIncidentTimelineLinkEntriesQueryHandler(IProductManagementDbContext context)
    : IQueryHandler<GetIncidentTimelineLinkEntriesQuery, IReadOnlyList<IncidentTimelineLinkEntryDto>>
{
    private readonly IProductManagementDbContext _context = context;

    public async Task<IReadOnlyList<IncidentTimelineLinkEntryDto>> Handle(
        GetIncidentTimelineLinkEntriesQuery request,
        CancellationToken cancellationToken)
    {
        var incidentId = await _context.Incidents
            .AsNoTracking()
            .Where(request.IdOrKeyFilter)
            .Select(i => (Guid?)i.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (incidentId is null)
        {
            return [];
        }

        return await _context.IncidentTimelineLinkEntries
            .AsNoTracking()
            .Where(e => e.IncidentId == incidentId)
            .OrderBy(e => e.RecordedAt)
            .Join(
                _context.Employees.AsNoTracking(),
                entry => entry.ActorEmployeeId,
                employee => employee.Id,
                (entry, employee) => new IncidentTimelineLinkEntryDto
                {
                    Id = entry.Id,
                    Kind = entry.Kind,
                    Action = entry.Action,
                    Label = entry.Label,
                    RecordedAt = entry.RecordedAt,
                    Actor = NavigationDto.Create(
                        employee.Id,
                        employee.Key,
                        employee.Name.FullName),
                })
            .ToListAsync(cancellationToken);
    }
}
