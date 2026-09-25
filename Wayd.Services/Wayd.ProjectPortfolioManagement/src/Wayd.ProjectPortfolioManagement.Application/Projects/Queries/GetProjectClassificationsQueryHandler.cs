using Wayd.Common.Application.Requests.ProjectPortfolioManagement;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Queries;

public sealed class GetProjectClassificationsQueryHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext)
    : IQueryHandler<GetProjectClassificationsQuery, List<ProjectClassification>>
{
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;

    public async Task<List<ProjectClassification>> Handle(GetProjectClassificationsQuery request, CancellationToken cancellationToken)
    {
        if (request.ProjectIds.Count == 0)
            return [];

        var projectIds = request.ProjectIds.Distinct().ToList();

        // Portfolios, programs and themes are read by id rather than through navigations, which the
        // in-memory fakes leave unset.
        var projects = await _projectPortfolioManagementDbContext.Projects
            .Where(p => projectIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.Key,
                p.Name,
                p.PortfolioId,
                p.ProgramId,
                ThemeIds = p.StrategicThemeTags.Select(t => t.StrategicThemeId).ToList(),
            })
            .ToListAsync(cancellationToken);

        if (projects.Count == 0)
            return [];

        var portfolioIds = projects.Select(p => p.PortfolioId).Distinct().ToList();
        var portfolios = (await _projectPortfolioManagementDbContext.Portfolios
                .Where(p => portfolioIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Key, p.Name })
                .ToListAsync(cancellationToken))
            .ToDictionary(p => p.Id, p => new PpmRecordReference(p.Id, p.Key, p.Name));

        var programIds = projects.Where(p => p.ProgramId.HasValue).Select(p => p.ProgramId!.Value).Distinct().ToList();
        var programs = (await _projectPortfolioManagementDbContext.Programs
                .Where(p => programIds.Contains(p.Id))
                .Select(p => new
                {
                    p.Id,
                    p.Key,
                    p.Name,
                    ThemeIds = p.StrategicThemeTags.Select(t => t.StrategicThemeId).ToList(),
                })
                .ToListAsync(cancellationToken))
            .ToDictionary(p => p.Id);

        var themeIds = projects.SelectMany(p => p.ThemeIds)
            .Concat(programs.Values.SelectMany(p => p.ThemeIds))
            .Distinct()
            .ToList();
        var themes = (await _projectPortfolioManagementDbContext.PpmStrategicThemes
                .Where(t => themeIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Key, t.Name })
                .ToListAsync(cancellationToken))
            .ToDictionary(t => t.Id, t => new PpmRecordReference(t.Id, t.Key, t.Name));

        return [.. projects
            .Where(p => portfolios.ContainsKey(p.PortfolioId))
            .Select(p =>
            {
                var program = p.ProgramId is { } programId && programs.TryGetValue(programId, out var found) ? found : null;
                var themesFromProgram = p.ThemeIds.Count == 0 && program is { ThemeIds.Count: > 0 };
                var effectiveThemeIds = themesFromProgram ? program!.ThemeIds : p.ThemeIds;

                return new ProjectClassification(
                    p.Id,
                    p.Key.Value,
                    p.Name,
                    portfolios[p.PortfolioId],
                    program is null ? null : new PpmRecordReference(program.Id, program.Key, program.Name),
                    [.. effectiveThemeIds.Where(themes.ContainsKey).Select(id => themes[id]).OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)],
                    themesFromProgram);
            })];
    }
}
