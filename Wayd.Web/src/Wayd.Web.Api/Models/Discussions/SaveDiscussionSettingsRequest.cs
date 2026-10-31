namespace Wayd.Web.Api.Models.Discussions;

/// <summary>
/// How conversations should behave in one scope, such as an intake desk, or by default for a kind of record.
/// </summary>
public sealed record SaveDiscussionSettingsRequest
{
    /// <summary>
    /// Whether new internal notes can be posted. Turning it off leaves existing notes readable to their audience. Can
    /// only be true where the kind of record holds internal notes.
    /// </summary>
    public bool InternalNotesEnabled { get; set; }

    /// <summary>
    /// Whether mentioning someone can give them access to the record. When false, only the people who already have
    /// the record can be mentioned. Can only be true where the kind of record allows it.
    /// </summary>
    public bool MentionsGrantAccess { get; set; }
}
