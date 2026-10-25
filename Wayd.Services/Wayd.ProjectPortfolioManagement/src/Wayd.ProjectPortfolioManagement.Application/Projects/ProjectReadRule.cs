using System.Text.RegularExpressions;
using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Extensions;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.Projects;

/// <summary>
/// Who may read a project, by the rule the project endpoints apply: the View Projects permission, which
/// admits every project.
/// </summary>
/// <remarks>
/// If projects ever gain a per-record read rule, it belongs in <see cref="Readable"/> as well as in the
/// project queries — otherwise following would show a project its endpoints refuse.
/// </remarks>
public sealed class ProjectReadRule(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider) : IRecordReadRule
{
    /// <summary>The permission the project read endpoints require.</summary>
    public static readonly string ViewPermission =
        ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Projects);

    /// <summary>The statuses of a project that has finished, whatever the outcome.</summary>
    private static readonly ProjectStatus[] ClosedStatuses =
    [
        .. Enum.GetValues<ProjectStatus>().Where(s => LifecycleCategories<ProjectStatus>.Of(s) is LifecycleCategory.Completed or LifecycleCategory.Canceled),
    ];

    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    /// <inheritdoc/>
    public string OwnerType => PpmFollowOwners.Project.Key;

    /// <inheritdoc/>
    public string ActivityAggregateType => "Project";

    /// <inheritdoc/>
    /// <remarks>
    /// A search matches the name anywhere, or the key exactly: the key is a converted value, which SQL can
    /// compare but not search within.
    /// </remarks>
    public async Task<IQueryable<RecordFacts>?> Readable(string? search, CancellationToken cancellationToken)
    {
        if (!await _currentPrincipal.HasPermission(ViewPermission, cancellationToken))
        {
            return null;
        }

        var now = _dateTimeProvider.Now;
        var projects = _ppmDbContext.Projects.AsQueryable();

        if (search is not null)
        {
            // Upper-cased invariantly before ProjectKey sees it: its own culture-sensitive ToUpper would turn
            // "pi" into "Pİ" under tr-TR and throw on a key that just passed the pattern.
            var upper = search.ToUpperInvariant();
            var key = Regex.IsMatch(upper, ProjectKey.Regex) ? new ProjectKey(upper) : null;
            projects = key is null
                ? projects.Where(p => p.Name.Contains(search))
                : projects.Where(p => p.Name.Contains(search) || p.Key == key);
        }

        return projects.Select(p => new RecordFacts
        {
            Id = p.Id,
            IsUnhealthy = p.HealthChecks
                .Where(h => h.Expiration > now)
                .OrderByDescending(h => h.ReportedOn)
                .Select(h => h.Status)
                .FirstOrDefault() == HealthStatus.Unhealthy,
            IsClosed = ClosedStatuses.Contains(p.Status),
        });
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<Guid, RecordSummary>> Describe(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.Now;
        var projects = await _ppmDbContext.Projects
            .Where(p => ids.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.Key,
                p.Name,
                p.Status,
                Start = p.DateRange != null ? p.DateRange.Start : (LocalDate?)null,
                End = p.DateRange != null ? p.DateRange.End : (LocalDate?)null,
                Health = p.HealthChecks
                    .Where(h => h.Expiration > now)
                    .OrderByDescending(h => h.ReportedOn)
                    .Select(h => new { h.Id, h.Status })
                    .FirstOrDefault(),
                Leads = p.Roles
                    .Where(r => r.Role == ProjectRole.Owner || r.Role == ProjectRole.Manager)
                    .Select(r => new { r.Role, r.EmployeeId, r.Employee!.Name })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        return projects.ToDictionary(
            p => p.Id,
            p => new RecordSummary
            {
                Key = p.Key.Value,
                Name = p.Name,
                StatusName = p.Status.GetDisplayName(),
                Lifecycle = LifecycleCategories<ProjectStatus>.Of(p.Status),
                Health = p.Health?.Status,
                HealthCheckId = p.Health?.Id,
                // Owners before managers, each by name.
                People = [.. p.Leads
                    .OrderBy(l => l.Role == ProjectRole.Owner ? 0 : 1)
                    .ThenBy(l => l.Name.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(l => new RecordPerson(l.Role.GetDisplayName(), l.Name.DisplayName, l.EmployeeId))],
                Start = p.Start,
                End = p.End,
            });
    }
}
