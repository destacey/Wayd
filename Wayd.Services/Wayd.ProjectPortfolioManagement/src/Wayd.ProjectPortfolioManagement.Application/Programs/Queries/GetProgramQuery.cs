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

public sealed class GetProgramQueryHandler(IProjectPortfolioManagementDbContext ppmDbContext)
    : IQueryHandler<GetProgramQuery, ProgramDetailsDto?>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;

    public async Task<ProgramDetailsDto?> Handle(GetProgramQuery request, CancellationToken cancellationToken)
    {
        return await _ppmDbContext.Programs
            .Where(request.IdOrKeyFilter)
            .ProjectToType<ProgramDetailsDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}