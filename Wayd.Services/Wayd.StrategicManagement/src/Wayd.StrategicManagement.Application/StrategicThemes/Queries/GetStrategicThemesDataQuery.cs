using Wayd.Common.Domain.Interfaces.StrategicManagement;

namespace Wayd.StrategicManagement.Application.StrategicThemes.Queries;

/// <summary>
/// Retrieves a list of StrategicThemes based on IStrategicThemeData.
/// </summary>
/// <param name="StateFilter"></param>
public sealed record GetStrategicThemesDataQuery() : IQuery<List<IStrategicThemeData>>;

public sealed class GetStrategicThemesDataQueryHandler(IStrategicManagementDbContext strategicManagementDbContext) : IQueryHandler<GetStrategicThemesDataQuery, List<IStrategicThemeData>>
{
    private readonly IStrategicManagementDbContext _strategicManagementDbContext = strategicManagementDbContext;

    public async Task<List<IStrategicThemeData>> Handle(GetStrategicThemesDataQuery request, CancellationToken cancellationToken)
    {
        var themes = await _strategicManagementDbContext.StrategicThemes
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return [.. themes.OfType<IStrategicThemeData>()];
    }
}
