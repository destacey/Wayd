using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.Common.Application.Dtos;

/// <summary>
/// A communication connection that is present, active, and has a valid configuration.
/// </summary>
public sealed record ActiveCommunicationConnectionSummaryDto
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required Connector Connector { get; init; }

    /// <summary>The connection's active switch. Inactive connections are excluded.</summary>
    public required bool IsActive { get; init; }

    /// <summary>True when the stored configuration is valid. Invalid connections are excluded.</summary>
    public required bool IsValidConfiguration { get; init; }
}
