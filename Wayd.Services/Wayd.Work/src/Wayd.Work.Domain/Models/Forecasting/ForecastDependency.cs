namespace Wayd.Work.Domain.Models.Forecasting;

/// <summary>
/// <paramref name="Successor"/> cannot finish before <paramref name="Predecessor"/> does.
/// </summary>
public sealed record ForecastDependency<TKey>(TKey Predecessor, TKey Successor) where TKey : notnull;
