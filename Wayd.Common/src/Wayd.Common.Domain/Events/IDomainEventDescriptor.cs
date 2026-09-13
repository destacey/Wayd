namespace Wayd.Common.Domain.Events;

/// <summary>
/// What an event type declares about itself, as opposed to what an occurrence of it carries.
/// </summary>
/// <remarks>
/// Static, so nothing here is part of a payload: the serializer never writes a static member, and a
/// descriptor answers the same for every instance. <see cref="DomainEvent{TSelf}"/> requires it, which is
/// what makes a missing declaration a compile error.
/// </remarks>
public interface IDomainEventDescriptor
{
    /// <summary>The kind of occurrence the activity log presents this event as.</summary>
    static abstract ActivityCategory ActivityCategory { get; }
}
