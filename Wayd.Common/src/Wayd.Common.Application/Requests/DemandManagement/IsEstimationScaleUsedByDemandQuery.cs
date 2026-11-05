namespace Wayd.Common.Application.Requests.DemandManagement;

/// <summary>
/// Whether Demand Management uses an estimation scale: an intake desk pins one of its versions, or an intake
/// request's estimate was given on one. Asked by Planning before it deletes the scale.
/// </summary>
public sealed record IsEstimationScaleUsedByDemandQuery(Guid EstimationScaleId) : IQuery<bool>;
