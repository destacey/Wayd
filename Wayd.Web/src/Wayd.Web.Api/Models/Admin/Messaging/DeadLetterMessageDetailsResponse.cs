using System.Text;
using Wolverine.Persistence.Durability;

namespace Wayd.Web.Api.Models.Admin.Messaging;

/// <summary>
/// Full detail for a single dead-lettered message, including the serialized message body
/// (System.Text.Json per the Wolverine host configuration, so it renders as JSON).
/// </summary>
public class DeadLetterMessageDetailsResponse : DeadLetterMessageResponse
{
    public string? Body { get; set; }
    public string? ContentType { get; set; }
    public string? CorrelationId { get; set; }
    public string? Destination { get; set; }
    public DateTimeOffset? ExecutionTime { get; set; }

    internal static DeadLetterMessageDetailsResponse From(DeadLetterEnvelope envelope, DeadLetterMessageResponse summary)
    {
        return new DeadLetterMessageDetailsResponse
        {
            Id = summary.Id,
            MessageType = summary.MessageType,
            ReceivedAt = summary.ReceivedAt,
            Source = summary.Source,
            ExceptionType = summary.ExceptionType,
            ExceptionMessage = summary.ExceptionMessage,
            SentAt = summary.SentAt,
            Replayable = summary.Replayable,
            Attempts = summary.Attempts,
            Body = envelope.Envelope.Data is { Length: > 0 } data ? Encoding.UTF8.GetString(data) : null,
            ContentType = envelope.Envelope.ContentType,
            CorrelationId = envelope.Envelope.CorrelationId,
            Destination = envelope.Envelope.Destination?.ToString(),
            ExecutionTime = envelope.ExecutionTime,
        };
    }
}
