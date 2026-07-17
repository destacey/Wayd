using Wayd.Common.Application.Exceptions;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkTypes.Dtos;

namespace Wayd.Work.Application.WorkTypes.Queries;

public sealed record GetWorkTypeQuery : IQuery<WorkTypeDto?>
{
    public GetWorkTypeQuery(int id)
    {
        Id = id;
    }

    public GetWorkTypeQuery(string name)
    {
        Name = Guard.Against.NullOrWhiteSpace(name).Trim();
    }

    public int? Id { get; }
    public string? Name { get; }
}

public sealed class GetWorkTypeQueryHandler : IQueryHandler<GetWorkTypeQuery, WorkTypeDto?>
{
    private readonly IWorkDbContext _workDbContext;
    private readonly ILogger<GetWorkTypeQueryHandler> _logger;

    public GetWorkTypeQueryHandler(IWorkDbContext workDbContext, ILogger<GetWorkTypeQueryHandler> logger)
    {
        _workDbContext = workDbContext;
        _logger = logger;
    }

    public async Task<WorkTypeDto?> Handle(GetWorkTypeQuery request, CancellationToken cancellationToken)
    {
        var query = _workDbContext.WorkTypes.AsQueryable();

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
            var exception = new InternalServerException("No work type id or name provided.");

            _logger.LogError(exception, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            throw exception;
        }

        return await query
            .ProjectToType<WorkTypeDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
