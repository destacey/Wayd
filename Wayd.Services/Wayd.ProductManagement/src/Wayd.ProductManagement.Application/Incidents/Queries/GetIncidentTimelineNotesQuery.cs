using System.Linq.Expressions;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Application.Models;
using Wayd.ProductManagement.Application.Incidents.Dtos;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Incidents.Queries;

public sealed record GetIncidentTimelineNotesQuery : IQuery<IReadOnlyList<IncidentTimelineNoteDto>>
{
    public GetIncidentTimelineNotesQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Incident>();
    }

    public Expression<Func<Incident, bool>> IdOrKeyFilter { get; }
}

public sealed class GetIncidentTimelineNotesQueryHandler(IProductManagementDbContext context)
    : IQueryHandler<GetIncidentTimelineNotesQuery, IReadOnlyList<IncidentTimelineNoteDto>>
{
    private readonly IProductManagementDbContext _context = context;

    public async Task<IReadOnlyList<IncidentTimelineNoteDto>> Handle(
        GetIncidentTimelineNotesQuery request,
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

        return await _context.IncidentTimelineNotes
            .AsNoTracking()
            .Where(n => n.IncidentId == incidentId)
            .OrderBy(n => n.RecordedAt)
            .Join(
                // A deleted employee's entries stay on the timeline, under their name.
                _context.Employees.AsNoTracking().IgnoreQueryFilters(),
                note => note.AuthorEmployeeId,
                employee => employee.Id,
                (note, employee) => new IncidentTimelineNoteDto
                {
                    Id = note.Id,
                    Body = note.Body,
                    RecordedAt = note.RecordedAt,
                    Author = EmployeeNavigationDto.From(employee),
                })
            .ToListAsync(cancellationToken);
    }
}
