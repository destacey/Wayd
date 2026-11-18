using Wayd.Common.Domain.Attachments;

namespace Wayd.Common.Application.Attachments.Dtos;

/// <summary>An uploaded image, and the path to write in markdown to show it.</summary>
public sealed record AttachmentDto
{
    /// <summary>The attachment's id.</summary>
    public required Guid Id { get; init; }

    /// <summary>The path to use as the markdown image destination: <c>/api/attachments/{id}</c>.</summary>
    public required string Path { get; init; }

    /// <summary>The image's media type, as detected from its bytes.</summary>
    public required string ContentType { get; init; }

    /// <summary>The stored image's size in bytes, after re-encoding.</summary>
    public required long Size { get; init; }

    /// <summary>The image's width in pixels.</summary>
    public required int Width { get; init; }

    /// <summary>The image's height in pixels.</summary>
    public required int Height { get; init; }

    internal static AttachmentDto From(Attachment attachment) => new()
    {
        Id = attachment.Id,
        Path = AttachmentReferences.PathFor(attachment.Id),
        ContentType = attachment.ContentType,
        Size = attachment.Size,
        Width = attachment.Width,
        Height = attachment.Height,
    };
}

/// <summary>An attachment's bytes, as served to a caller who may read it.</summary>
/// <param name="Data">The image.</param>
/// <param name="ContentType">Its media type.</param>
/// <param name="ETag">A validator that changes only if the bytes do, which they never do.</param>
public sealed record AttachmentContentDto(byte[] Data, string ContentType, string ETag);
