namespace Wayd.Common.Domain.Discussions;

/// <summary>
/// How conversations in one scope behave: whether new internal notes can be posted, and whether a mention can
/// give someone access.
/// </summary>
/// <param name="InternalNotesEnabled">
/// Whether new internal notes can be posted. Turning it off leaves existing notes readable to their audience.
/// </param>
/// <param name="MentionsGrantAccess">
/// Whether mentioning someone can give them access to the record, where the channel type allows it at all. When
/// off, only the people who already have the record can be mentioned.
/// </param>
public sealed record DiscussionSettingValues(bool InternalNotesEnabled, bool MentionsGrantAccess);
