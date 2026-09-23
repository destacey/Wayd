namespace Wayd.Work.Domain.Models.Forecasting;

/// <summary>
/// How often a dependency decided when its successor finished.
/// </summary>
/// <param name="ShareOfTrialsSettingFinish">
/// The share of trials (0 to 1) in which waiting for the predecessor made the successor finish
/// later than its own backlog position allowed. Null when the successor cannot be forecast.
/// </param>
public sealed record DependencyInfluence<TKey>(
    TKey Predecessor,
    TKey Successor,
    double? ShareOfTrialsSettingFinish) where TKey : notnull;
