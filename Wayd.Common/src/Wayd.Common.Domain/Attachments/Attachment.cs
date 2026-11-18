using Ardalis.GuardClauses;
using NodaTime;

namespace Wayd.Common.Domain.Attachments;

/// <summary>
/// An image placed in a record's markdown, stored apart from the text, which refers to it by
/// <see cref="AttachmentReferences.PathFor"/>.
/// </summary>
/// <remarks>
/// <strong>Access follows exactly one record.</strong> Until a save claims it, an attachment is pending and only
/// its uploader may read it; once claimed, whoever may read its owner may read it, by that owner type's own read
/// rule. A record that refers to an attachment owned by another record gets a copy of its own rather than a share,
/// so no attachment ever answers to two records.
/// <para>
/// The bytes live in an <c>IAttachmentStore</c>, not here, so reading who owns an attachment never loads the image.
/// An attachment's bytes never change, so a copy is a new attachment and the hash is a stable cache validator.
/// Raises no events: what a record says is recorded by the record's own events, and an attachment is a part of
/// that text, not a record of its own.
/// </para>
/// </remarks>
public sealed class Attachment
{
    /// <summary>The longest user id an attachment records, matching the audit columns.</summary>
    public const int UserIdMaxLength = 450;

    private Attachment() { }

    private Attachment(Guid id, string contentType, long size, int width, int height, string sha256, string uploadedBy, Instant uploadedOn)
    {
        Id = id;
        ContentType = contentType;
        Size = size;
        Width = width;
        Height = height;
        Sha256 = sha256;
        UploadedBy = uploadedBy;
        UploadedOn = uploadedOn;
    }

    /// <summary>
    /// Creates a pending attachment for an image already checked and re-encoded, whose bytes the caller stores
    /// under the returned id.
    /// </summary>
    /// <param name="contentType">One of <see cref="AttachmentImageTypes.All"/>.</param>
    /// <param name="sha256">The lower-case hex SHA-256 of the stored bytes.</param>
    /// <param name="uploadedBy">The user id of the account uploading it.</param>
    public static Attachment CreatePending(string contentType, long size, int width, int height, string sha256, string uploadedBy, Instant uploadedOn)
    {
        Guard.Against.InvalidInput(contentType, nameof(contentType), AttachmentImageTypes.All.Contains, "Not an image type an attachment may hold.");
        Guard.Against.NegativeOrZero(size, nameof(size));
        Guard.Against.NegativeOrZero(width, nameof(width));
        Guard.Against.NegativeOrZero(height, nameof(height));
        Guard.Against.NullOrWhiteSpace(sha256, nameof(sha256));
        Guard.Against.NullOrWhiteSpace(uploadedBy, nameof(uploadedBy));

        return new Attachment(Guid.CreateVersion7(), contentType, size, width, height, sha256, uploadedBy, uploadedOn);
    }

    /// <summary>The attachment's id, which its markdown reference carries.</summary>
    public Guid Id { get; private init; }

    /// <summary>The image's media type, as the server detected it from the bytes.</summary>
    public string ContentType { get; private init; } = default!;

    /// <summary>The size of the stored bytes.</summary>
    public long Size { get; private init; }

    /// <summary>The image's width in pixels.</summary>
    public int Width { get; private init; }

    /// <summary>The image's height in pixels.</summary>
    public int Height { get; private init; }

    /// <summary>The lower-case hex SHA-256 of the stored bytes; served as the entity tag.</summary>
    public string Sha256 { get; private init; } = default!;

    /// <summary>The user id of the account that uploaded it, or that a copy was made for.</summary>
    public string UploadedBy { get; private init; } = default!;

    /// <summary>When it was uploaded or copied.</summary>
    public Instant UploadedOn { get; private init; }

    /// <summary>The owner type of the record it belongs to, or null while it is pending.</summary>
    public string? OwnerType { get; private set; }

    /// <summary>The id of the record it belongs to, or null while it is pending.</summary>
    public Guid? OwnerId { get; private set; }

    /// <summary>When a save claimed it for its owner, or null while it is pending.</summary>
    public Instant? ClaimedOn { get; private set; }

    /// <summary>Whether no record has claimed it yet, so only its uploader may read it.</summary>
    public bool IsPending => OwnerId is null;

    /// <summary>Whether it belongs to the given record.</summary>
    public bool IsOwnedBy(string ownerType, Guid ownerId) =>
        OwnerId == ownerId && string.Equals(OwnerType, ownerType, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A new pending attachment with the same image, for <paramref name="uploadedBy"/> to claim for another record.
    /// The caller copies the bytes to the new id.
    /// </summary>
    public Attachment CopyFor(string uploadedBy, Instant now)
    {
        Guard.Against.NullOrWhiteSpace(uploadedBy, nameof(uploadedBy));

        return new Attachment(Guid.CreateVersion7(), ContentType, Size, Width, Height, Sha256, uploadedBy, now);
    }

    /// <summary>
    /// Gives a pending attachment to the record whose markdown refers to it. Throws when it already belongs to a
    /// record: access must never move from one record to another.
    /// </summary>
    public void ClaimFor(string ownerType, Guid ownerId, Instant now)
    {
        Guard.Against.NullOrWhiteSpace(ownerType, nameof(ownerType));
        Guard.Against.Default(ownerId, nameof(ownerId));

        if (!IsPending)
        {
            throw new InvalidOperationException($"Attachment {Id} already belongs to a record.");
        }

        OwnerType = ownerType;
        OwnerId = ownerId;
        ClaimedOn = now;
    }
}
