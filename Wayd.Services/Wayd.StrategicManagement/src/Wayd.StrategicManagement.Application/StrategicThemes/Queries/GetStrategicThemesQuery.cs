using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.StrategicManagement.Application.StrategicThemes.Dtos;

namespace Wayd.StrategicManagement.Application.StrategicThemes.Queries;

/// <summary>
/// Retrieves a list of StrategicThemes based on the provided filter.  Returns all StrategicThemes if no filter is provided.
/// </summary>
/// <param name="StateFilter"></param>
public sealed record GetStrategicThemesQuery(StrategicThemeState[]? StateFilter = null) : IQuery<List<StrategicThemeListDto>>;

public sealed class GetStrategicThemesQueryHandler(IStrategicManagementDbContext strategicManagementDbContext) : IQueryHandler<GetStrategicThemesQuery, List<StrategicThemeListDto>>
{
    private readonly IStrategicManagementDbContext _strategicManagementDbContext = strategicManagementDbContext;

    public async Task<List<StrategicThemeListDto>> Handle(GetStrategicThemesQuery request, CancellationToken cancellationToken)
    {
        var query = _strategicManagementDbContext.StrategicThemes.AsQueryable();

        if (request.StateFilter is { Length: > 0 })
        {
            query = query.Where(st => request.StateFilter.Contains(st.State));
        }

        return await query.ProjectToType<StrategicThemeListDto>().ToListAsync(cancellationToken);
    }
}
