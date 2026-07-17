using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.StrategicManagement.Application.Visions.Dtos;
using Wayd.StrategicManagement.Domain.Models;

namespace Wayd.StrategicManagement.Application.Visions.Queries;

/// <summary>
/// Get a Vision by Id or Key
/// </summary>
public sealed record GetVisionQuery : IQuery<VisionDto?>
{
    public GetVisionQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Vision>();
    }

    public Expression<Func<Vision, bool>> IdOrKeyFilter { get; }
}

public sealed class GetVisionQueryHandler(IStrategicManagementDbContext strategicManagementDbContext) : IQueryHandler<GetVisionQuery, VisionDto?>
{
    private readonly IStrategicManagementDbContext _strategicManagementDbContext = strategicManagementDbContext;

    public async Task<VisionDto?> Handle(GetVisionQuery request, CancellationToken cancellationToken)
    {
        return await _strategicManagementDbContext.Visions
            .Where(request.IdOrKeyFilter)
            .ProjectToType<VisionDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
