using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Common;

/// <summary>
/// Checks the strategic themes a command asks a project or program to carry. Each is a foreign key to PPM's copy
/// of the theme, so an unknown one would otherwise be refused only when the change is saved, as an exception
/// rather than an answer the caller can act on.
/// </summary>
internal static class PpmStrategicThemes
{
    /// <summary>The failure for a theme that is not in PPM's copy of the themes.</summary>
    public const string NotFoundError = "One or more Strategic Themes not found.";

    /// <summary>The failure for a theme the record may not take on (see <see cref="StrategicTheme.CanBeAttached"/>).</summary>
    public const string NotActiveError = "One or more Strategic Themes are not active.";

    /// <summary>
    /// Fails unless every requested theme exists and the record may carry it: a theme in
    /// <paramref name="alreadyAttached"/> is kept whatever its state, and any other must be Active.
    /// </summary>
    /// <param name="alreadyAttached">The themes the record carries before the change; null for a new record.</param>
    public static async Task<Result> EnsureThemesAttachable(
        this IProjectPortfolioManagementDbContext dbContext,
        IEnumerable<Guid> strategicThemeIds,
        IReadOnlySet<Guid>? alreadyAttached,
        CancellationToken cancellationToken)
    {
        var requested = strategicThemeIds.Distinct().ToList();
        if (requested.Count == 0)
            return Result.Success();

        var themes = await dbContext.PpmStrategicThemes
            .AsNoTracking()
            .Where(t => requested.Contains(t.Id))
            .ToListAsync(cancellationToken);

        if (themes.Count != requested.Count)
            return Result.Failure(NotFoundError);

        return themes.All(t => t.CanBeAttached(alreadyAttached?.Contains(t.Id) ?? false))
            ? Result.Success()
            : Result.Failure(NotActiveError);
    }
}
