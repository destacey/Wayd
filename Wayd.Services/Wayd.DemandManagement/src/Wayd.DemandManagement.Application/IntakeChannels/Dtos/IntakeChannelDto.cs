namespace Wayd.DemandManagement.Application.IntakeChannels.Dtos;

/// <summary>
/// An intake channel: one of the places requests come from.
/// </summary>
public sealed record IntakeChannelDto
{
    /// <summary>The channel's id, which a request refers to.</summary>
    public Guid Id { get; init; }

    /// <summary>What the company calls the channel.</summary>
    public required string Name { get; init; }

    /// <summary>What belongs in the channel, when described.</summary>
    public string? Description { get; init; }

    /// <summary>Position in the list a requester chooses from, lowest first.</summary>
    public int Order { get; init; }

    /// <summary>Whether new requests can be given this channel.</summary>
    public bool IsActive { get; init; }
}
