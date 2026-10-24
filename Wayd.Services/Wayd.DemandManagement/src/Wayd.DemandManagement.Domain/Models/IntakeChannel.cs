using Ardalis.GuardClauses;

namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// Where a request came from: business stakeholder, customer feedback, support, and so on. Every request
/// carries one.
/// </summary>
/// <remarks>
/// A company-defined vocabulary referenced by id rather than a closed set, because each company sources
/// demand differently; the seeded list is a starting point. It is not the desk: a request raised by
/// support can be reviewed on any desk. Seed-and-read only for now; administering channels arrives with
/// the tag axes, which are the same kind of vocabulary on the same settings screen.
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
    /// Whether new requests can be given this channel.
    /// </summary>
    /// <remarks>
    /// Archived rather than deleted, because requests already carrying it still resolve their channel.
    /// </remarks>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Defines a channel.
    /// </summary>
    public static IntakeChannel Create(string name, string? description, int order) =>
        new(name, description, order);
}
