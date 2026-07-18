using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Application.Programs.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.Programs.Queries;

public sealed record GetProgramsQuery(ProgramStatus[]? StatusFilter = null, IdOrKey? PortfolioIdOrKey = null) : IQuery<List<ProgramListDto>?>;

public sealed class GetProgramsQueryHandler(IProjectPortfolioManagementDbContext ppmDbContext)
    : IQueryHandler<GetProgramsQuery, List<ProgramListDto>?>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;

    public async Task<List<ProgramListDto>?> Handle(GetProgramsQuery request, CancellationToken cancellationToken)
    {
        var query = _ppmDbContext.Programs.AsQueryable();

        if (request.StatusFilter is { Length: > 0 })
        {
            query = query.Where(pp => request.StatusFilter.Contains(pp.Status));
        }

        if (request.PortfolioIdOrKey is not null)
        {
            // TODO: make this reusable
            Guid? portfolioId = request.PortfolioIdOrKey.IsId
                ? request.PortfolioIdOrKey.AsId
                : await _ppmDbContext.Portfolios
                    .Where(p => p.Key == request.PortfolioIdOrKey.AsKey)
                    .Select(p => (Guid?)p.Id)
                    .FirstOrDefaultAsync(cancellationToken);

            if (portfolioId is null)
            {
                return null;
            }

            query = query.Where(pp => pp.PortfolioId == portfolioId);
        }

        var programs = await query.ProjectToType<ProgramListDto>().ToListAsync(cancellationToken);
        return [.. programs.OrderBy(p => p.Name)];
    }
}