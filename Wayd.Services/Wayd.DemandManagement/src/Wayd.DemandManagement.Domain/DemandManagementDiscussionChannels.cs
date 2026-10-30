using Wayd.Common.Domain.Discussions;

namespace Wayd.DemandManagement.Domain;

/// <summary>
/// The Demand Management records people can discuss.
/// </summary>
/// <remarks>
/// Declared here rather than in Common so discussions carry no Demand Management vocabulary. The key is spelled as
/// the request's follow owner type, and is persisted on every channel: <strong>never change it once shipped</strong>,
/// or every request's conversation is orphaned.
/// </remarks>
public static class DemandManagementDiscussionChannels
{
    /// <summary>
    /// An intake request's conversation: replies the requester reads, and internal notes for the desk's agents.
    /// </summary>
    public static readonly DiscussionChannelTypeDescriptor IntakeRequest =
        new(DemandManagementFollowOwners.IntakeRequest.Key, "Intake Request", "Requests", supportsInternalNotes: true);

    /// <summary>Every Demand Management channel type, for registration.</summary>
    public static readonly DiscussionChannelTypeDescriptor[] All = [IntakeRequest];

    /// <summary>Registers the Demand Management channel types with <see cref="DiscussionChannelTypes"/>. Idempotent.</summary>
    public static void Register() => DiscussionChannelTypes.Register(All);
}
