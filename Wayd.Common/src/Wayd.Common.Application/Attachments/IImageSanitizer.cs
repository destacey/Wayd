namespace Wayd.Common.Application.Attachments;

/// <summary>
/// Turns an uploaded file into an image safe to store and serve: decoded, checked, stripped of metadata and encoded
/// again, so what is kept is pixels the server wrote itself, with their colour space.
/// </summary>
/// <remarks>
/// The format is detected from the bytes, never from a file name or a declared type. Re-encoding drops anything a
/// file carried beside its pixels — EXIF location, comments, a script hidden in a polyglot file — and fails a file
/// that is not really an image.
/// </remarks>
public interface IImageSanitizer
{
    /// <summary>
    /// The re-encoded image, or a failure a user can read when the file is not an accepted image or is too large.
    /// </summary>
    Task<Result<SanitizedImage>> Sanitize(Stream content, CancellationToken cancellationToken);
}

/// <summary>An image as the server re-encoded it.</summary>
/// <param name="Data">The encoded bytes.</param>
/// <param name="ContentType">Its media type, one of <c>AttachmentImageTypes.All</c>.</param>
/// <param name="Width">Its width in pixels.</param>
/// <param name="Height">Its height in pixels.</param>
public sealed record SanitizedImage(byte[] Data, string ContentType, int Width, int Height);
