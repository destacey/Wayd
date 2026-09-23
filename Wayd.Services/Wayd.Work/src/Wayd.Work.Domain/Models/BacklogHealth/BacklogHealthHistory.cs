using Ardalis.GuardClauses;
using NodaTime;

namespace Wayd.Work.Domain.Models.BacklogHealth;

/// <summary>
/// A work item the team completed in the lookback window.
/// </summary>
public sealed record BacklogHealthCompletion(Instant? Activated, Instant Done, double? StoryPoints);

/// <summary>
/// What the team completed and took on over the lookback window.
/// </summary>
public sealed record BacklogHealthHistory
{
    public BacklogHealthHistory(int lookbackDays, IReadOnlyList<BacklogHealthCompletion> completions, int itemsCreated)
    {
        LookbackDays = Guard.Against.NegativeOrZero(lookbackDays);
        Completions = Guard.Against.Null(completions);
        ItemsCreated = Guard.Against.Negative(itemsCreated);
    }

    public int LookbackDays { get; }

    public IReadOnlyList<BacklogHealthCompletion> Completions { get; }

    public int ItemsCreated { get; }
}
