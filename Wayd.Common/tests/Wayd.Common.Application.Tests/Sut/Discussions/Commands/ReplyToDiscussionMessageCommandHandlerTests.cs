using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Discussions;
using Wayd.Common.Application.Discussions.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Discussions;
using Wayd.Tests.Shared;

namespace Wayd.Common.Application.Tests.Sut.Discussions.Commands;

public sealed class ReplyToDiscussionMessageCommandHandlerTests
{
    private const string GadgetType = "test.discussion-reply.gadget";
    private static readonly DiscussionChannelTypeDescriptor Gadget = new(GadgetType, "Gadget", "Gadgets", supportsInternalNotes: true);
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 29, 9, 30);

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly FakeDiscussionChannelType _gadgets = new(GadgetType);
    private readonly DiscussionWriter _writer;

    public ReplyToDiscussionMessageCommandHandlerTests()
    {
        DiscussionChannelTypes.Register(Gadget);
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        _writer = new DiscussionWriter(_dbContext, [_gadgets], principal.Object, Mock.Of<ICurrentUser>(u => u.GetUserId() == "user-1"), new TestingDateTimeProvider(new FakeClock(Now)));
    }

    [Fact]
    public async Task Handle_ShouldSaveTheReply()
    {
        // Arrange
        var ownerId = _gadgets.AddRecord();
        var parentId = (await _writer.Post(new PostDiscussionMessage(GadgetType, ownerId, DiscussionMessageKind.Public, null, "Question"), TestContext.Current.CancellationToken)).Value;

        // Act
        var result = await new ReplyToDiscussionMessageCommandHandler(_writer, _dbContext)
            .Handle(new ReplyToDiscussionMessageCommand(GadgetType, ownerId, parentId, "Answer"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _dbContext.DiscussionMessages.Should().ContainSingle(m => m.Id == result.Value && m.ParentMessageId == parentId);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldReportAConflict_WhenWhatTheReplyChangesOnTheRecordCollides()
    {
        // Arrange — the same participant added by two replies at once
        var ownerId = _gadgets.AddRecord();
        var parentId = (await _writer.Post(new PostDiscussionMessage(GadgetType, ownerId, DiscussionMessageKind.Public, null, "Question"), TestContext.Current.CancellationToken)).Value;
        _dbContext.FailNextSaveWith = new DbUpdateException("Duplicate participant.");

        // Act
        var result = await new ReplyToDiscussionMessageCommandHandler(_writer, _dbContext)
            .Handle(new ReplyToDiscussionMessageCommand(GadgetType, ownerId, parentId, "Answer"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionAccessErrors.Conflict);
    }
}
