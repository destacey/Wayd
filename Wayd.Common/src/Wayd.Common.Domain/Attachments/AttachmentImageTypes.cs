namespace Wayd.Common.Domain.Attachments;

/// <summary>The image media types an attachment is stored as. A GIF upload is stored as a PNG of its first frame.</summary>
/// <remarks>
/// Raster formats only. SVG is never accepted: it is a document that can carry script, and a browser opening one
/// from the API's origin would run it there.
/// </remarks>
public static class AttachmentImageTypes
{
    /// <summary>PNG.</summary>
    public const string Png = "image/png";

    /// <summary>JPEG.</summary>
    public const string Jpeg = "image/jpeg";

    /// <summary>WebP.</summary>
    public const string WebP = "image/webp";

    /// <summary>Every accepted media type.</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Png, Jpeg, WebP };
}
