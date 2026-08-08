using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Application.Programs.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Programs.Queries;

public sealed record GetProgramQuery : IQuery<ProgramDetailsDto?>
{
    public GetProgramQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Program>();
    }

    public Expression<Func<Program, bool>> IdOrKeyFilter { get; }
}

public sealed class GetProgramQueryHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetProgramQuery, ProgramDetailsDto?>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<ProgramDetailsDto?> Handle(GetProgramQuery request, CancellationToken cancellationToken)
    {
        // A per-request config is needed so CanManageProgram reflects the caller; the global mapping has
        // no actor and would always report false.
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        var isPpmAdministrator = await _currentPrincipal.HasPermission(
            PpmAuthorizationExtensions.PpmAdministratorPermission, cancellationToken);

        var config = ProgramDetailsDto.CreateTypeAdapterConfig(employeeId, isPpmAdministrator);

        return await _ppmDbContext.Programs
            .Where(request.IdOrKeyFilter)
            .ProjectToType<ProgramDetailsDto>(config)
            .FirstOrDefaultAsync(cancellationToken);
    }
}