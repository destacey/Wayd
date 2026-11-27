using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.Common.Domain.Models;

/// <summary>
/// Names one record in an outside system. It never names a Wayd connection, so it stays the same when
/// connections to that system are added, replaced or removed.
/// </summary>
/// <param name="Connector">The kind of outside system.</param>
/// <param name="SystemId">
/// The outside system's own identifier for the instance the record lives in, such as an Azure DevOps
/// organization. Null when the record is unique across every instance, or not yet known.
/// </param>
/// <param name="ExternalId">The record's identifier in the outside system.</param>
public sealed record ExternalReference(Connector Connector, string? SystemId, string ExternalId);
