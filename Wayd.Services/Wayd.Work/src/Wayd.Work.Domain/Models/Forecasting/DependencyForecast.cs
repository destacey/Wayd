namespace Wayd.Work.Domain.Models.Forecasting;

public sealed class DependencyForecast<TKey> where TKey : notnull
{
    internal DependencyForecast(
        IReadOnlyDictionary<TKey, DependencyAdjustedForecast<TKey>> items,
        IReadOnlyList<DependencyInfluence<TKey>> dependencies,
        IReadOnlyList<ForecastDependency<TKey>> ignoredDependencies)
    {
        Items = items;
        Dependencies = dependencies;
        IgnoredDependencies = ignoredDependencies;
    }

    public IReadOnlyDictionary<TKey, DependencyAdjustedForecast<TKey>> Items { get; }

    public IReadOnlyList<DependencyInfluence<TKey>> Dependencies { get; }

    /// <summary>
    /// Dependencies left out because each closed a cycle.
    /// </summary>
    public IReadOnlyList<ForecastDependency<TKey>> IgnoredDependencies { get; }
}
