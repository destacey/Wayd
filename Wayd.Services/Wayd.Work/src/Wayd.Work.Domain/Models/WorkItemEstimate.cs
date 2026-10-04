using Wayd.Common.Domain.Enums.Organization;

namespace Wayd.Work.Domain.Models;

/// <summary>
/// How much work an item counts as under a team's sizing method: the one rule every metric that sums
/// estimates reads through.
/// </summary>
public static class WorkItemEstimate
{
    /// <summary>
    /// The item's value in the estimate <paramref name="sizingMethod"/> names, or 1 under
    /// <see cref="SizingMethod.Count"/>. Null when the item has no value in that estimate: another estimate
    /// never stands in, and 0 is an estimate rather than a missing one.
    /// </summary>
    public static double? Of(SizingMethod sizingMethod, double? storyPoints, double? effort, double? size) =>
        sizingMethod switch
        {
            SizingMethod.StoryPoints => storyPoints,
            SizingMethod.Effort => effort,
            SizingMethod.Size => size,
            SizingMethod.Count => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(sizingMethod), sizingMethod, "Unknown sizing method."),
        };

    /// <inheritdoc cref="Of(SizingMethod, double?, double?, double?)"/>
    public static double? Of(SizingMethod sizingMethod, WorkItem item) =>
        Of(sizingMethod, item.StoryPoints, item.Effort, item.Size);
}
