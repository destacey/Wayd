using Wayd.Common.Application.Dtos;

namespace Wayd.Common.Application.Requests.AppIntegration;

/// <summary>
/// Lists every active connection whose connector declares Communication capability.
/// </summary>
public sealed record GetActiveCommunicationConnectionsQuery()
    : IQuery<IReadOnlyList<ActiveCommunicationConnectionSummaryDto>>;
