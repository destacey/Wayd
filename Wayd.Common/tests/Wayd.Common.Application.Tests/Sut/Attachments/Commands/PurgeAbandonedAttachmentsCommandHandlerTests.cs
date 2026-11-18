using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Attachments.Commands;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Attachments;
using Wayd.Tests.Shared;

namespace Wayd.Common.Application.Tests.Sut.Attachments.Commands;

public sealed class PurgeAbandonedAttachmentsCommandHandlerTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 17, 12, 15);
    private static readonly Instant Cutoff = Now - Duration.FromHours(24);

    private readonly FakeAttachmentsDbContext _dbContext = new();
    private readonly FakeAttachmentStore _store = new();

    private PurgeAbandonedAttachmentsCommandHandler CreateHandler() => new(
        _dbContext,
        _store,
        new TestingDateTimeProvider(new FakeClock(Now)),
        NullLogger<PurgeAbandonedAttachmentsCommandHandler>.Instance);

    private Attachment Seed(Instant uploadedOn, bool claimed = false)
    {
        var attachment = Attachment.CreatePending(AttachmentImageTypes.Png, 1, 1, 1, new string('c', 64), "user-1", uploadedOn);
        if (claimed)
        {
            attachment.ClaimFor("test.purge-attachments.gadget", Guid.NewGuid(), uploadedOn);
        }

        _dbContext.Add(attachment);
        _store.Write(attachment.Id, [1]);
        return attachment;
    }

    [Fact]
    public async Task Handle_ShouldDeleteOnlyUploadsLeftUnclaimedForMoreThanADay()
    {
        // Arrange
        Seed(Cutoff - Duration.FromSeconds(1));
        Seed(Cutoff - Duration.FromDays(30));
        var recent = Seed(Cutoff);
        var claimedLongAgo = Seed(Cutoff - Duration.FromDays(30), claimed: true);

        // Act
        var result = await CreateHandler().Handle(new PurgeAbandonedAttachmentsCommand(), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().Be(2);
        _dbContext.All.Should().BeEquivalentTo([recent, claimedLongAgo]);
        _store.Data.Keys.Should().BeEquivalentTo([recent.Id, claimedLongAgo.Id]);
    }

    [Fact]
    public async Task Handle_ShouldSaveEachBatch_UntilABatchComesBackShort()
    {
        // Arrange
        for (var i = 0; i < PurgeAbandonedAttachmentsCommandHandler.BatchSize + 5; i++)
        {
            Seed(Cutoff - Duration.FromHours(1 + i));
        }

        // Act
        var result = await CreateHandler().Handle(new PurgeAbandonedAttachmentsCommand(), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().Be(PurgeAbandonedAttachmentsCommandHandler.BatchSize + 5);
        _dbContext.All.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldStopWithoutFailing_WhenABatchConflictsWithASaveThatClaimedPartOfIt()
    {
        // Arrange — the second batch's delete conflicts: a save claimed one of its uploads after it was read.
        for (var i = 0; i < PurgeAbandonedAttachmentsCommandHandler.BatchSize + 5; i++)
        {
            Seed(Cutoff - Duration.FromHours(1 + i));
        }

        _dbContext.ConflictOnSave = 2;

        // Act
        var result = await CreateHandler().Handle(new PurgeAbandonedAttachmentsCommand(), TestContext.Current.CancellationToken);

        // Assert — only the batch that saved counts; the rest wait for the next run.
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(PurgeAbandonedAttachmentsCommandHandler.BatchSize);
        _dbContext.SaveChangesCallCount.Should().Be(2);
    }
}
