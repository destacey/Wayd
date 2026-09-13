using Wayd.Common.Application.Requests.StrategicManagement;
using Wayd.Common.Domain.Interfaces.StrategicManagement;

namespace Wayd.StrategicManagement.Application.StrategicThemes.Queries;

public sealed class GetStrategicThemeDataQueryHandler(IStrategicManagementDbContext strategicManagementDbContext)
    : IQueryHandler<GetStrategicThemeDataQuery, IStrategicThemeData?>
{
    private readonly IStrategicManagementDbContext _strategicManagementDbContext = strategicManagementDbContext;

    public async Task<IStrategicThemeData?> Handle(GetStrategicThemeDataQuery request, CancellationToken cancellationToken)
    {
        return await _strategicManagementDbContext.StrategicThemes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
    }
}
