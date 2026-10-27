namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// A company channel a desk chose to offer, while the desk offers only the channels it chose.
/// </summary>
/// <remarks>
/// Kept when the channel is archived, so the desk offers it again if it is restored; an archived channel is
/// offered by no desk in the meantime.
/// </remarks>
public sealed class IntakeDeskChannel
{
    private IntakeDeskChannel() { }

    internal IntakeDeskChannel(Guid deskId, Guid channelId)
    {
        DeskId = deskId;
        ChannelId = channelId;
    }

    /// <summary>The desk offering the channel.</summary>
    public Guid DeskId { get; private init; }

    /// <summary>The company channel the desk offers.</summary>
    public Guid ChannelId { get; private init; }
}
