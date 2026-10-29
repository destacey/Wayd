namespace Wayd.Common.Domain.Events.Discussions;

/// <summary>
/// Marks every event raised by a discussion, and names the record the conversation is about.
/// </summary>
/// <remarks>
/// A module's handlers react only to conversations on their own records, so every discussion event carries the
/// channel type and owner, letting a handler filter without loading the channel. No discussion event carries a
/// subject or a body: an internal note must never reach an event payload, the outbox or a log line, and a reply
/// is its own record, read through its conversation.
/// </remarks>
public interface IDiscussionEvent : IEvent, IAggregateEvent
{
    /// <summary>The registered channel type of the conversation, such as <c>demand.intake-request</c>.</summary>
    string ChannelType { get; }

    /// <summary>The record the conversation is about, or null for a conversation with no record.</summary>
    Guid? OwnerId { get; }
}
