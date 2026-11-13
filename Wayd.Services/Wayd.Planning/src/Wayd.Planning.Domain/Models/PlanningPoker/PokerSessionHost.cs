using Ardalis.GuardClauses;
using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Planning.Domain.Models.PlanningPoker;

/// <summary>
/// The record a poker session belongs to, such as an intake desk. A hosted session is listed only in its host's
/// area, never in the main poker grid, and whoever manages the host facilitates it.
/// </summary>
/// <remarks>
/// A reference into another module, which Planning never reads; that module answers who manages the host.
/// </remarks>
public sealed record PokerSessionHost
{
    private PokerSessionHost() { }

    /// <summary>A reference to the hosting record.</summary>
    /// <exception cref="ArgumentException">The kind is not defined, or the id is empty.</exception>
    public PokerSessionHost(PokerSessionHostKind kind, Guid id)
    {
        Kind = Guard.Against.EnumOutOfRange(kind, nameof(kind));
        Id = Guard.Against.Default(id, nameof(id));
    }

    /// <summary>The kind of record that hosts the session.</summary>
    public PokerSessionHostKind Kind { get; private init; }

    /// <summary>The hosting record's id in the module that owns it.</summary>
    public Guid Id { get; private init; }
}
