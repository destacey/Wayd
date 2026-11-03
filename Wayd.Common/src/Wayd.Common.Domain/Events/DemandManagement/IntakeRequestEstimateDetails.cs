namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An intake request's estimate as one value: the size given, and the estimation scale version it was given
/// on, which never changes once published.
/// </summary>
/// <remarks>
/// The scale is named as well as identified, as it was called when the estimate was given, so the entry stays
/// readable after the scale is renamed or deleted.
/// </remarks>
/// <param name="Value">An estimate value of the version, such as <c>M</c> or <c>8</c>.</param>
/// <param name="ScaleVersionId">The estimation scale version the value belongs to.</param>
/// <param name="ScaleName">The scale's name when the estimate was given.</param>
/// <param name="ScaleVersionNumber">The version's number within its scale.</param>
public sealed record IntakeRequestEstimateDetails(string Value, Guid ScaleVersionId, string ScaleName, int ScaleVersionNumber);
