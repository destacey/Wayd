using FluentAssertions;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Discussions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Discussions;
using Wayd.Tests.Shared;

namespace Wayd.Common.Application.Tests.Sut.Discussions;

public sealed class DiscussionWriterTests
{
    private const string GadgetType = "test.discussion-writer.gadget";
    private static readonly DiscussionChannelTypeDescriptor Gadget = new(GadgetType, "Gadget", "Gadgets", supportsInternalNotes: true);
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 29, 9, 30);

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly FakeDiscussionChannelType _gadgets = new(GadgetType);
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Now));
    private readonly Guid _employeeId = Guid.NewGuid();

    public DiscussionWriterTests()
    {
        DiscussionChannelTypes.Register(Gadget);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);
        _currentUser.Setup(u => u.GetUserId()).Returns("user-1");
    }

    private DiscussionWriter CreateWriter() =>
        new(_dbContext, [_gadgets], _currentPrincipal.Object, _currentUser.Object, _dateTimeProvider);

    [Fact]
    public async Task Post_ShouldOpenTheConversationOnTheFirstPost()
    {
        // Arrange
        var ownerId = _gadgets.AddRecord();

        // Act
        var result = await CreateWriter().Post(new PostDiscussionMessage(GadgetType, ownerId, DiscussionMessageKind.Public, "Topic", "Hello"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var channel = _dbContext.DiscussionChannels.Should().ContainSingle().Subject;
        channel.ChannelType.Should().Be(GadgetType);
        channel.OwnerId.Should().Be(ownerId);

        var message = _dbContext.DiscussionMessages.Should().ContainSingle().Subject;
        message.Id.Should().Be(result.Value);
        message.ChannelId.Should().Be(channel.Id);
        message.AuthorEmployeeId.Should().Be(_employeeId);
        message.Subject.Should().Be("Topic");
        message.PostedOn.Should().Be(Now);
        message.DomainEvents.OfType<DiscussionMessagePostedEvent>().Single().Actor.UserId.Should().Be("user-1");
        _gadgets.Posted.Should().ContainSingle().Which.Should().BeSameAs(message);
        _dbContext.SaveChangesCallCount.Should().Be(0, "the writer stages; its caller saves");
    }

    [Fact]
    public async Task Post_ShouldReuseTheConversation_OnLaterPosts()
    {
        // Arrange
        var ownerId = _gadgets.AddRecord();
        var writer = CreateWriter();
        await writer.Post(new PostDiscussionMessage(GadgetType, ownerId, DiscussionMessageKind.Public, null, "First"), TestContext.Current.CancellationToken);

        // Act
        var result = await writer.Post(new PostDiscussionMessage(GadgetType, ownerId, DiscussionMessageKind.Public, null, "Second"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var channel = _dbContext.DiscussionChannels.Should().ContainSingle().Subject;
        _dbContext.DiscussionMessages.Should().HaveCount(2).And.OnlyContain(m => m.ChannelId == channel.Id);
    }

    [Fact]
    public async Task Post_ShouldBeNotFound_WhenTheCallerCannotReadTheRecord()
    {
        // Arrange — a record the channel type does not know, so the caller cannot read it.
        var unreadableId = Guid.NewGuid();

        // Act
        var result = await CreateWriter().Post(new PostDiscussionMessage(GadgetType, unreadableId, DiscussionMessageKind.Public, null, "Hello"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionAccessErrors.NotFound);
        _dbContext.DiscussionChannels.Should().BeEmpty();
        _dbContext.DiscussionMessages.Should().BeEmpty();
    }

    [Theory]
    [InlineData(DiscussionMessageKind.Public, false, true)]
    [InlineData(DiscussionMessageKind.InternalNote, true, false)]
    public async Task Post_ShouldBeRefused_WhenTheCallerCannotWriteThatKind(DiscussionMessageKind kind, bool canPost, bool canPostInternal)
    {
        // Arrange
        var ownerId = _gadgets.AddRecord();
        _gadgets.Access = new DiscussionAccess(CanReadInternal: true, CanPost: canPost, CanPostInternal: canPostInternal, CanModerate: false);

        // Act
        var result = await CreateWriter().Post(new PostDiscussionMessage(GadgetType, ownerId, kind, null, "Hello"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionAccessErrors.CannotWrite);
        _dbContext.DiscussionMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task Post_ShouldKeepOnlyTheMentionsTheChannelTypeAllows()
    {
        // Arrange
        var ownerId = _gadgets.AddRecord();
        var dana = Guid.NewGuid();
        var outsider = Guid.NewGuid();
        _gadgets.Unmentionable.Add(outsider);
        var body = $"Over to @[Dana Ortiz](employee:{dana}) and @[Pat Out](employee:{outsider}).";

        // Act
        var result = await CreateWriter().Post(new PostDiscussionMessage(GadgetType, ownerId, DiscussionMessageKind.Public, null, body), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _dbContext.DiscussionMessages.Single().Mentions.Select(m => m.EmployeeId).Should().Equal(dana);
    }

    [Fact]
    public async Task Reply_ShouldReplyInTheThread()
    {
        // Arrange
        var ownerId = _gadgets.AddRecord();
        var writer = CreateWriter();
        var parentId = (await writer.Post(new PostDiscussionMessage(GadgetType, ownerId, DiscussionMessageKind.InternalNote, null, "Note"), TestContext.Current.CancellationToken)).Value;

        // Act
        var result = await writer.Reply(GadgetType, ownerId, parentId, "Agreed", TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var reply = _dbContext.DiscussionMessages.Single(m => m.Id == result.Value);
        reply.ParentMessageId.Should().Be(parentId);
        reply.Kind.Should().Be(DiscussionMessageKind.InternalNote);
        _gadgets.Posted.Should().HaveCount(2);
    }

    [Fact]
    public async Task Reply_ShouldBeNotFound_ForAnInternalNoteTheCallerCannotRead()
    {
        // Arrange
        var ownerId = _gadgets.AddRecord();
        var writer = CreateWriter();
        var noteId = (await writer.Post(new PostDiscussionMessage(GadgetType, ownerId, DiscussionMessageKind.InternalNote, null, "Note"), TestContext.Current.CancellationToken)).Value;
        _gadgets.Access = new DiscussionAccess(CanReadInternal: false, CanPost: true, CanPostInternal: false, CanModerate: false);

        // Act
        var result = await writer.Reply(GadgetType, ownerId, noteId, "What note?", TestContext.Current.CancellationToken);

        // Assert
        // Not found rather than forbidden, so the reply never confirms the note exists.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionAccessErrors.NotFound);
        _dbContext.DiscussionMessages.Should().ContainSingle();
    }

    [Fact]
    public async Task Edit_ShouldEditTheCallersOwnMessage()
    {
        // Arrange
        var ownerId = _gadgets.AddRecord();
        var writer = CreateWriter();
        var messageId = (await writer.Post(new PostDiscussionMessage(GadgetType, ownerId, DiscussionMessageKind.Public, null, "Draft"), TestContext.Current.CancellationToken)).Value;

        // Act
        var result = await writer.Edit(GadgetType, ownerId, messageId, null, "Final", TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var message = _dbContext.DiscussionMessages.Single();
        message.Body.Should().Be("Final");
        message.Revisions.Should().ContainSingle().Which.Body.Should().Be("Draft");
    }

    [Fact]
    public async Task Edit_ShouldBeRefused_WhenTheCallerCanNoLongerWriteThatKind()
    {
        // Arrange
        var ownerId = _gadgets.AddRecord();
        var writer = CreateWriter();
        var noteId = (await writer.Post(new PostDiscussionMessage(GadgetType, ownerId, DiscussionMessageKind.InternalNote, null, "Draft"), TestContext.Current.CancellationToken)).Value;
        _gadgets.Access = new DiscussionAccess(CanReadInternal: true, CanPost: true, CanPostInternal: false, CanModerate: false);

        // Act
        var result = await writer.Edit(GadgetType, ownerId, noteId, null, "Final", TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionAccessErrors.CannotWrite);
        _dbContext.DiscussionMessages.Single().Body.Should().Be("Draft");
    }

    [Fact]
    public async Task Delete_ShouldLetAModeratorDeleteSomeoneElsesMessage()
    {
        // Arrange
        var ownerId = _gadgets.AddRecord();
        var messageId = await PostAsSomeoneElse(ownerId);
        _gadgets.Access = new DiscussionAccess(CanReadInternal: false, CanPost: false, CanPostInternal: false, CanModerate: true);

        // Act
        var result = await CreateWriter().Delete(GadgetType, ownerId, messageId, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var message = _dbContext.DiscussionMessages.Single();
        message.IsDeleted.Should().BeTrue();
        message.DeletedById.Should().Be(_employeeId);
    }

    [Fact]
    public async Task Delete_ShouldBeRefused_ForSomeoneElsesMessage_WithoutModerating()
    {
        // Arrange
        var ownerId = _gadgets.AddRecord();
        var messageId = await PostAsSomeoneElse(ownerId);
        _gadgets.Access = new DiscussionAccess(CanReadInternal: true, CanPost: true, CanPostInternal: true, CanModerate: false);

        // Act
        var result = await CreateWriter().Delete(GadgetType, ownerId, messageId, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionAccessErrors.CannotModerate);
        _dbContext.DiscussionMessages.Single().IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_ShouldLetTheAuthorDeleteTheirOwnMessage()
    {
        // Arrange
        var ownerId = _gadgets.AddRecord();
        var writer = CreateWriter();
        var messageId = (await writer.Post(new PostDiscussionMessage(GadgetType, ownerId, DiscussionMessageKind.Public, null, "Oops"), TestContext.Current.CancellationToken)).Value;
        _gadgets.Access = new DiscussionAccess(CanReadInternal: false, CanPost: true, CanPostInternal: false, CanModerate: false);

        // Act
        var result = await writer.Delete(GadgetType, ownerId, messageId, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _dbContext.DiscussionMessages.Single().IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task SetLocked_ShouldOpenTheConversationLocked_WhenNobodyHasPosted()
    {
        // Arrange
        var ownerId = _gadgets.AddRecord();

        // Act
        var result = await CreateWriter().SetLocked(GadgetType, ownerId, locked: true, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var channel = _dbContext.DiscussionChannels.Should().ContainSingle().Subject;
        channel.IsLocked.Should().BeTrue();
        channel.LockedById.Should().Be(_employeeId);
    }

    [Fact]
    public async Task SetLocked_ShouldBeRefused_WithoutModerating()
    {
        // Arrange
        var ownerId = _gadgets.AddRecord();
        _gadgets.Access = new DiscussionAccess(CanReadInternal: true, CanPost: true, CanPostInternal: true, CanModerate: false);

        // Act
        var result = await CreateWriter().SetLocked(GadgetType, ownerId, locked: true, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionAccessErrors.CannotModerate);
        _dbContext.DiscussionChannels.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteChannel_ShouldRemoveTheRecordsConversation()
    {
        // Arrange
        var ownerId = _gadgets.AddRecord();
        await CreateWriter().Post(new PostDiscussionMessage(GadgetType, ownerId, DiscussionMessageKind.Public, null, "Hello"), TestContext.Current.CancellationToken);

        var channel = _dbContext.DiscussionChannels.Single();
        channel.ClearDomainEvents();

        // Act
        await CreateWriter().DeleteChannel(GadgetType, ownerId, EventActor.System, TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DiscussionChannels.Should().BeEmpty();
        channel.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<DiscussionChannelDeletedEvent>()
            .Which.OwnerId.Should().Be(ownerId);
    }

    [Fact]
    public async Task DeleteChannel_ShouldThrow_ForAnUnregisteredType()
    {
        // Arrange
        var ownerId = _gadgets.AddRecord();

        // Act
        var act = () => CreateWriter().DeleteChannel("test.discussion-writer.nothing", ownerId, EventActor.System, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Post_ShouldStageNoConversation_WhenTheMessageIsRefused()
    {
        // Arrange
        var ownerId = _gadgets.AddRecord();
        var tooLong = new string('a', DiscussionMessage.BodyMaxLength + 1);

        // Act
        var result = await CreateWriter().Post(new PostDiscussionMessage(GadgetType, ownerId, DiscussionMessageKind.Public, null, tooLong), TestContext.Current.CancellationToken);

        // Assert
        // A caller that saves its own work after a failed post must not store an empty conversation.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionErrors.BodyTooLong);
        _dbContext.DiscussionChannels.Should().BeEmpty();
    }

    [Fact]
    public async Task Edit_ShouldBeRefused_ForSomeoneElsesMessage()
    {
        // Arrange
        var ownerId = _gadgets.AddRecord();
        var messageId = await PostAsSomeoneElse(ownerId);

        // Act
        var result = await CreateWriter().Edit(GadgetType, ownerId, messageId, null, "Rewritten", TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionAccessErrors.NotTheAuthor);
        _dbContext.DiscussionMessages.Single().Body.Should().Be("Theirs");
    }

    private async Task<Guid> PostAsSomeoneElse(Guid ownerId)
    {
        var someoneElse = new Mock<ICurrentPrincipal>();
        someoneElse.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        var writer = new DiscussionWriter(_dbContext, [_gadgets], someoneElse.Object, _currentUser.Object, _dateTimeProvider);
        return (await writer.Post(new PostDiscussionMessage(GadgetType, ownerId, DiscussionMessageKind.Public, null, "Theirs"), TestContext.Current.CancellationToken)).Value;
    }
}
