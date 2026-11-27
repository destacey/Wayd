using Wayd.Common.Domain.Models;

namespace Wayd.Common.Domain.Interfaces;

/// <summary>
/// A record that may correspond to a record in an outside system.
/// </summary>
public interface IHasExternalReference
{
    /// <summary>
    /// The outside record this one corresponds to, or null when it has none.
    /// </summary>
    ExternalReference? ExternalReference { get; }
}
