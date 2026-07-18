using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Planning.Application.Risks.Dtos;

namespace Wayd.Planning.Application.Risks.Queries;

public sealed record GetRiskQuery : IQuery<RiskDetailsDto?>
{
    public GetRiskQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Risk>();
    }

    public Expression<Func<Risk, bool>> IdOrKeyFilter { get; }
}

public sealed class GetRiskQueryHandler : IQueryHandler<GetRiskQuery, RiskDetailsDto?>
{
    private readonly IPlanningDbContext _planningDbContext;
    private readonly ILogger<GetRiskQueryHandler> _logger;

    public GetRiskQueryHandler(IPlanningDbContext planningDbContext, ILogger<GetRiskQueryHandler> logger)
    {
        _planningDbContext = planningDbContext;
        _logger = logger;
    }

    public async Task<RiskDetailsDto?> Handle(GetRiskQuery request, CancellationToken cancellationToken)
    {
        return await _planningDbContext.Risks
            //.Include(r => r.Team)
            //.Include(r => r.ReportedBy)
            //.Include(r => r.Assignee)
            .Where(request.IdOrKeyFilter)
            .ProjectToType<RiskDetailsDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
