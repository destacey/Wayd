namespace Wayd.Common.Application.Dtos;

using Wayd.Common.Domain.Enums.AppIntegrations;

/// <summary>
/// An active connection whose connector declares the Communication capability.
/// </summary>
public sealed record ActiveCommunicationConnectionSummaryDto
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required Connector Connector { get; init; }
}
