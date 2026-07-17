using Wayd.Common.Models;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.Workspaces.Dtos;

namespace Wayd.Work.Application.Workspaces.Queries;

public sealed record GetWorkspaceQuery : IQuery<Result<WorkspaceDto?>>
{
    public GetWorkspaceQuery(Guid workspaceId)
    {
        Id = Guard.Against.NullOrEmpty(workspaceId);
    }

    public GetWorkspaceQuery(WorkspaceKey workspaceKey)
    {
        Key = Guard.Against.Default(workspaceKey);
    }

    public Guid? Id { get; }
    public WorkspaceKey? Key { get; }
}

public sealed class GetWorkspaceQueryHandler(IWorkDbContext workDbContext, ILogger<GetWorkspaceQueryHandler> logger) : IQueryHandler<GetWorkspaceQuery, Result<WorkspaceDto?>>
{
    private const string AppRequestName = nameof(GetWorkspaceQuery);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<GetWorkspaceQueryHandler> _logger = logger;

    public async Task<Result<WorkspaceDto?>> Handle(GetWorkspaceQuery request, CancellationToken cancellationToken)
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
            return Result.Failure<WorkspaceDto?>("No valid workspace id or key provided.");
        }

        return await query.ProjectToType<WorkspaceDto>().FirstOrDefaultAsync(cancellationToken);
    }
}
