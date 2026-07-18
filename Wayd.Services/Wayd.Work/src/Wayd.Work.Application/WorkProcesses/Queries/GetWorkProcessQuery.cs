using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkProcesses.Dtos;

namespace Wayd.Work.Application.WorkProcesses.Queries;

public sealed record GetWorkProcessQuery : IQuery<Result<WorkProcessDto?>>
{
    public GetWorkProcessQuery(Guid workProcessId)
    {
        Id = Guard.Against.NullOrEmpty(workProcessId);
    }

    public GetWorkProcessQuery(int workProcessKey)
    {
        Key = Guard.Against.Default(workProcessKey);
    }

    public Guid? Id { get; }
    public int? Key { get; }
}

public sealed class GetWorkProcessQueryHandler(IWorkDbContext workDbContext, ILogger<GetWorkProcessQueryHandler> logger) : IQueryHandler<GetWorkProcessQuery, Result<WorkProcessDto?>>
{
    private const string AppRequestName = nameof(GetWorkProcessQuery);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<GetWorkProcessQueryHandler> _logger = logger;

    public async Task<Result<WorkProcessDto?>> Handle(GetWorkProcessQuery request, CancellationToken cancellationToken)
    {
        var query = _workDbContext.WorkProcesses
            .AsQueryable();

        if (request.Id.HasValue)
        {
            query = query.Where(e => e.Id == request.Id);
        }
        else if (request.Key.HasValue)
        {
            query = query.Where(e => e.Key == request.Key);
        }
        else
        {
            _logger.LogError("{AppRequestName}: No work process id or key provided. {@Request}", AppRequestName, request);
            return Result.Failure<WorkProcessDto?>("No valid work process id or key provided.");
        }

        return await query.ProjectToType<WorkProcessDto>().FirstOrDefaultAsync(cancellationToken);
    }
}
