using Mapster;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Employees.Queries;

public sealed record GetDirectReportsQuery(Guid EmployeeId) : IQuery<IReadOnlyList<EmployeeListDto>>;

public sealed class GetDirectReportsQueryHandler : IQueryHandler<GetDirectReportsQuery, IReadOnlyList<EmployeeListDto>>
{
    private readonly IWaydDbContext _waydDbContext;
    private readonly ILogger<GetDirectReportsQueryHandler> _logger;

    public GetDirectReportsQueryHandler(IWaydDbContext waydDbContext, ILogger<GetDirectReportsQueryHandler> logger)
    {
        _waydDbContext = waydDbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<EmployeeListDto>> Handle(GetDirectReportsQuery request, CancellationToken cancellationToken)
    {
        return await _waydDbContext.Employees
            .Where(e => e.ManagerId == request.EmployeeId && e.IsActive)
            .ProjectToType<EmployeeListDto>()
            .ToListAsync(cancellationToken);
    }
}
