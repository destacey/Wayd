using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.StrategicManagement.Application.StrategicThemes.Dtos;

namespace Wayd.StrategicManagement.Application.StrategicThemes.Queries;

public sealed record GetStrategicThemeOptionsQuery(bool? IncludeArchived) : IQuery<List<StrategicThemeOptionDto>>;

public sealed class GetStrategicThemeOptionsQueryHandler(IStrategicManagementDbContext strategicManagementDbContext) : IQueryHandler<GetStrategicThemeOptionsQuery, List<StrategicThemeOptionDto>>
{
    private readonly IStrategicManagementDbContext _strategicManagementDbContext = strategicManagementDbContext;

    public async Task<List<StrategicThemeOptionDto>> Handle(GetStrategicThemeOptionsQuery request, CancellationToken cancellationToken)
    {
        List<StrategicThemeState> statusFilter = request.IncludeArchived ?? false
            ? [StrategicThemeState.Active, StrategicThemeState.Archived]
            : [StrategicThemeState.Active];

        var themes = await _strategicManagementDbContext.StrategicThemes
            .Where(t => statusFilter.Contains(t.State))
            .ProjectToType<StrategicThemeOptionDto>()
            .ToListAsync(cancellationToken);

        return [.. themes.OrderBy(p => p.Name)];
    }
}
