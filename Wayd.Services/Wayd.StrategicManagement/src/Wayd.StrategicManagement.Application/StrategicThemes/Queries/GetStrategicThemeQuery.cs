using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.StrategicManagement.Application.StrategicThemes.Dtos;
using Wayd.StrategicManagement.Domain.Models;

namespace Wayd.StrategicManagement.Application.StrategicThemes.Queries;

/// <summary>
/// Get a StrategicTheme by Id or Key
/// </summary>
public sealed record GetStrategicThemeQuery : IQuery<StrategicThemeDetailsDto?>
{
    public GetStrategicThemeQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<StrategicTheme>();
    }

    public Expression<Func<StrategicTheme, bool>> IdOrKeyFilter { get; }
}

public sealed class GetStrategicThemeQueryHandler(IStrategicManagementDbContext strategicManagementDbContext) : IQueryHandler<GetStrategicThemeQuery, StrategicThemeDetailsDto?>
{
    private readonly IStrategicManagementDbContext _strategicManagementDbContext = strategicManagementDbContext;

    public async Task<StrategicThemeDetailsDto?> Handle(GetStrategicThemeQuery request, CancellationToken cancellationToken)
    {
        return await _strategicManagementDbContext.StrategicThemes
            .Where(request.IdOrKeyFilter)
            .ProjectToType<StrategicThemeDetailsDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
