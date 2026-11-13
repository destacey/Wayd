using Ardalis.GuardClauses;
using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Planning.Domain.Models.PlanningPoker;

/// <summary>
/// The record a poker round estimates, such as an intake request.
/// </summary>
/// <remarks>
/// A reference into another module, which Planning never reads. What voters read about the record is the round's
/// own <see cref="PokerRound.Label"/> and <see cref="PokerRound.Description"/>, filled from the record's title and
/// description when the round is added.
/// </remarks>
public sealed record PokerRoundSubject
{
    /// <summary>The longest display key a subject can carry.</summary>
    public const int DisplayKeyMaxLength = 64;

    private PokerRoundSubject() { }

    /// <summary>
    /// A subject for the record, trimming its key.
    /// </summary>
    /// <exception cref="ArgumentException">The key is blank or too long, or the id is empty.</exception>
    public PokerRoundSubject(PokerRoundSubjectKind kind, Guid id, string displayKey)
    {
        Kind = Guard.Against.EnumOutOfRange(kind, nameof(kind));
        Id = Guard.Against.Default(id, nameof(id));
        DisplayKey = Guard.Against.StringTooLong(Guard.Against.NullOrWhiteSpace(displayKey, nameof(displayKey)).Trim(),
            DisplayKeyMaxLength, nameof(displayKey));
    }

    /// <summary>The kind of record the round estimates.</summary>
    public PokerRoundSubjectKind Kind { get; private init; }

    /// <summary>The record's id in the module that owns it.</summary>
    public Guid Id { get; private init; }

    /// <summary>The key people know the record by, such as <c>42</c> for an intake request.</summary>
    public string DisplayKey { get; private init; } = default!;
}

/// <summary>
/// A record to add a poker round about: what it is, and the title and description the round takes from it.
/// </summary>
/// <param name="Subject">The record.</param>
/// <param name="Title">The record's title, which becomes the round's label.</param>
/// <param name="Description">The record's description, which becomes the round's, or null when it has none.</param>
public sealed record PokerRoundRecord(PokerRoundSubject Subject, string Title, string? Description);
