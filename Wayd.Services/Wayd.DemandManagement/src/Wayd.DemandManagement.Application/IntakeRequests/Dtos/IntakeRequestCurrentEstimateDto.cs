namespace Wayd.DemandManagement.Application.IntakeRequests.Dtos;

/// <summary>
/// A request's estimate, as the requests list and board sort and filter on it. Substance tier, which is why it is
/// returned beside the summaries rather than on them.
/// </summary>
public sealed record IntakeRequestCurrentEstimateDto
{
    /// <summary>The request estimated.</summary>
    public Guid RequestId { get; init; }

    /// <summary>The estimate.</summary>
    public required IntakeRequestEstimateDto Estimate { get; init; }
}
