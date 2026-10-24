using System.Globalization;
using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Extensions;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.Programs;

/// <summary>
/// Who may read a program, by the rule the program endpoints apply: the View Programs permission, which
/// admits every program.
/// </summary>
/// <remarks>
/// If programs ever gain a per-record read rule, it belongs in <see cref="Readable"/> as well as in the
/// program queries — otherwise following would show a program its endpoints refuse. Programs record no
/// health, so none is ever unhealthy.
/// </remarks>
public sealed class ProgramReadRule(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ICurrentPrincipal currentPrincipal) : IRecordReadRule
{
    /// <summary>The permission the program read endpoints require.</summary>
    public static readonly string ViewPermission =
        ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Programs);

    /// <summary>The statuses of a program that has finished, whatever the outcome.</summary>
    private static readonly ProgramStatus[] ClosedStatuses =
    [
        .. Enum.GetValues<ProgramStatus>().Where(s => LifecycleCategories<ProgramStatus>.Of(s) is LifecycleCategory.Completed or LifecycleCategory.Canceled),
    ];

    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    /// <inheritdoc/>
    public string OwnerType => PpmFollowOwners.Program.Key;

    /// <inheritdoc/>
    /// <remarks>A search matches the name anywhere, or the key exactly.</remarks>
    public async Task<IQueryable<RecordFacts>?> Readable(string? search, CancellationToken cancellationToken)
    {
        if (!await _currentPrincipal.HasPermission(ViewPermission, cancellationToken))
        {
            return null;
        }

        var programs = _ppmDbContext.Programs.AsQueryable();

        if (search is not null)
        {
            programs = int.TryParse(search, NumberStyles.None, CultureInfo.InvariantCulture, out var key)
                ? programs.Where(p => p.Name.Contains(search) || p.Key == key)
                : programs.Where(p => p.Name.Contains(search));
        }

        return programs.Select(p => new RecordFacts
        {
            Id = p.Id,
            IsUnhealthy = false,
            IsClosed = ClosedStatuses.Contains(p.Status),
        });
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<Guid, RecordSummary>> Describe(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        var programs = await _ppmDbContext.Programs
            .Where(p => ids.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.Key,
                p.Name,
                p.Status,
                Start = p.DateRange != null ? p.DateRange.Start : (LocalDate?)null,
                End = p.DateRange != null ? p.DateRange.End : (LocalDate?)null,
                Leads = p.Roles
                    .Where(r => r.Role == ProgramRole.Owner || r.Role == ProgramRole.Manager)
                    .Select(r => new { r.Role, r.EmployeeId, r.Employee!.Name })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        return programs.ToDictionary(
            p => p.Id,
            p => new RecordSummary
            {
                Key = p.Key.ToString(CultureInfo.InvariantCulture),
                Name = p.Name,
                StatusName = p.Status.GetDisplayName(),
                Lifecycle = LifecycleCategories<ProgramStatus>.Of(p.Status),
                // Owners before managers, each by name.
                People = [.. p.Leads
                    .OrderBy(l => l.Role == ProgramRole.Owner ? 0 : 1)
                    .ThenBy(l => l.Name.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(l => new RecordPerson(l.Role.GetDisplayName(), l.Name.DisplayName, l.EmployeeId))],
                Start = p.Start,
                End = p.End,
            });
    }
}
