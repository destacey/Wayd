using Wayd.Common.Application.Following;
using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Attachments;

namespace Wayd.Common.Application.Attachments;

/// <summary>
/// Whether the caller may read an attachment: its uploader while it is pending, and afterwards whoever may read the
/// record it belongs to, by that owner type's own read rule.
/// </summary>
/// <remarks>
/// Asks the read rule every time, from the record as it is now, so an attachment is never readable after its record
/// stops being. An owner type with no read rule admits no one. Public because Wolverine's generated handlers
/// construct it directly.
/// </remarks>
public sealed class AttachmentAccess(ICurrentUser currentUser, IEnumerable<IRecordReadRule> readRules)
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IEnumerable<IRecordReadRule> _readRules = readRules;

    /// <summary>Whether the caller may read <paramref name="attachment"/>.</summary>
    public async Task<bool> CanRead(Attachment attachment, CancellationToken cancellationToken)
    {
        if (attachment.IsPending)
        {
            return string.Equals(attachment.UploadedBy, _currentUser.GetUserId(), StringComparison.Ordinal);
        }

        var owner = FollowReadRules.Resolve(_readRules, attachment.OwnerType);

        return owner.IsSuccess && await FollowReadRules.CanRead(owner.Value.ReadRule, attachment.OwnerId!.Value, cancellationToken);
    }

    /// <summary>
    /// The registered spelling of an owner type whose records may hold attachments. Throws when it has no read rule:
    /// a module claiming for an owner type nobody can read would store images no one could ever see.
    /// </summary>
    internal string OwnerTypeKey(string ownerType)
    {
        var owner = FollowReadRules.Resolve(_readRules, ownerType);

        return owner.IsSuccess
            ? owner.Value.Descriptor.Key
            : throw new InvalidOperationException($"'{ownerType}' has no read rule, so its records cannot hold attachments.");
    }
}
