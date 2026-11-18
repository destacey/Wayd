using Ardalis.GuardClauses;

namespace Wayd.Common.Domain.Links;

/// <summary>
/// What a module declares about one kind of record that can carry links: the key its links are stored under, what to
/// call it, and the aggregate type its activity is filed under.
/// </summary>
/// <remarks>
/// The seam that keeps links free of any module's vocabulary, as <c>KpiSubjectTypeDescriptor</c> is for KPIs. A module
/// declares its descriptors as static readonly fields in its own domain project and registers them with
/// <see cref="LinkOwnerTypes"/>. Who may read or change a record's links is not declared here: that is the module's
/// owner type in its Application project.
/// <para>
/// Not a record: <see cref="LinkOwnerTypes"/> treats re-registering the same instance as harmless and a second
/// instance under one key as a conflict, which value equality would blur.
/// </para>
/// </remarks>
public sealed class LinkOwnerTypeDescriptor
{
    /// <summary>The longest key the <c>OwnerType</c> column holds.</summary>
    public const int KeyMaxLength = 64;

    /// <param name="key">
    /// Stable identifier, persisted on every link, such as <c>ppm.project</c>. Spelled the same as the record's
    /// follow owner type when it has one. <strong>Never change one after it ships</strong>: renaming it orphans every
    /// link that carries it.
    /// </param>
    /// <param name="displayName">What to call this kind of record in a heading or a message.</param>
    /// <param name="activityAggregateType">
    /// The aggregate type the record's own activity log entries are filed under, such as <c>Project</c>, so a link's
    /// events are listed on the record as well. <strong>Never change it once shipped</strong>: entries already written
    /// are filed under it.
    /// </param>
    public LinkOwnerTypeDescriptor(string key, string displayName, string activityAggregateType)
    {
        Key = Guard.Against.NullOrWhiteSpace(key, nameof(key)).Trim();
        DisplayName = Guard.Against.NullOrWhiteSpace(displayName, nameof(displayName)).Trim();
        ActivityAggregateType = Guard.Against.NullOrWhiteSpace(activityAggregateType, nameof(activityAggregateType)).Trim();

        if (Key.Length > KeyMaxLength)
        {
            throw new ArgumentException($"'{Key}' is longer than {KeyMaxLength} characters.", nameof(key));
        }
    }

    /// <summary>
    /// Stable identifier persisted on every link. See the constructor for why it must never change once it has shipped.
    /// </summary>
    public string Key { get; }

    /// <summary>What to call this kind of record in a heading or a message.</summary>
    public string DisplayName { get; }

    /// <summary>The aggregate type the record's activity log entries are filed under.</summary>
    public string ActivityAggregateType { get; }
}
