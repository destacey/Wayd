namespace Wayd.Common.Application.Attachments;

/// <summary>
/// Keeps a record's attachments in step with the markdown it saves: what it refers to it owns, and what it no
/// longer refers to is deleted.
/// </summary>
/// <remarks>
/// A module calls it from each command that saves the record's markdown, passing every markdown field of the record
/// together, and saves its own context afterwards: every change here is tracked in the same unit of work, so the
/// record and its attachments are saved together or not at all.
/// <list type="number">
/// <item><see cref="CopyForeign"/> before the text reaches the aggregate, since it can change the text.</item>
/// <item><see cref="Claim"/> once the record's id is known, before the save.</item>
/// <item><see cref="RemoveAll"/> when the record is deleted.</item>
/// </list>
/// The owner type is the record's read rule owner type: that rule decides who may read the record's attachments.
/// </remarks>
public interface IMarkdownAttachments
{
    /// <summary>
    /// The texts with each reference to an image belonging to another record, and readable by the caller, pointed
    /// at a new copy pending for the caller — so the record owns every image it shows, and access to it never
    /// follows a different record.
    /// </summary>
    /// <remarks>
    /// Left as they are: the caller's own pending uploads, the record's own attachments, and references the caller
    /// cannot read, which they cannot copy — those still show only to people who can read the record that owns them.
    /// </remarks>
    /// <param name="ownerId">The record being saved, or null when it is being created.</param>
    Task<IReadOnlyList<string?>> CopyForeign(string ownerType, Guid? ownerId, IReadOnlyList<string?> markdown, CancellationToken cancellationToken);

    /// <summary>
    /// Gives the record each of the caller's pending uploads its texts refer to, and deletes each of its attachments
    /// they no longer refer to.
    /// </summary>
    /// <param name="markdown">Every markdown field of the record, as it will be saved.</param>
    Task Claim(string ownerType, Guid ownerId, IReadOnlyList<string?> markdown, CancellationToken cancellationToken);

    /// <summary>Deletes every attachment the record holds.</summary>
    Task RemoveAll(string ownerType, Guid ownerId, CancellationToken cancellationToken);
}
