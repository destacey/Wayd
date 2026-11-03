using Wayd.Common.Domain.Events.Planning.EstimationScales;

namespace Wayd.Common.Domain.Models.Planning.EstimationScales;

/// <summary>
/// An estimation scale as Planning holds it at the moment it is read, for a module that keeps a copy of the
/// scales and needs to build or repair one.
/// </summary>
/// <param name="Versions">Every version the scale has published, oldest first.</param>
public sealed record EstimationScaleSnapshot(
    Guid Id,
    int Key,
    string Name,
    bool IsActive,
    IReadOnlyList<EstimationScaleVersionValues> Versions);
