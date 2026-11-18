using FluentAssertions;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Attachments;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Attachments;
using Wayd.Common.Domain.Following;
using Wayd.Tests.Shared;

namespace Wayd.Common.Application.Tests.Sut.Attachments;

public sealed class MarkdownAttachmentsTests
{
    private const string GadgetType = "test.attachments.gadget";
    private const string Me = "user-me";
    private const string Someone = "user-someone";
    private static readonly FollowOwnerDescriptor Gadget = new(GadgetType, "Gadget");
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 17, 12, 0);

    private readonly FakeAttachmentsDbContext _dbContext = new();
    private readonly FakeAttachmentStore _store = new();
    private readonly FakeRecordReadRule _readRule = new(GadgetType);
    private readonly Mock<ICurrentUser> _currentUser = new();

    public MarkdownAttachmentsTests()
    {
        FollowOwners.Register(Gadget);
        _currentUser.Setup(u => u.GetUserId()).Returns(Me);
    }

    private MarkdownAttachments CreateSut() => new(
        _dbContext,
        _store,
        new AttachmentAccess(_currentUser.Object, [_readRule]),
        _currentUser.Object,
        new TestingDateTimeProvider(new FakeClock(Now)));

    private Attachment Seed(string uploadedBy, Guid? ownerId = null)
    {
        var attachment = Attachment.CreatePending(AttachmentImageTypes.Png, 3, 1, 1, new string('a', 64), uploadedBy, Now - Duration.FromHours(1));
        if (ownerId is { } id)
        {
            attachment.ClaimFor(GadgetType, id, Now - Duration.FromMinutes(30));
        }

        _dbContext.Add(attachment);
        _store.Write(attachment.Id, [1, 2, 3]);
        return attachment;
    }

    private static string Image(Attachment attachment) => $"![]({AttachmentReferences.PathFor(attachment.Id)})";

    #region CopyForeign

    [Fact]
    public async Task CopyForeign_ShouldPointAnotherRecordsReadableImageAtANewCopy_PendingForTheCaller()
    {
        // Arrange
        var other = _readRule.Add("G-1", "Other");
        var theirs = Seed(Someone, other.Id);

        // Act
        var markdown = await CreateSut().CopyForeign(GadgetType, Guid.NewGuid(), [$"See {Image(theirs)}", null], TestContext.Current.CancellationToken);

        // Assert
        var copy = _dbContext.All.Single(a => a.Id != theirs.Id);
        copy.IsPending.Should().BeTrue();
        copy.UploadedBy.Should().Be(Me);
        markdown.Should().Equal($"See {Image(copy)}", null);
        _store.Data[copy.Id].Should().Equal(1, 2, 3);
        theirs.IsOwnedBy(GadgetType, other.Id).Should().BeTrue();
    }

    [Fact]
    public async Task CopyForeign_ShouldLeaveTheCallersPendingUploadsAndTheRecordsOwnImagesAsTheyAre()
    {
        // Arrange
        var record = _readRule.Add("G-1", "Record");
        var mine = Seed(Me);
        var owned = Seed(Someone, record.Id);
        var text = $"{Image(mine)} {Image(owned)}";

        // Act
        var markdown = await CreateSut().CopyForeign(GadgetType, record.Id, [text], TestContext.Current.CancellationToken);

        // Assert
        markdown.Should().Equal(text);
        _dbContext.All.Should().HaveCount(2);
    }

    [Fact]
    public async Task CopyForeign_ShouldNotCopyAnImageTheCallerCannotRead()
    {
        // Arrange — another user's pending upload, and an image on a record hidden from the caller.
        var hidden = _readRule.Add("G-2", "Hidden");
        _readRule.Hide(hidden.Id);
        var theirPending = Seed(Someone);
        var onHidden = Seed(Someone, hidden.Id);
        var text = $"{Image(theirPending)} {Image(onHidden)}";

        // Act
        var markdown = await CreateSut().CopyForeign(GadgetType, null, [text], TestContext.Current.CancellationToken);

        // Assert
        markdown.Should().Equal(text);
        _dbContext.All.Should().HaveCount(2);
    }

    [Fact]
    public async Task CopyForeign_ShouldThrow_ForAnOwnerTypeWithNoReadRule()
    {
        // Act
        var copy = () => CreateSut().CopyForeign("test.attachments.unread", null, ["text"], TestContext.Current.CancellationToken);

        // Assert
        await copy.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion CopyForeign

    #region Claim

    [Fact]
    public async Task Claim_ShouldGiveTheRecordTheCallersPendingUploads_AndCopiesMadeForIt()
    {
        // Arrange
        var other = _readRule.Add("G-1", "Other");
        var recordId = Guid.NewGuid();
        var mine = Seed(Me);
        var theirs = Seed(Someone, other.Id);
        var sut = CreateSut();
        var markdown = await sut.CopyForeign(GadgetType, null, [$"{Image(mine)} {Image(theirs)}"], TestContext.Current.CancellationToken);

        // Act
        await sut.Claim(GadgetType, recordId, markdown, TestContext.Current.CancellationToken);

        // Assert
        var copy = _dbContext.All.Single(a => a.Id != mine.Id && a.Id != theirs.Id);
        mine.IsOwnedBy(GadgetType, recordId).Should().BeTrue();
        copy.IsOwnedBy(GadgetType, recordId).Should().BeTrue();
        copy.ClaimedOn.Should().Be(Now);
        theirs.IsOwnedBy(GadgetType, other.Id).Should().BeTrue();
    }

    [Fact]
    public async Task Claim_ShouldLeaveAnotherUsersPendingUploadPending()
    {
        // Arrange
        var theirPending = Seed(Someone);

        // Act
        await CreateSut().Claim(GadgetType, Guid.NewGuid(), [Image(theirPending)], TestContext.Current.CancellationToken);

        // Assert
        theirPending.IsPending.Should().BeTrue();
    }

    [Fact]
    public async Task Claim_ShouldDeleteTheRecordsImagesItsTextNoLongerRefersTo_AndKeepTheRest()
    {
        // Arrange
        var recordId = Guid.NewGuid();
        var kept = Seed(Someone, recordId);
        var dropped = Seed(Someone, recordId);
        var otherRecords = Seed(Someone, Guid.NewGuid());

        // Act
        await CreateSut().Claim(GadgetType, recordId, [Image(kept), null], TestContext.Current.CancellationToken);

        // Assert
        _dbContext.All.Should().BeEquivalentTo([kept, otherRecords]);
        _store.Data.Keys.Should().BeEquivalentTo([kept.Id, otherRecords.Id]);
    }

    #endregion Claim

    [Fact]
    public async Task RemoveAll_ShouldDeleteEveryImageTheRecordHolds_AndNoOtherRecords()
    {
        // Arrange
        var recordId = Guid.NewGuid();
        Seed(Someone, recordId);
        Seed(Me, recordId);
        var otherRecords = Seed(Someone, Guid.NewGuid());
        var pending = Seed(Me);

        // Act
        await CreateSut().RemoveAll(GadgetType, recordId, TestContext.Current.CancellationToken);

        // Assert
        _dbContext.All.Should().BeEquivalentTo([otherRecords, pending]);
        _store.Data.Keys.Should().BeEquivalentTo([otherRecords.Id, pending.Id]);
    }
}
