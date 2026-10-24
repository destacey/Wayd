namespace Wayd.DemandManagement.Application.IntakeRequests.Dtos;

/// <summary>
/// The upstream record an intake request arrived from.
/// </summary>
public sealed record IntakeRequestOriginDto
{
    /// <summary>The system the request was raised in.</summary>
    public required string SourceSystem { get; init; }

    /// <summary>The request's id in that system.</summary>
    public required string ExternalId { get; init; }

    /// <summary>A link to the request in that system, when it has one.</summary>
    public string? Url { get; init; }
}
