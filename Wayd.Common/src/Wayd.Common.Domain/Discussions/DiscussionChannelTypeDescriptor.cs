using Ardalis.GuardClauses;

namespace Wayd.Common.Domain.Discussions;

/// <summary>
/// What a module declares about one kind of record people can discuss: the key its channels are stored under,
/// what to call it, the sidebar section it belongs to, and which parts of a conversation it supports.
/// </summary>
/// <remarks>
/// The seam that keeps discussions free of any module's vocabulary, as <c>FollowOwnerDescriptor</c> is for
/// following. A module declares its descriptors as static readonly fields in its own domain project and
/// registers them with <see cref="DiscussionChannelTypes"/>. Who may read or post is not declared here: that is
/// the module's channel type in its Application project.
/// <para>
/// Not a record: <see cref="DiscussionChannelTypes"/> treats re-registering the same instance as harmless and a
/// second instance under one key as a conflict, which value equality would blur.
/// </para>
/// </remarks>
public sealed class DiscussionChannelTypeDescriptor
{
    /// <summary>The longest key the <c>ChannelType</c> column holds.</summary>
    public const int KeyMaxLength = 64;

    /// <param name="key">
    /// Stable identifier, persisted on every channel. Spelled the same as the record's follow owner type, such
    /// as <c>demand.intake-request</c>. <strong>Never change one after it ships</strong>: renaming it orphans
    /// every channel that carries it.
    /// </param>
    /// <param name="displayName">What to call this kind of record in a heading or a message.</param>
    /// <param name="section">The sidebar section its channels are grouped under, such as "Requests".</param>
    /// <param name="supportsInternalNotes">
    /// Whether conversations on this kind of record can hold internal notes at all. When false, no setting can
    /// turn them on.
    /// </param>
    /// <param name="supportsSubjects">Whether top-level messages may carry a subject.</param>
    public DiscussionChannelTypeDescriptor(
        string key,
        string displayName,
        string section,
        bool supportsInternalNotes,
        bool supportsSubjects = true)
    {
        Key = Guard.Against.NullOrWhiteSpace(key, nameof(key)).Trim();
        DisplayName = Guard.Against.NullOrWhiteSpace(displayName, nameof(displayName)).Trim();
        Section = Guard.Against.NullOrWhiteSpace(section, nameof(section)).Trim();
        SupportsInternalNotes = supportsInternalNotes;
        SupportsSubjects = supportsSubjects;

        if (Key.Length > KeyMaxLength)
        {
            throw new ArgumentException($"'{Key}' is longer than {KeyMaxLength} characters.", nameof(key));
        }
    }

    /// <summary>
    /// Stable identifier persisted on every channel. See the constructor for why it must never change once it
    /// has shipped.
    /// </summary>
    public string Key { get; }

    /// <summary>What to call this kind of record in a heading or a message.</summary>
    public string DisplayName { get; }

    /// <summary>The sidebar section this kind of record's channels are grouped under. Never stored.</summary>
    public string Section { get; }

    /// <summary>Whether conversations on this kind of record can hold internal notes at all.</summary>
    public bool SupportsInternalNotes { get; }

    /// <summary>Whether top-level messages may carry a subject.</summary>
    public bool SupportsSubjects { get; }
}
