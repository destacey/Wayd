using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Attachments;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Attachments;
using Wayd.Common.Domain.Following;
using Wayd.Infrastructure.Attachments;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Attachments;

/// <summary>
/// Attachments against real SQL Server: what the in-memory fakes cannot see — the bytes kept in their own table and
/// written by the same save as the row, a copy read back before it is saved, and a dropped image's bytes deleted
/// with it.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class AttachmentPersistenceIntegrationTests(SqlServerDbContextFixture fixture)
{
    private const string GadgetType = "test.attachment-persistence.gadget";
    private const string UserId = "user-attachment-persistence";
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 17, 12, 0);

    static AttachmentPersistenceIntegrationTests()
    {
        FollowOwners.Register(new FollowOwnerDescriptor(GadgetType, "Gadget"));
    }

    private static MarkdownAttachments CreateSut(WaydDbContext context)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(UserId);
        var readRule = new Mock<IRecordReadRule>();
        readRule.Setup(r => r.OwnerType).Returns(GadgetType);

        return new MarkdownAttachments(
            context,
            new DatabaseAttachmentStore(context),
            new AttachmentAccess(currentUser.Object, [readRule.Object]),
            currentUser.Object,
            Mock.Of<IDateTimeProvider>(d => d.Now == Now));
    }

    private async Task<Attachment> Upload(byte[] data)
    {
        await using var context = fixture.CreateContext();
        var attachment = Attachment.CreatePending(AttachmentImageTypes.Png, data.Length, 1, 1, new string('d', 64), UserId, Now);
        context.Attachments.Add(attachment);
        new DatabaseAttachmentStore(context).Write(attachment.Id, data);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return attachment;
    }

    [Fact]
    public async Task Claim_ShouldGiveTheUploadToTheRecord_WithItsBytesReadableAfterwards()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var upload = await Upload([1, 2, 3, 4]);
        var recordId = Guid.CreateVersion7();

        // Act
        await using (var context = fixture.CreateContext())
        {
            await CreateSut(context).Claim(GadgetType, recordId, [$"![]({AttachmentReferences.PathFor(upload.Id)})"], cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        // Assert
        await using var reader = fixture.CreateContext();
        var stored = await reader.Attachments.SingleAsync(a => a.Id == upload.Id, cancellationToken);
        stored.IsOwnedBy(GadgetType, recordId).Should().BeTrue();
        stored.ClaimedOn.Should().Be(Now);
        (await new DatabaseAttachmentStore(reader).Read(upload.Id, cancellationToken)).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public async Task Claim_ShouldDeleteADroppedImageAndItsBytes_InTheSameSave()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var kept = await Upload([1]);
        var dropped = await Upload([2]);
        var recordId = Guid.CreateVersion7();
        await using (var context = fixture.CreateContext())
        {
            await CreateSut(context).Claim(
                GadgetType,
                recordId,
                [$"![]({AttachmentReferences.PathFor(kept.Id)}) ![]({AttachmentReferences.PathFor(dropped.Id)})"],
                cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        // Act
        await using (var context = fixture.CreateContext())
        {
            await CreateSut(context).Claim(GadgetType, recordId, [$"![]({AttachmentReferences.PathFor(kept.Id)})"], cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        // Assert
        await using var reader = fixture.CreateContext();
        (await reader.Attachments.Where(a => a.OwnerType == GadgetType && a.OwnerId == recordId).Select(a => a.Id).ToListAsync(cancellationToken))
            .Should().Equal(kept.Id);
        (await reader.AttachmentContents.AnyAsync(c => c.AttachmentId == dropped.Id, cancellationToken)).Should().BeFalse();
        (await reader.AttachmentContents.AnyAsync(c => c.AttachmentId == kept.Id, cancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task Save_ShouldConflict_WhenAnUploadReadAsPendingWasClaimedMeanwhile()
    {
        // Arrange — the sweep reads an upload as pending, then a save claims it before the sweep deletes it.
        var cancellationToken = TestContext.Current.CancellationToken;
        var upload = await Upload([1]);
        var recordId = Guid.CreateVersion7();
        await using var sweep = fixture.CreateContext();
        var readAsPending = await sweep.Attachments.SingleAsync(a => a.Id == upload.Id, cancellationToken);

        await using (var context = fixture.CreateContext())
        {
            await CreateSut(context).Claim(GadgetType, recordId, [$"![]({AttachmentReferences.PathFor(upload.Id)})"], cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        sweep.Attachments.Remove(readAsPending);

        // Act
        var act = () => sweep.SaveChangesAsync(cancellationToken);

        // Assert — the claimed image survives.
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        await using var reader = fixture.CreateContext();
        (await reader.Attachments.SingleAsync(a => a.Id == upload.Id, cancellationToken)).IsOwnedBy(GadgetType, recordId).Should().BeTrue();
    }

    [Fact]
    public async Task Copy_ShouldStoreTheBytesAgainUnderTheCopy_ReadableBeforeAndAfterTheSave()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var source = await Upload([5, 6, 7]);
        Guid copyId;

        // Act
        byte[]? beforeSave;
        await using (var context = fixture.CreateContext())
        {
            var store = new DatabaseAttachmentStore(context);
            var copy = source.CopyFor(UserId, Now);
            context.Attachments.Add(copy);
            await store.Copy(source.Id, copy.Id, cancellationToken);
            beforeSave = await store.Read(copy.Id, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            copyId = copy.Id;
        }

        // Assert
        beforeSave.Should().Equal(5, 6, 7);
        await using var reader = fixture.CreateContext();
        (await new DatabaseAttachmentStore(reader).Read(copyId, cancellationToken)).Should().Equal(5, 6, 7);
        (await new DatabaseAttachmentStore(reader).Read(source.Id, cancellationToken)).Should().Equal(5, 6, 7);
    }
}
