using Wayd.Common.Domain.Models;

namespace Wayd.Common.Domain.Interfaces;

/// <summary>
/// A record that states who writes it — Wayd users, a sync, or Wayd itself — and which outside record
/// it corresponds to. Its entity configuration must map <see cref="OwnershipInfo"/> with
/// <c>ConfigureOwnershipInfo</c>, so every such record has the same columns.
/// </summary>
public interface IHasOwnershipInfo : IHasExternalReference
{
    /// <summary>
    /// Who writes the record and the outside record it corresponds to.
    /// </summary>
    OwnershipInfo OwnershipInfo { get; }

    ExternalReference? IHasExternalReference.ExternalReference => OwnershipInfo.GetExternalReference();
}
