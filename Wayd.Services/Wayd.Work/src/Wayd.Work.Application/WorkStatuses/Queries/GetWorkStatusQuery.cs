using Wayd.Common.Application.Exceptions;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkStatuses.Dtos;

namespace Wayd.Work.Application.WorkStatuses.Queries;

public sealed record GetWorkStatusQuery : IQuery<WorkStatusDto?>
{
    public GetWorkStatusQuery(int id)
    {
        Id = id;
    }

    public GetWorkStatusQuery(string name)
    {
        Name = Guard.Against.NullOrWhiteSpace(name).Trim();
    }

    public int? Id { get; }
    public string? Name { get; }
}

public sealed class GetWorkStatusQueryHandler : IQueryHandler<GetWorkStatusQuery, WorkStatusDto?>
{
    private readonly IWorkDbContext _workDbContext;
    private readonly ILogger<GetWorkStatusQueryHandler> _logger;

    public GetWorkStatusQueryHandler(IWorkDbContext workDbContext, ILogger<GetWorkStatusQueryHandler> logger)
    {
        _workDbContext = workDbContext;
        _logger = logger;
    }

    public async Task<WorkStatusDto?> Handle(GetWorkStatusQuery request, CancellationToken cancellationToken)
    {
        var query = _workDbContext.WorkStatuses.AsQueryable();

        if (request.Id.HasValue)
        {
            query = query.Where(s => s.Id == request.Id.Value);
        }
        else if (!string.IsNullOrWhiteSpace(request.Name))
        {
            query = query.Where(s => s.Name == request.Name);
        }
        else
        {
            var requestName = request.GetType().Name;
            var exception = new InternalServerException("No work state id or name provided.");

            _logger.LogError(exception, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            throw exception;
        }

        return await query
            .ProjectToType<WorkStatusDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
