namespace Wayd.Common.Application.Attachments;

/// <summary>The limits every uploaded image is held to.</summary>
public static class AttachmentLimits
{
    /// <summary>The largest file accepted for upload, before re-encoding: 5 MB.</summary>
    public const long MaxUploadBytes = 5 * 1024 * 1024;

    /// <summary>
    /// The most pixels an image may have. Checked from the header before the image is decoded, so a small file that
    /// would decode to gigabytes is refused without being decoded.
    /// </summary>
    public const long MaxPixels = 40_000_000;

    /// <summary>The widest or tallest an image may be, in pixels.</summary>
    public const int MaxDimension = 10_000;

    /// <summary>How long an upload no save has claimed is kept before the clean-up job deletes it.</summary>
    public static readonly Duration PendingRetention = Duration.FromHours(24);
}
