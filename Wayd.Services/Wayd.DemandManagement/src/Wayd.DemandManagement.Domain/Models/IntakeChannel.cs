using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;

namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// Where a request came from: business stakeholder, customer feedback, support, and so on. One list for the
/// whole company, from which each desk offers the channels that apply to it.
/// </summary>
/// <remarks>
/// A company-defined vocabulary referenced by id rather than a closed set, because each company sources
/// demand differently; the seeded list is a starting point. It is not the desk: a request raised by
/// support can be reviewed on any desk, and moving a request to another desk keeps its channel. One shared
/// list is what keeps channels comparable across desks.
/// <para>
/// Archived rather than deleted, because requests refer to it by id and keep it after it is archived.
/// </para>
/// </remarks>
public sealed class IntakeChannel : BaseAuditableEntity
{
    private IntakeChannel() { }

    private IntakeChannel(string name, string? description, int order)
    {
        Name = name;
        Description = description;
        Order = order;
    }

    /// <summary>
    /// What the company calls the channel (e.g. "Customer feedback").
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// Optional explanation of what belongs in the channel, shown to whoever picks one.
    /// </summary>
    public string? Description
    {
        get;
        private set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// Position in the list a requester chooses from, lowest first.
    /// </summary>
    public int Order { get; private set; }

    /// <summary>
    /// Whether new requests can be given this channel, and desks offer it. False once archived.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Renames the channel or changes what it says belongs in it. Every request carrying it reads the new
    /// name from then on.
    /// </summary>
    public Result UpdateDetails(string name, string? description, EventActor actor, Instant timestamp)
    {
        var previousName = Name;
        var previousDescription = Description;

        Name = name;
        Description = description;

        if (Name == previousName && Description == previousDescription)
            return Result.Success();

        AddDomainEvent(new IntakeChannelDetailsUpdatedEvent(Id, Name, Description, previousName, previousDescription,
            actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Moves the channel to another position in the list.
    /// </summary>
    public Result Reorder(int order, EventActor actor, Instant timestamp)
    {
        if (order == Order)
            return Result.Success();

        var fromOrder = Order;
        Order = order;

        AddDomainEvent(new IntakeChannelReorderedEvent(Id, fromOrder, Order, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Takes the channel out of use: no new request can be given it and no desk offers it. Requests already
    /// carrying it keep it, and desks that chose it offer it again if it is restored.
    /// </summary>
    public Result Archive(EventActor actor, Instant timestamp)
    {
        if (!IsActive)
            return Result.Success();

        IsActive = false;
        AddDomainEvent(new IntakeChannelArchivedEvent(Id, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Brings an archived channel back into use.
    /// </summary>
    public Result Restore(EventActor actor, Instant timestamp)
    {
        if (IsActive)
            return Result.Success();

        IsActive = true;
        AddDomainEvent(new IntakeChannelRestoredEvent(Id, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Defines a channel.
    /// </summary>
    public static IntakeChannel Create(string name, string? description, int order, EventActor actor, Instant timestamp)
    {
        var channel = new IntakeChannel(name, description, order);

        channel.AddDomainEvent(new IntakeChannelCreatedEvent(channel.Id, channel.Name, channel.Description,
            channel.Order, actor, timestamp));

        return channel;
    }
}
