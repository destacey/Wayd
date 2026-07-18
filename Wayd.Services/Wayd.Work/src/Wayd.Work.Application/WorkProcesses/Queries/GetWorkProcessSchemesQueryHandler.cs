using Wayd.Common.Application.Requests.WorkManagement.Interfaces;
using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkProcesses.Dtos;

namespace Wayd.Work.Application.WorkProcesses.Queries;

public sealed class GetWorkProcessSchemesQueryHandler(IWorkDbContext workDbContext, ILogger<GetWorkProcessSchemesQueryHandler> logger) : IQueryHandler<GetWorkProcessSchemesQuery, IReadOnlyList<IWorkProcessSchemeDto>>
{
    private const string AppRequestName = nameof(GetWorkProcessSchemesQuery);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<GetWorkProcessSchemesQueryHandler> _logger = logger;

    public async Task<IReadOnlyList<IWorkProcessSchemeDto>> Handle(GetWorkProcessSchemesQuery request, CancellationToken cancellationToken)
    {
        return await _workDbContext.WorkProcesses
            .Where(p => p.Id == request.WorkProcessId)
            .SelectMany(p => p.Schemes)
            .ProjectToType<WorkProcessSchemeDto>()
            .ToListAsync(cancellationToken);
    }
}
