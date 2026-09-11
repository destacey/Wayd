using Wayd.Common.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Domain.Enums;

/// <summary>
/// The <see cref="LifecycleCategory"/> each value of a PPM status enum belongs to.
/// </summary>
/// <remarks>
/// Read from each status's <c>Display.GroupName</c> rather than a second table, so the categories the
/// status queries already surface and the ones events carry cannot disagree. Generic over the status enum
/// for the same reason: one implementation means projects, programs and portfolios cannot drift apart.
/// Built once per status type, so callers pay no reflection.
/// </remarks>
public static class LifecycleCategories<TStatus> where TStatus : struct, Enum
{
    private static readonly IReadOnlyDictionary<TStatus, LifecycleCategory> Categories =
        Enum.GetValues<TStatus>()
            .Select(status => (status, groupName: status.GetDisplayGroupName()))
            .Where(x => Enum.IsDefined(typeof(LifecycleCategory), x.groupName ?? string.Empty))
            .ToDictionary(x => x.status, x => Enum.Parse<LifecycleCategory>(x.groupName!));

    public static LifecycleCategory Of(TStatus status) =>
        Categories.TryGetValue(status, out var category)
            ? category
            : throw new InvalidOperationException(
                $"{typeof(TStatus).Name}.{status} declares no lifecycle category. Every status needs a Display GroupName.");
}
