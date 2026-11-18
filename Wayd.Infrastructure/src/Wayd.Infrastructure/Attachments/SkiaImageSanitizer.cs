using CSharpFunctionalExtensions;
using SkiaSharp;
using Wayd.Common.Application.Attachments;
using Wayd.Common.Domain.Attachments;

namespace Wayd.Infrastructure.Attachments;

/// <summary>
/// Re-encodes uploads with SkiaSharp: the format is detected from the bytes, the size is checked from the header
/// before anything is decoded, the image is turned upright, and only its pixels and colour space are written out
/// again, so no EXIF, XMP or comment the file carried survives.
/// </summary>
/// <remarks>
/// SkiaSharp is MIT and Skia BSD-3-Clause; the native library ships with its notices. Skia writes PNG, JPEG and
/// WebP but not GIF, so a GIF is stored as a PNG of its first frame. Nothing outside this class names SkiaSharp, so it
/// can be replaced by implementing <see cref="IImageSanitizer"/> again.
/// </remarks>
public sealed class SkiaImageSanitizer : IImageSanitizer
{
    private const string NotAnImageError = "The file is not a PNG, JPEG, GIF or WebP image.";

    /// <summary>The quality JPEG and WebP images are encoded at: indistinguishable from the original at a glance.</summary>
    private const int Quality = 90;

    private static readonly string TooLargeError =
        $"Images can be at most {AttachmentLimits.MaxDimension:N0} pixels on a side and {AttachmentLimits.MaxPixels / 1_000_000:N0} megapixels in all.";

    /// <inheritdoc/>
    public async Task<Result<SanitizedImage>> Sanitize(Stream content, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        if (!await CopyWithin(content, buffer, AttachmentLimits.MaxUploadBytes, cancellationToken))
        {
            return Result.Failure<SanitizedImage>("Images can be at most 5 MB.");
        }

        using var data = SKData.CreateCopy(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
        using var codec = SKCodec.Create(data);
        if (codec is null)
        {
            return Result.Failure<SanitizedImage>(NotAnImageError);
        }

        var output = OutputFormatOf(codec.EncodedFormat);
        if (output is null)
        {
            return Result.Failure<SanitizedImage>(NotAnImageError);
        }

        // Read from the header: nothing is decoded until the image is known to fit.
        var width = codec.Info.Width;
        var height = codec.Info.Height;
        if (width <= 0
            || height <= 0
            || width > AttachmentLimits.MaxDimension
            || height > AttachmentLimits.MaxDimension
            || (long)width * height > AttachmentLimits.MaxPixels)
        {
            return Result.Failure<SanitizedImage>(TooLargeError);
        }

        var info = new SKImageInfo(
            width,
            height,
            SKColorType.Rgba8888,
            codec.Info.AlphaType == SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Premul,
            codec.Info.ColorSpace);

        using var decoded = new SKBitmap(info);
        if (decoded.GetPixels() == IntPtr.Zero)
        {
            return Result.Failure<SanitizedImage>(TooLargeError);
        }

        // Only the first frame is decoded, so an animation is never decoded whole. A truncated file is refused rather
        // than stored half-drawn.
        if (codec.GetPixels(info, decoded.GetPixels()) != SKCodecResult.Success)
        {
            return Result.Failure<SanitizedImage>(NotAnImageError);
        }

        // Applied here because the EXIF that recorded it is not written out: a phone photo would otherwise be stored
        // on its side.
        using var turned = codec.EncodedOrigin == SKEncodedOrigin.TopLeft ? null : Orient(decoded, codec.EncodedOrigin);
        var upright = turned ?? decoded;
        using var pixmap = upright.PeekPixels();
        using var encoded = output.Value.Format switch
        {
            SKEncodedImageFormat.Jpeg => pixmap.Encode(new SKJpegEncoderOptions(Quality)),
            SKEncodedImageFormat.Webp => pixmap.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossy, Quality)),
            _ => pixmap.Encode(SKPngEncoderOptions.Default),
        };
        if (encoded is null)
        {
            return Result.Failure<SanitizedImage>(NotAnImageError);
        }

        return new SanitizedImage(encoded.ToArray(), output.Value.ContentType, upright.Width, upright.Height);
    }

    /// <summary>What an accepted format is stored as, or null for a format that is not accepted.</summary>
    private static (SKEncodedImageFormat Format, string ContentType)? OutputFormatOf(SKEncodedImageFormat input) => input switch
    {
        SKEncodedImageFormat.Png or SKEncodedImageFormat.Gif => (SKEncodedImageFormat.Png, AttachmentImageTypes.Png),
        SKEncodedImageFormat.Jpeg => (SKEncodedImageFormat.Jpeg, AttachmentImageTypes.Jpeg),
        SKEncodedImageFormat.Webp => (SKEncodedImageFormat.Webp, AttachmentImageTypes.WebP),
        _ => null,
    };

    /// <summary><paramref name="source"/> redrawn the way its EXIF orientation says it is viewed.</summary>
    private static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        var width = source.Width;
        var height = source.Height;
        var turned = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var result = new SKBitmap(source.Info.WithSize(turned ? height : width, turned ? width : height));

        using var canvas = new SKCanvas(result);
        canvas.SetMatrix(origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, width, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, width, 0, -1, height, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, height, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, height, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, height, -1, 0, width, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, width, 0, 0, 1),
            _ => SKMatrix.Identity,
        });
        canvas.DrawBitmap(source, 0, 0, SKSamplingOptions.Default);
        canvas.Flush();

        return result;
    }

    /// <summary>
    /// Copies <paramref name="source"/> into <paramref name="target"/>, answering false as soon as it is longer than
    /// <paramref name="maxBytes"/>, whatever length the request declared.
    /// </summary>
    private static async Task<bool> CopyWithin(Stream source, Stream target, long maxBytes, CancellationToken cancellationToken)
    {
        var chunk = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(chunk, cancellationToken)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                return false;
            }

            await target.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return true;
    }
}
