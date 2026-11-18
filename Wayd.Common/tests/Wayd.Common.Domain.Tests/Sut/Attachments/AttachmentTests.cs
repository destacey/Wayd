using NodaTime;
using Wayd.Common.Domain.Attachments;

namespace Wayd.Common.Domain.Tests.Sut.Attachments;

public sealed class AttachmentTests
{
    private static readonly Instant UploadedOn = Instant.FromUtc(2026, 11, 17, 9, 0);
    private static readonly Instant Later = UploadedOn + Duration.FromHours(1);

    private static Attachment Pending(string uploadedBy = "user-1") =>
        Attachment.CreatePending(AttachmentImageTypes.Png, 1024, 640, 480, new string('a', 64), uploadedBy, UploadedOn);

    [Fact]
    public void CreatePending_ShouldBelongToNoRecord()
    {
        // Act
        var attachment = Pending();

        // Assert
        attachment.IsPending.Should().BeTrue();
        attachment.OwnerType.Should().BeNull();
        attachment.OwnerId.Should().BeNull();
        attachment.UploadedBy.Should().Be("user-1");
        attachment.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void CreatePending_ShouldRefuseAnSvg()
    {
        // Act
        var create = () => Attachment.CreatePending("image/svg+xml", 1024, 640, 480, new string('a', 64), "user-1", UploadedOn);

        // Assert
        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ClaimFor_ShouldGiveThePendingAttachmentToTheRecord()
    {
        // Arrange
        var attachment = Pending();
        var projectId = Guid.NewGuid();

        // Act
        attachment.ClaimFor("ppm.project", projectId, Later);

        // Assert
        attachment.IsPending.Should().BeFalse();
        attachment.IsOwnedBy("PPM.Project", projectId).Should().BeTrue();
        attachment.IsOwnedBy("ppm.project", Guid.NewGuid()).Should().BeFalse();
        attachment.ClaimedOn.Should().Be(Later);
    }

    [Fact]
    public void ClaimFor_ShouldThrow_WhenTheAttachmentAlreadyBelongsToARecord()
    {
        // Arrange
        var attachment = Pending();
        var projectId = Guid.NewGuid();
        attachment.ClaimFor("ppm.project", projectId, Later);

        // Act
        var claimAgain = () => attachment.ClaimFor("ppm.project", Guid.NewGuid(), Later);

        // Assert
        claimAgain.Should().Throw<InvalidOperationException>();
        attachment.IsOwnedBy("ppm.project", projectId).Should().BeTrue();
    }

    [Fact]
    public void CopyFor_ShouldBeANewPendingAttachmentOfTheSameImage_ForTheGivenUser()
    {
        // Arrange
        var original = Pending("user-1");
        original.ClaimFor("ppm.project", Guid.NewGuid(), Later);

        // Act
        var copy = original.CopyFor("user-2", Later);

        // Assert
        copy.Id.Should().NotBe(original.Id);
        copy.IsPending.Should().BeTrue();
        copy.UploadedBy.Should().Be("user-2");
        copy.UploadedOn.Should().Be(Later);
        copy.ContentType.Should().Be(original.ContentType);
        copy.Sha256.Should().Be(original.Sha256);
        copy.Size.Should().Be(original.Size);
        copy.Width.Should().Be(640);
        copy.Height.Should().Be(480);
    }
}
