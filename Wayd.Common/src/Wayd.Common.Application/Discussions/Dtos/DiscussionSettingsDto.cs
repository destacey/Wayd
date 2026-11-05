using Wayd.Common.Domain.Discussions;

namespace Wayd.Common.Application.Discussions.Dtos;

/// <summary>
/// How conversations behave in one scope, such as an intake desk, or by default for a kind of record: the settings in
/// effect, where they come from, and which of them the kind of record supports.
/// </summary>
public sealed record DiscussionSettingsDto
{
    /// <summary>The registered channel type, such as <c>demand.intake-request</c>.</summary>
    public required string ChannelType { get; init; }

    /// <summary>The scope's record, or null for the default every scope inherits.</summary>
    public Guid? ScopeId { get; init; }

    /// <summary>Whether new internal notes can be posted. Existing notes stay readable to their audience either way.</summary>
    public bool InternalNotesEnabled { get; init; }

    /// <summary>Whether mentioning someone can give them access to the record. When off, only the people who already have the record can be mentioned.</summary>
    public bool MentionsGrantAccess { get; init; }

    /// <summary>
    /// Whether the scope has saved no settings of its own and follows its default. Saving values equal to the
    /// default keeps it following.
    /// </summary>
    public bool IsInherited { get; init; }

    /// <summary>Whether this kind of record can hold internal notes at all. When false, they cannot be turned on.</summary>
    public bool SupportsInternalNotes { get; init; }

    /// <summary>Whether a mention can give access on this kind of record at all. When false, it cannot be turned on.</summary>
    public bool SupportsMentionAccess { get; init; }

    /// <summary>Whether the caller may change these settings.</summary>
    public bool CanManage { get; init; }
}
