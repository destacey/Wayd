using Ardalis.GuardClauses;
using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Planning.Domain.Models.PlanningPoker;

/// <summary>
/// The record a poker round estimates, such as an intake request, with what voters read about it.
/// </summary>
/// <remarks>
/// A reference into another module, which Planning never reads. The title and description are a snapshot taken
/// when the round is added, so voters read the record as it was then; adding a record to a session shares that
/// snapshot with everyone in the session, whatever the record's own visibility.
/// </remarks>
public sealed record PokerRoundSubject
{
    /// <summary>The longest display key a subject can carry.</summary>
    public const int DisplayKeyMaxLength = 64;

    /// <summary>The longest title a subject can carry.</summary>
    public const int TitleMaxLength = 512;

    /// <summary>
    /// The most of a record's description a subject keeps. A longer one is cut, since it is a snapshot for voters
    /// to read before they estimate, not the record.
    /// </summary>
    public const int DescriptionMaxLength = 4000;

    private PokerRoundSubject() { }

    /// <summary>
    /// A subject for the record, trimming each value and keeping no description when it is blank. A description
    /// longer than <see cref="DescriptionMaxLength"/> keeps its start.
    /// </summary>
    /// <exception cref="ArgumentException">The key or title is blank or too long, or the id is empty.</exception>
    public PokerRoundSubject(PokerRoundSubjectKind kind, Guid id, string displayKey, string title, string? description)
    {
        Kind = Guard.Against.EnumOutOfRange(kind, nameof(kind));
        Id = Guard.Against.Default(id, nameof(id));
        DisplayKey = Guard.Against.StringTooLong(Guard.Against.NullOrWhiteSpace(displayKey, nameof(displayKey)).Trim(),
            DisplayKeyMaxLength, nameof(displayKey));
        Title = Guard.Against.StringTooLong(Guard.Against.NullOrWhiteSpace(title, nameof(title)).Trim(),
            TitleMaxLength, nameof(title));
        Description = string.IsNullOrWhiteSpace(description) ? null : Shorten(description.Trim());
    }

    /// <summary>The kind of record the round estimates.</summary>
    public PokerRoundSubjectKind Kind { get; private init; }

    /// <summary>The record's id in the module that owns it.</summary>
    public Guid Id { get; private init; }

    /// <summary>The key people know the record by, such as <c>42</c> for an intake request.</summary>
    public string DisplayKey { get; private init; } = default!;

    /// <summary>The record's title when the round was added.</summary>
    public string Title { get; private init; } = default!;

    /// <summary>The record's description when the round was added, or null when it had none.</summary>
    public string? Description { get; private init; }

    private static string Shorten(string description) =>
        description.Length <= DescriptionMaxLength ? description : description[..DescriptionMaxLength].TrimEnd();
}
