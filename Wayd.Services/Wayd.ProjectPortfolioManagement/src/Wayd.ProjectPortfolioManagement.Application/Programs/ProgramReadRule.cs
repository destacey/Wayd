using System.Globalization;
using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Authorization;
using Wayd.ProjectPortfolioManagement.Domain;

namespace Wayd.ProjectPortfolioManagement.Application.Programs;

/// <summary>
/// Who may read a program, by the rule the program endpoints apply: the View Programs
/// permission, which admits every program.
/// </summary>
/// <remarks>
/// If programs ever gain a per-record read rule, it belongs in <see cref="ReadableIds"/> as well as in
/// the program queries — otherwise following would show a program its endpoints refuse.
/// </remarks>
public sealed class ProgramReadRule(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ICurrentPrincipal currentPrincipal) : IRecordReadRule
{
    /// <summary>The permission the program read endpoints require.</summary>
    public static readonly string ViewPermission =
        ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Programs);

    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    /// <inheritdoc/>
    public string OwnerType => PpmFollowOwners.Program.Key;

    /// <inheritdoc/>
    public async Task<IQueryable<Guid>?> ReadableIds(CancellationToken cancellationToken) =>
        await _currentPrincipal.HasPermission(ViewPermission, cancellationToken)
            ? _ppmDbContext.Programs.Select(p => p.Id)
            : null;

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<Guid, RecordSummary>> Describe(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        var programs = await _ppmDbContext.Programs
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Key, p.Name })
            .ToListAsync(cancellationToken);

        return programs.ToDictionary(
            p => p.Id,
            p => new RecordSummary(p.Key.ToString(CultureInfo.InvariantCulture), p.Name));
    }
}
