namespace Wayd.Common.Domain.Attachments;

/// <summary>
/// The stored bytes of one attachment, kept by the database attachment store in a table of their own so that
/// reading who owns an attachment never loads its image.
/// </summary>
public sealed class AttachmentContent
{
    private AttachmentContent() { }

    /// <summary>Holds <paramref name="data"/> as the bytes of attachment <paramref name="attachmentId"/>.</summary>
    public AttachmentContent(Guid attachmentId, byte[] data)
    {
        AttachmentId = attachmentId;
        Data = data;
    }

    /// <summary>The attachment these bytes belong to.</summary>
    public Guid AttachmentId { get; private init; }

    /// <summary>The image, as re-encoded on upload.</summary>
    public byte[] Data { get; private init; } = default!;
}
