using Wayd.Common.Application.Requests.WorkManagement.Interfaces;
using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkProcesses.Dtos;

namespace Wayd.Work.Application.WorkProcesses.Queries;

public sealed class GetWorkProcessSchemesQueryHandler(IWorkDbContext workDbContext, ILogger<GetWorkProcessSchemesQueryHandler> logger) : IQueryHandler<GetWorkProcessSchemesQuery, IReadOnlyList<IWorkProcessSchemeDto>?>
{
    private const string AppRequestName = nameof(GetWorkProcessSchemesQuery);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<GetWorkProcessSchemesQueryHandler> _logger = logger;

    /// <summary>The work process's schemes ordered by work type name, or null when no work process has the id.</summary>
    public async Task<IReadOnlyList<IWorkProcessSchemeDto>?> Handle(GetWorkProcessSchemesQuery request, CancellationToken cancellationToken)
    {
        var exists = await _workDbContext.WorkProcesses
            .AnyAsync(p => p.Id == request.WorkProcessId, cancellationToken);
        if (!exists)
            return null;

        return await _workDbContext.WorkProcesses
            .Where(p => p.Id == request.WorkProcessId)
            .SelectMany(p => p.Schemes)
            .OrderBy(s => s.WorkType!.Name)
            .ThenBy(s => s.Id)
            .ProjectToType<WorkProcessSchemeDto>()
            .ToListAsync(cancellationToken);
    }
}
