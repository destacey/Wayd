using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Workspaces.Queries;

public sealed class GetWorkspaceMostRecentChangeDateQueryHandler(IWorkDbContext workDbContext, ILogger<GetWorkspaceMostRecentChangeDateQueryHandler> logger) : IQueryHandler<GetWorkspaceMostRecentChangeDateQuery, Result<Instant?>>
{
    private const string AppRequestName = nameof(GetWorkspaceMostRecentChangeDateQuery);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<GetWorkspaceMostRecentChangeDateQueryHandler> _logger = logger;

    public async Task<Result<Instant?>> Handle(GetWorkspaceMostRecentChangeDateQuery request, CancellationToken cancellationToken)
    {
        var query = _workDbContext.Workspaces
            .AsQueryable();

        if (request.Id.HasValue)
        {
            query = query.Where(e => e.Id == request.Id);
        }
        else if (request.Key is not null)
        {
            query = query.Where(e => e.Key == request.Key);
        }
        else
        {
            _logger.LogError("{AppRequestName}: No workspace id or key provided. {@Request}", AppRequestName, request);
            return Result.Failure<Instant?>("No valid workspace id or key provided.");
        }

        var lastModified = await query
            .SelectMany(e => e.WorkItems)
            .Select(w => (Instant?)w.LastModified)
            .MaxAsync(cancellationToken);

        return lastModified;
    }
}
