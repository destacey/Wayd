namespace Wayd.DemandManagement.Application.IntakeChannels.Dtos;

/// <summary>
/// A channel as a request refers to it.
/// </summary>
public sealed record IntakeChannelNavigationDto
{
    /// <summary>The channel's id.</summary>
    public Guid Id { get; init; }

    /// <summary>What the company calls the channel.</summary>
    public required string Name { get; init; }
}
