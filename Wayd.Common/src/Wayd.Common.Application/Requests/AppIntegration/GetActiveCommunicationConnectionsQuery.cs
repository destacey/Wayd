using Wayd.Common.Application.Dtos;

namespace Wayd.Common.Application.Requests.AppIntegration;

/// <summary>
/// Lists every connection whose connector declares Communication capability, that is active,
/// and whose configuration is valid.
/// </summary>
public sealed record GetActiveCommunicationConnectionsQuery()
    : IQuery<IReadOnlyList<ActiveCommunicationConnectionSummaryDto>>;
