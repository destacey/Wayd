using Wayd.StrategicManagement.Application.Visions.Dtos;
using Wayd.StrategicManagement.Domain.Enums;

namespace Wayd.StrategicManagement.Application.Visions.Queries;

/// <summary>
/// Retrieves a list of Visions based on the provided filter.  Returns all Visions if no filter is provided.
/// </summary>
/// <param name="StateFilter"></param>
public sealed record GetVisionsQuery(VisionState? StateFilter) : IQuery<List<VisionDto>>;

public sealed class GetVisionsQueryHandler(IStrategicManagementDbContext strategicManagementDbContext) : IQueryHandler<GetVisionsQuery, List<VisionDto>>
{
    private readonly IStrategicManagementDbContext _strategicManagementDbContext = strategicManagementDbContext;

    public async Task<List<VisionDto>> Handle(GetVisionsQuery request, CancellationToken cancellationToken)
    {
        var query = _strategicManagementDbContext.Visions.AsQueryable();

        if (request.StateFilter.HasValue)
        {
            query = query.Where(st => st.State == request.StateFilter.Value);
        }

        return await query.ProjectToType<VisionDto>().ToListAsync(cancellationToken);
    }
}
