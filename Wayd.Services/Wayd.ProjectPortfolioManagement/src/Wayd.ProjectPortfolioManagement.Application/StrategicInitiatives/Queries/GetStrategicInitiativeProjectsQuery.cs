using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Queries;

public sealed record GetStrategicInitiativeProjectsQuery : IQuery<List<ProjectListDto>?>
{
    public GetStrategicInitiativeProjectsQuery(IdOrKey strategicInitiativeIdOrKey)
    {
        StrategicInitiativeIdOrKeyFilter = strategicInitiativeIdOrKey.CreateFilter<StrategicInitiative>();
    }

    public Expression<Func<StrategicInitiative, bool>> StrategicInitiativeIdOrKeyFilter { get; }
}

public sealed class GetStrategicInitiativeProjectsQueryHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext, IDateTimeProvider dateTimeProvider, ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetStrategicInitiativeProjectsQuery, List<ProjectListDto>?>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = projectPortfolioManagementDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<List<ProjectListDto>?> Handle(GetStrategicInitiativeProjectsQuery request, CancellationToken cancellationToken)
    {
        var query = _ppmDbContext.StrategicInitiatives
            .Where(request.StrategicInitiativeIdOrKeyFilter);

        if (!await query.AnyAsync(cancellationToken))
        {
            return null;
        }

        var now = _dateTimeProvider.Now;
        var isPpmAdministrator = await _currentPrincipal.HasPermission(
            PpmAuthorizationExtensions.PpmAdministratorPermission, cancellationToken);
        var config = ProjectListDto.CreateTypeAdapterConfig(
            now, await _currentPrincipal.GetEmployeeId(cancellationToken), isPpmAdministrator);
        return await query
            .SelectMany(i => i.StrategicInitiativeProjects.Select(ip => ip.Project))
            .ProjectToType<ProjectListDto>(config)
            .ToListAsync(cancellationToken);
    }
}
