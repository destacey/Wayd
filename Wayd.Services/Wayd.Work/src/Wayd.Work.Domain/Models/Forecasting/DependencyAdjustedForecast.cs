namespace Wayd.Work.Domain.Models.Forecasting;

/// <summary>
/// An item's completion forecast once its predecessors are accounted for.
/// </summary>
/// <param name="Forecast">
/// Null when the item cannot be forecast: it has no forecast of its own, or it is
/// <paramref name="BlockedBy"/> a predecessor that has none.
/// </param>
/// <param name="BlockedBy">
/// The predecessors, direct or further up the chain, that have no forecast of their own.
/// </param>
public sealed record DependencyAdjustedForecast<TKey>(
    TKey Item,
    CompletionForecast? Forecast,
    IReadOnlyList<TKey> BlockedBy) where TKey : notnull;
