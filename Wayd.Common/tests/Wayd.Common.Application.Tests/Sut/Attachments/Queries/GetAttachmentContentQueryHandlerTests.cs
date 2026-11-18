using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Attachments;
using Wayd.Common.Application.Attachments.Dtos;
using Wayd.Common.Application.Attachments.Queries;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Attachments;
using Wayd.Common.Domain.Following;

namespace Wayd.Common.Application.Tests.Sut.Attachments.Queries;

public sealed class GetAttachmentContentQueryHandlerTests
{
    private const string GadgetType = "test.attachment-content.gadget";
    private const string Me = "user-me";
    private static readonly FollowOwnerDescriptor Gadget = new(GadgetType, "Gadget");
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 17, 12, 0);

    private readonly FakeAttachmentsDbContext _dbContext = new();
    private readonly FakeAttachmentStore _store = new();
    private readonly FakeRecordReadRule _readRule = new(GadgetType);
    private readonly Mock<ICurrentUser> _currentUser = new();

    public GetAttachmentContentQueryHandlerTests()
    {
        FollowOwners.Register(Gadget);
        _currentUser.Setup(u => u.GetUserId()).Returns(Me);
    }

    private GetAttachmentContentQueryHandler CreateHandler() =>
        new(_dbContext, _store, new AttachmentAccess(_currentUser.Object, [_readRule]));

    private Attachment Seed(string uploadedBy, string? ownerType = null, Guid? ownerId = null)
    {
        var attachment = Attachment.CreatePending(AttachmentImageTypes.Jpeg, 3, 1, 1, new string('b', 64), uploadedBy, Now);
        if (ownerId is { } id)
        {
            attachment.ClaimFor(ownerType!, id, Now);
        }

        _dbContext.Add(attachment);
        _store.Write(attachment.Id, [7, 8, 9]);
        return attachment;
    }

    private Task<AttachmentContentDto?> Get(Attachment attachment) =>
        CreateHandler().Handle(new GetAttachmentContentQuery(attachment.Id), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Handle_ShouldReturnAPendingUpload_ToItsUploader()
    {
        // Arrange
        var attachment = Seed(Me);

        // Act
        var content = await Get(attachment);

        // Assert
        content.Should().NotBeNull();
        content!.Data.Should().Equal(7, 8, 9);
        content.ContentType.Should().Be(AttachmentImageTypes.Jpeg);
        content.ETag.Should().Be(new string('b', 64));
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_ForAnotherUsersPendingUpload()
    {
        // Arrange
        var attachment = Seed("user-someone");

        // Act
        var content = await Get(attachment);

        // Assert
        content.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnAClaimedImage_ToAnyoneWhoCanReadItsRecord()
    {
        // Arrange
        var record = _readRule.Add("G-1", "Readable");
        var attachment = Seed("user-someone", GadgetType, record.Id);

        // Act
        var content = await Get(attachment);

        // Assert
        content.Should().NotBeNull();
        content!.Data.Should().Equal(7, 8, 9);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenTheCallerCannotReadTheRecord_EvenAsItsUploader()
    {
        // Arrange — uploading an image gives no lasting access: once claimed, the record decides.
        var record = _readRule.Add("G-1", "Hidden");
        _readRule.Hide(record.Id);
        var attachment = Seed(Me, GadgetType, record.Id);

        // Act
        var content = await Get(attachment);

        // Assert
        content.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenTheRecordNoLongerExists()
    {
        // Arrange
        var attachment = Seed(Me, GadgetType, Guid.NewGuid());

        // Act
        var content = await Get(attachment);

        // Assert
        content.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenTheOwnerTypeHasNoReadRule()
    {
        // Arrange
        var attachment = Seed(Me, "test.attachment-content.unread", Guid.NewGuid());

        // Act
        var content = await Get(attachment);

        // Assert
        content.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenNoAttachmentHasTheId()
    {
        // Act
        var content = await CreateHandler().Handle(new GetAttachmentContentQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        content.Should().BeNull();
    }
}
