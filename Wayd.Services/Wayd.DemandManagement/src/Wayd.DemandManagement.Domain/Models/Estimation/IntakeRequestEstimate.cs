using Wayd.Common.Domain.Events.DemandManagement;

namespace Wayd.DemandManagement.Domain.Models.Estimation;

/// <summary>
/// A rough order of magnitude of the effort an intake request asks for: a value of an estimation scale version.
/// A decision aid for the desk, never a delivery commitment, and separate from the estimates delivery records on
/// work items.
/// </summary>
/// <remarks>
/// The value and its version are one value, because a size means nothing without the scale it was given on. A
/// stored estimate is never checked again: the version it names never changes.
/// </remarks>
public sealed record IntakeRequestEstimate
{
    private IntakeRequestEstimate() { }

    internal IntakeRequestEstimate(string value, Guid scaleVersionId, int position, string scaleName, int scaleVersionNumber)
    {
        Value = value;
        ScaleVersionId = scaleVersionId;
        Position = position;
        ScaleName = scaleName;
        ScaleVersionNumber = scaleVersionNumber;
    }

    /// <summary>The estimate value given, such as <c>M</c> or <c>8</c>.</summary>
    public string Value { get; private init; } = default!;

    /// <summary>The estimation scale version <see cref="Value"/> belongs to.</summary>
    public Guid ScaleVersionId { get; private init; }

    /// <summary>
    /// Where <see cref="Value"/> sits among its version's values, from 0, which is what estimates sort by: <c>XL</c>
    /// after <c>S</c>, <c>13</c> after <c>8</c>. Kept rather than looked up because the version never changes.
    /// </summary>
    public int Position { get; private init; }

    /// <summary>
    /// The scale's name when the estimate was given, frozen so the change that later replaces the estimate can
    /// name it even after the scale is renamed or deleted.
    /// </summary>
    public string ScaleName { get; private init; } = default!;

    /// <summary>The number of the version <see cref="ScaleVersionId"/> within its scale.</summary>
    public int ScaleVersionNumber { get; private init; }

    /// <summary>The estimate as an event carries it.</summary>
    public IntakeRequestEstimateDetails ToDetails() => new(Value, ScaleVersionId, ScaleName, ScaleVersionNumber);
}
