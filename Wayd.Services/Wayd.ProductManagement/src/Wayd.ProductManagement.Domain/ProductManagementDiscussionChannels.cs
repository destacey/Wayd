using Wayd.Common.Domain.Discussions;

namespace Wayd.ProductManagement.Domain;

/// <summary>
/// The Product Management records people can discuss.
/// </summary>
/// <remarks>
/// Declared here rather than in Common so discussions carry no Product Management vocabulary. The key is spelled as
/// the record's follow owner type, and is persisted on every channel: <strong>never change it once shipped</strong>,
/// or every record's conversation is orphaned.
/// </remarks>
public static class ProductManagementDiscussionChannels
{
    /// <summary>An incident's conversation: public messages only, with subjects for parallel topics.</summary>
    public static readonly DiscussionChannelTypeDescriptor Incident =
        new(ProductWorkflowOwners.Incident.Key, "Incident", "Incidents", supportsInternalNotes: false);

    /// <summary>Every Product Management channel type, for registration.</summary>
    public static readonly DiscussionChannelTypeDescriptor[] All = [Incident];

    /// <summary>Registers the Product Management channel types with <see cref="DiscussionChannelTypes"/>. Idempotent.</summary>
    public static void Register() => DiscussionChannelTypes.Register(All);
}
