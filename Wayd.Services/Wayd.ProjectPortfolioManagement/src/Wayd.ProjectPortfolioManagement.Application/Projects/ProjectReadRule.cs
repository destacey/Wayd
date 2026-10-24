using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Authorization;
using Wayd.ProjectPortfolioManagement.Domain;

namespace Wayd.ProjectPortfolioManagement.Application.Projects;

/// <summary>
/// Who may read a project, by the rule the project endpoints apply: the View Projects
/// permission, which admits every project.
/// </summary>
/// <remarks>
/// If projects ever gain a per-record read rule, it belongs in <see cref="ReadableIds"/> as well as in
/// the project queries — otherwise following would show a project its endpoints refuse.
/// </remarks>
public sealed class ProjectReadRule(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ICurrentPrincipal currentPrincipal) : IRecordReadRule
{
    /// <summary>The permission the project read endpoints require.</summary>
    public static readonly string ViewPermission =
        ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Projects);

    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    /// <inheritdoc/>
    public string OwnerType => PpmFollowOwners.Project.Key;

    /// <inheritdoc/>
    public async Task<IQueryable<Guid>?> ReadableIds(CancellationToken cancellationToken) =>
        await _currentPrincipal.HasPermission(ViewPermission, cancellationToken)
            ? _ppmDbContext.Projects.Select(p => p.Id)
            : null;

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<Guid, RecordSummary>> Describe(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        var projects = await _ppmDbContext.Projects
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Key, p.Name })
            .ToListAsync(cancellationToken);

        return projects.ToDictionary(p => p.Id, p => new RecordSummary(p.Key.Value, p.Name));
    }
}
