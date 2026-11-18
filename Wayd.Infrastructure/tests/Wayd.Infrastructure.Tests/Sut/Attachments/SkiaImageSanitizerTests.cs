using System.Buffers.Binary;
using System.Text;
using SkiaSharp;
using Wayd.Common.Application.Attachments;
using Wayd.Common.Domain.Attachments;
using Wayd.Infrastructure.Attachments;

namespace Wayd.Infrastructure.Tests.Sut.Attachments;

public sealed class SkiaImageSanitizerTests
{
    private readonly SkiaImageSanitizer _sanitizer = new();

    /// <summary>A <paramref name="width"/>×<paramref name="height"/> image, red on its left half and blue on its right.</summary>
    private static byte[] Encode(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            using var red = new SKPaint { Color = SKColors.Red };
            using var blue = new SKPaint { Color = SKColors.Blue };
            canvas.DrawRect(0, 0, width / 2f, height, red);
            canvas.DrawRect(width / 2f, 0, width - (width / 2f), height, blue);
        }

        using var data = bitmap.Encode(format, 95);
        return data.ToArray();
    }

    /// <summary>
    /// <paramref name="jpeg"/> with an EXIF block holding <paramref name="orientation"/> and a camera make, as a phone
    /// writes one.
    /// </summary>
    private static byte[] WithExif(byte[] jpeg, ushort orientation)
    {
        var tiff = new List<byte>();
        tiff.AddRange("II*\0"u8.ToArray());
        tiff.AddRange(BitConverter.GetBytes(8u));
        tiff.AddRange(BitConverter.GetBytes((ushort)2));
        // Orientation: SHORT, one value, held in the entry.
        tiff.AddRange(BitConverter.GetBytes((ushort)0x0112));
        tiff.AddRange(BitConverter.GetBytes((ushort)3));
        tiff.AddRange(BitConverter.GetBytes(1u));
        tiff.AddRange(BitConverter.GetBytes(orientation));
        tiff.AddRange(new byte[2]);
        // Make: ASCII "Cam", which fits in the entry.
        tiff.AddRange(BitConverter.GetBytes((ushort)0x010F));
        tiff.AddRange(BitConverter.GetBytes((ushort)2));
        tiff.AddRange(BitConverter.GetBytes(4u));
        tiff.AddRange("Cam\0"u8.ToArray());
        tiff.AddRange(BitConverter.GetBytes(0u));

        var payload = "Exif\0\0"u8.ToArray().Concat(tiff).ToArray();
        var segment = new byte[4 + payload.Length];
        segment[0] = 0xFF;
        segment[1] = 0xE1;
        BinaryPrimitives.WriteUInt16BigEndian(segment.AsSpan(2), (ushort)(payload.Length + 2));
        payload.CopyTo(segment, 4);

        return [.. jpeg[..2], .. segment, .. jpeg[2..]];
    }

    /// <summary><paramref name="png"/> with a text chunk, as an editor that saves a comment writes one.</summary>
    private static byte[] WithTextChunk(byte[] png, string keyword, string text)
    {
        var typeAndData = Encoding.Latin1.GetBytes($"tEXt{keyword}\0{text}");
        var chunk = new byte[8 + typeAndData.Length];
        BinaryPrimitives.WriteUInt32BigEndian(chunk, (uint)(typeAndData.Length - 4));
        typeAndData.CopyTo(chunk, 4);
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(4 + typeAndData.Length), Crc32(typeAndData));

        // After the 8-byte signature and the 25-byte IHDR chunk.
        return [.. png[..33], .. chunk, .. png[33..]];
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }

    /// <summary>A 1×1 GIF of two frames, red then blue.</summary>
    private static readonly byte[] AnimatedGif =
    [
        .. "GIF89a"u8.ToArray(),
        0x01, 0x00, 0x01, 0x00, 0x80, 0x00, 0x00,       // 1×1, a two-colour global table
        0xFF, 0x00, 0x00, 0x00, 0x00, 0xFF,             // red, blue
        0x21, 0xF9, 0x04, 0x00, 0x0A, 0x00, 0x00, 0x00, // frame 1: 0.1s
        0x2C, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
        0x02, 0x02, 0x44, 0x01, 0x00,                   // colour 0
        0x21, 0xF9, 0x04, 0x00, 0x0A, 0x00, 0x00, 0x00, // frame 2: 0.1s
        0x2C, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
        0x02, 0x02, 0x4C, 0x01, 0x00,                   // colour 1
        0x3B,
    ];

    private Task<CSharpFunctionalExtensions.Result<SanitizedImage>> Sanitize(byte[] bytes) =>
        _sanitizer.Sanitize(new MemoryStream(bytes), TestContext.Current.CancellationToken);

    private static bool Contains(byte[] haystack, string needle) =>
        haystack.AsSpan().IndexOf(Encoding.Latin1.GetBytes(needle)) >= 0;

    [Fact]
    public async Task Sanitize_ShouldKeepAPngAsAPng_WithoutItsTextChunks()
    {
        // Arrange
        var bytes = WithTextChunk(Encode(40, 30, SKEncodedImageFormat.Png), "Comment", "secret-note");
        Contains(bytes, "secret-note").Should().BeTrue();

        // Act
        var result = await Sanitize(bytes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ContentType.Should().Be(AttachmentImageTypes.Png);
        result.Value.Width.Should().Be(40);
        result.Value.Height.Should().Be(30);
        Contains(result.Value.Data, "secret-note").Should().BeFalse();
        SKCodec.Create(SKData.CreateCopy(result.Value.Data))!.EncodedFormat.Should().Be(SKEncodedImageFormat.Png);
    }

    [Fact]
    public async Task Sanitize_ShouldTurnAJpegUpright_AndDropItsExif()
    {
        // Arrange — orientation 6 says "rotate 90° clockwise to view": 40×30 stored shows as 30×40, and the red left
        // half of the stored pixels shows on top.
        var bytes = WithExif(Encode(40, 30, SKEncodedImageFormat.Jpeg), orientation: 6);
        Contains(bytes, "Exif\0\0").Should().BeTrue();

        // Act
        var result = await Sanitize(bytes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ContentType.Should().Be(AttachmentImageTypes.Jpeg);
        result.Value.Width.Should().Be(30);
        result.Value.Height.Should().Be(40);
        Contains(result.Value.Data, "Exif\0\0").Should().BeFalse();
        Contains(result.Value.Data, "Cam").Should().BeFalse();

        using var stored = SKBitmap.Decode(result.Value.Data);
        stored.GetPixel(15, 3).Red.Should().BeGreaterThan(200);
        stored.GetPixel(15, 3).Blue.Should().BeLessThan(60);
        stored.GetPixel(15, 36).Blue.Should().BeGreaterThan(200);
        stored.GetPixel(15, 36).Red.Should().BeLessThan(60);
    }

    [Fact]
    public async Task Sanitize_ShouldKeepAWebPAsAWebP()
    {
        // Arrange
        var bytes = Encode(20, 10, SKEncodedImageFormat.Webp);

        // Act
        var result = await Sanitize(bytes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ContentType.Should().Be(AttachmentImageTypes.WebP);
        SKCodec.Create(SKData.CreateCopy(result.Value.Data))!.EncodedFormat.Should().Be(SKEncodedImageFormat.Webp);
    }

    [Fact]
    public async Task Sanitize_ShouldStoreAnAnimatedGifAsAPngOfItsFirstFrame()
    {
        // Act
        var result = await Sanitize(AnimatedGif);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ContentType.Should().Be(AttachmentImageTypes.Png);
        using var stored = SKBitmap.Decode(result.Value.Data);
        stored.Width.Should().Be(1);
        stored.GetPixel(0, 0).Should().Be(new SKColor(255, 0, 0));
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>")]
    [InlineData("just some text")]
    public async Task Sanitize_ShouldRefuseAFileThatIsNotAnAcceptedImage(string content)
    {
        // Act
        var result = await Sanitize(Encoding.UTF8.GetBytes(content));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The file is not a PNG, JPEG, GIF or WebP image.");
    }

    [Fact]
    public async Task Sanitize_ShouldRefuseAFileThatOnlyStartsLikeAGif()
    {
        // Act — its "header" reads as nonsense dimensions or fails to decode; either way nothing is stored.
        var result = await Sanitize(Encoding.UTF8.GetBytes("GIF89a but not really"));

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Sanitize_ShouldRefuseATruncatedImage_RatherThanStoreItHalfDrawn()
    {
        // Arrange
        var png = Encode(64, 64, SKEncodedImageFormat.Png);

        // Act
        var result = await Sanitize(png[..(png.Length / 2)]);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Sanitize_ShouldRefuseAnImageWiderThanTheLimit_FromItsHeader()
    {
        // Arrange
        var bytes = Encode(AttachmentLimits.MaxDimension + 1, 1, SKEncodedImageFormat.Png);

        // Act
        var result = await Sanitize(bytes);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().StartWith("Images can be at most 10,000 pixels on a side");
    }

    [Fact]
    public async Task Sanitize_ShouldRefuseAStreamLongerThanTheLimit_WhateverItsLengthWasDeclaredAs()
    {
        // Arrange
        var bytes = new byte[AttachmentLimits.MaxUploadBytes + 1];

        // Act
        var result = await Sanitize(bytes);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Images can be at most 5 MB.");
    }
}
