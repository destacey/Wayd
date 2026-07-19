using Wolverine.Persistence.Durability;

namespace Wayd.Web.Api.Models.Admin.Messaging;

/// <summary>
/// A single dead-lettered message envelope: what failed, where, and why.
/// </summary>
public class DeadLetterMessageResponse
{
    public Guid Id { get; set; }
    public string MessageType { get; set; } = default!;

    /// <summary>The listener/queue URI the message was received at (e.g. local://durable/).</summary>
    public string? ReceivedAt { get; set; }

    /// <summary>The service that originally sent the message.</summary>
    public string? Source { get; set; }

    public string? ExceptionType { get; set; }
    public string? ExceptionMessage { get; set; }
    public DateTimeOffset SentAt { get; set; }

    /// <summary>Whether the durability agent is set to move this envelope back to incoming for another attempt.</summary>
    public bool Replayable { get; set; }

    public int Attempts { get; set; }

    internal static DeadLetterMessageResponse From(DeadLetterEnvelope envelope)
    {
        return new DeadLetterMessageResponse
        {
            Id = envelope.Id,
            MessageType = envelope.MessageType,
            ReceivedAt = envelope.ReceivedAt,
            Source = envelope.Source,
            ExceptionType = envelope.ExceptionType,
            ExceptionMessage = envelope.ExceptionMessage,
            SentAt = envelope.SentAt,
            Replayable = envelope.Replayable,
            Attempts = envelope.Envelope.Attempts,
        };
    }
}
