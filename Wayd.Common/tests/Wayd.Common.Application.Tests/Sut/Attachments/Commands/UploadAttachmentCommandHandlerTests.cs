using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Attachments;
using Wayd.Common.Application.Attachments.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Attachments;
using Wayd.Tests.Shared;

namespace Wayd.Common.Application.Tests.Sut.Attachments.Commands;

public sealed class UploadAttachmentCommandHandlerTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 17, 12, 0);

    private readonly FakeAttachmentsDbContext _dbContext = new();
    private readonly FakeAttachmentStore _store = new();
    private readonly Mock<IImageSanitizer> _sanitizer = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    public UploadAttachmentCommandHandlerTests()
    {
        _currentUser.Setup(u => u.GetUserId()).Returns("user-me");
    }

    private UploadAttachmentCommandHandler CreateHandler() => new(
        _dbContext,
        _store,
        _sanitizer.Object,
        _currentUser.Object,
        new TestingDateTimeProvider(new FakeClock(Now)),
        NullLogger<UploadAttachmentCommandHandler>.Instance);

    [Fact]
    public async Task Handle_ShouldStoreTheReEncodedImage_AsAPendingUploadOfTheCaller()
    {
        // Arrange — what is stored is what the sanitizer wrote, never the upload itself.
        byte[] reEncoded = [4, 5, 6, 7];
        _sanitizer
            .Setup(s => s.Sanitize(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SanitizedImage(reEncoded, AttachmentImageTypes.WebP, 320, 200));
        using var upload = new MemoryStream([9, 9, 9]);

        // Act
        var result = await CreateHandler().Handle(new UploadAttachmentCommand(upload, 3), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var attachment = _dbContext.All.Single();
        attachment.IsPending.Should().BeTrue();
        attachment.UploadedBy.Should().Be("user-me");
        attachment.UploadedOn.Should().Be(Now);
        attachment.ContentType.Should().Be(AttachmentImageTypes.WebP);
        attachment.Size.Should().Be(4);
        attachment.Sha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(reEncoded)));
        _store.Data[attachment.Id].Should().Equal(reEncoded);
        result.Value.Id.Should().Be(attachment.Id);
        result.Value.Path.Should().Be($"/api/attachments/{attachment.Id}");
        result.Value.Width.Should().Be(320);
        result.Value.Height.Should().Be(200);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldStoreNothing_WhenTheFileIsNotAnAcceptedImage()
    {
        // Arrange
        _sanitizer
            .Setup(s => s.Sanitize(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CSharpFunctionalExtensions.Result.Failure<SanitizedImage>("The file is not a PNG, JPEG, GIF or WebP image."));
        using var upload = new MemoryStream([1]);

        // Act
        var result = await CreateHandler().Handle(new UploadAttachmentCommand(upload, 1), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The file is not a PNG, JPEG, GIF or WebP image.");
        _dbContext.All.Should().BeEmpty();
        _store.Data.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(AttachmentLimits.MaxUploadBytes, true)]
    [InlineData(AttachmentLimits.MaxUploadBytes + 1, false)]
    public void Validator_ShouldAcceptOnlyANonEmptyFileWithinTheLimit(long length, bool isValid)
    {
        // Act
        var result = new UploadAttachmentCommandValidator().Validate(new UploadAttachmentCommand(Stream.Null, length));

        // Assert
        result.IsValid.Should().Be(isValid);
    }
}
