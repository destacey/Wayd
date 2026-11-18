namespace Wayd.Common.Application.Attachments;

/// <summary>Where the bytes of each attachment are kept, by attachment id.</summary>
/// <remarks>
/// Writes and deletes take effect with the caller's next save of the attachments context, so an attachment row and
/// its bytes are written together or not at all. A store outside the database must keep that promise for writes —
/// bytes written for a save that fails must never be served — and may delete late.
/// </remarks>
public interface IAttachmentStore
{
    /// <summary>Stores <paramref name="data"/> as the bytes of attachment <paramref name="id"/>.</summary>
    void Write(Guid id, byte[] data);

    /// <summary>Stores the bytes of attachment <paramref name="sourceId"/> again as those of <paramref name="targetId"/>.</summary>
    Task Copy(Guid sourceId, Guid targetId, CancellationToken cancellationToken);

    /// <summary>The bytes of attachment <paramref name="id"/>, or null when none are stored.</summary>
    Task<byte[]?> Read(Guid id, CancellationToken cancellationToken);

    /// <summary>Removes the bytes of attachment <paramref name="id"/>.</summary>
    void Delete(Guid id);
}
