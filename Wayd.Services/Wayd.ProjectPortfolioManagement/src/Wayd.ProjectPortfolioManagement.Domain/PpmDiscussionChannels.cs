using Wayd.Common.Domain.Discussions;

namespace Wayd.ProjectPortfolioManagement.Domain;

/// <summary>
/// The Project Portfolio Management records people can discuss.
/// </summary>
/// <remarks>
/// Declared here rather than in Common so discussions carry no PPM vocabulary. The key is spelled as the project's
/// follow owner type, and is persisted on every channel: <strong>never change it once shipped</strong>, or every
/// project's conversation is orphaned.
/// </remarks>
public static class PpmDiscussionChannels
{
    /// <summary>
    /// A project's conversation: messages everyone who can read the project reads, with subjects to keep parallel
    /// topics apart. No internal notes.
    /// </summary>
    public static readonly DiscussionChannelTypeDescriptor Project =
        new(PpmFollowOwners.Project.Key, "Project", "Projects", supportsInternalNotes: false);

    /// <summary>Every PPM channel type, for registration.</summary>
    public static readonly DiscussionChannelTypeDescriptor[] All = [Project];

    /// <summary>Registers the PPM channel types with <see cref="DiscussionChannelTypes"/>. Idempotent.</summary>
    public static void Register() => DiscussionChannelTypes.Register(All);
}
