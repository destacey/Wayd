using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Discussions;

namespace Wayd.Common.Domain.Tests.Sut.Discussions;

public sealed class DiscussionMessageTests
{
    private static readonly DiscussionChannelTypeDescriptor Gadget = new("test.discussions.message", "Gadget", "Gadgets", supportsInternalNotes: true);
    private static readonly DiscussionChannelTypeDescriptor Plain = new("test.discussions.message-plain", "Plain", "Plains", supportsInternalNotes: false, supportsSubjects: false);
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 29, 14, 30);
    private static readonly Instant Later = Now.Plus(Duration.FromHours(2));
    private static readonly EventActor Actor = EventActor.User("user-1", Guid.NewGuid());

    private readonly Guid _authorId = Guid.NewGuid();

    public DiscussionMessageTests()
    {
        DiscussionChannelTypes.Register(Gadget, Plain);
    }

    [Fact]
    public void Post_ShouldPostATopLevelMessageWithItsSubjectAndMentions()
    {
        // Arrange
        var channel = OpenChannel(Gadget);
        var dana = Guid.NewGuid();
        var tom = Guid.NewGuid();

        // Act
        var result = DiscussionMessage.Post(channel, DiscussionMessageKind.Public, _authorId, "  Which fields?  ", "  Which fields does finance need?  ",
            [dana, tom, dana, _authorId, Guid.Empty], Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var message = result.Value;
        message.ChannelId.Should().Be(channel.Id);
        message.ParentMessageId.Should().BeNull();
        message.IsTopLevel.Should().BeTrue();
        message.Kind.Should().Be(DiscussionMessageKind.Public);
        message.Subject.Should().Be("Which fields?");
        message.Body.Should().Be("Which fields does finance need?");
        message.AuthorEmployeeId.Should().Be(_authorId);
        message.PostedOn.Should().Be(Now);
        message.EditedOn.Should().BeNull();
        // Duplicates, the empty id and the author are dropped: mentioning yourself tells nobody anything.
        message.Mentions.Select(m => m.EmployeeId).Should().BeEquivalentTo([dana, tom]);
        message.Mentions.Should().OnlyContain(m => m.MessageId == message.Id && m.MentionedOn == Now);

        var posted = message.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<DiscussionMessagePostedEvent>().Subject;
        posted.Id.Should().Be(message.Id);
        posted.ChannelId.Should().Be(channel.Id);
        posted.ChannelType.Should().Be(Gadget.Key);
        posted.OwnerId.Should().Be(channel.OwnerId);
        posted.ParentMessageId.Should().BeNull();
        posted.Kind.Should().Be(DiscussionMessageKind.Public);
        posted.AuthorEmployeeId.Should().Be(_authorId);
        posted.MentionedEmployeeIds.Should().BeEquivalentTo([dana, tom]);
        posted.Timestamp.Should().Be(Now);
    }

    [Fact]
    public void Post_ShouldStoreNoSubject_WhenTheSubjectIsBlank()
    {
        // Arrange
        var channel = OpenChannel(Gadget);

        // Act
        var result = DiscussionMessage.Post(channel, DiscussionMessageKind.Public, _authorId, "   ", "Hello", [], Actor, Now);

        // Assert
        result.Value.Subject.Should().BeNull();
    }

    [Fact]
    public void Post_ShouldPostAnInternalNote_WhenTheChannelTypeSupportsThem()
    {
        // Arrange
        var channel = OpenChannel(Gadget);

        // Act
        var result = DiscussionMessage.Post(channel, DiscussionMessageKind.InternalNote, _authorId, null, "Between us", [], Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Kind.Should().Be(DiscussionMessageKind.InternalNote);
        result.Value.DomainEvents.OfType<DiscussionMessagePostedEvent>().Single().Kind.Should().Be(DiscussionMessageKind.InternalNote);
    }

    [Fact]
    public void Post_ShouldFail_ForAnInternalNote_WhenTheChannelTypeHasNone()
    {
        // Arrange
        var channel = OpenChannel(Plain);

        // Act
        var result = DiscussionMessage.Post(channel, DiscussionMessageKind.InternalNote, _authorId, null, "Between us", [], Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionErrors.InternalNotesNotSupported);
    }

    [Fact]
    public void Post_ShouldFail_WhenTheChannelIsLocked()
    {
        // Arrange
        var channel = OpenChannel(Gadget);
        channel.Lock(Guid.NewGuid(), Actor, Now);

        // Act
        var result = DiscussionMessage.Post(channel, DiscussionMessageKind.Public, _authorId, null, "Hello", [], Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionErrors.ChannelLocked);
    }

    [Theory]
    [InlineData("", DiscussionErrors.BodyRequired)]
    [InlineData("   ", DiscussionErrors.BodyRequired)]
    public void Post_ShouldFail_WhenTheBodyIsBlank(string body, string error)
    {
        // Arrange
        var channel = OpenChannel(Gadget);

        // Act
        var result = DiscussionMessage.Post(channel, DiscussionMessageKind.Public, _authorId, null, body, [], Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Post_ShouldAcceptABodyAtTheLimit_AndFailOneCharacterOver()
    {
        // Arrange
        var channel = OpenChannel(Gadget);

        // Act
        var atLimit = DiscussionMessage.Post(channel, DiscussionMessageKind.Public, _authorId, null, new string('a', DiscussionMessage.BodyMaxLength), [], Actor, Now);
        var over = DiscussionMessage.Post(channel, DiscussionMessageKind.Public, _authorId, null, new string('a', DiscussionMessage.BodyMaxLength + 1), [], Actor, Now);

        // Assert
        atLimit.IsSuccess.Should().BeTrue();
        over.IsFailure.Should().BeTrue();
        over.Error.Should().Be(DiscussionErrors.BodyTooLong);
    }

    [Fact]
    public void Post_ShouldAcceptASubjectAtTheLimit_AndFailOneCharacterOver()
    {
        // Arrange
        var channel = OpenChannel(Gadget);

        // Act
        var atLimit = DiscussionMessage.Post(channel, DiscussionMessageKind.Public, _authorId, new string('s', DiscussionMessage.SubjectMaxLength), "Hello", [], Actor, Now);
        var over = DiscussionMessage.Post(channel, DiscussionMessageKind.Public, _authorId, new string('s', DiscussionMessage.SubjectMaxLength + 1), "Hello", [], Actor, Now);

        // Assert
        atLimit.IsSuccess.Should().BeTrue();
        over.IsFailure.Should().BeTrue();
        over.Error.Should().Be(DiscussionErrors.SubjectTooLong);
    }

    [Fact]
    public void Post_ShouldFail_ForASubject_WhenTheChannelTypeHasNone()
    {
        // Arrange
        var channel = OpenChannel(Plain);

        // Act
        var result = DiscussionMessage.Post(channel, DiscussionMessageKind.Public, _authorId, "Topic", "Hello", [], Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionErrors.SubjectNotAllowed);
    }

    [Fact]
    public void Reply_ShouldReplyInTheThreadWithTheParentsKind()
    {
        // Arrange
        var channel = OpenChannel(Gadget);
        var note = PostTopLevel(channel, DiscussionMessageKind.InternalNote);
        var replierId = Guid.NewGuid();

        // Act
        var result = DiscussionMessage.Reply(channel, note, replierId, " Partly. ", [_authorId], Actor, Later);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var reply = result.Value;
        reply.ParentMessageId.Should().Be(note.Id);
        reply.IsTopLevel.Should().BeFalse();
        // A thread under an internal note is internal throughout.
        reply.Kind.Should().Be(DiscussionMessageKind.InternalNote);
        reply.Subject.Should().BeNull();
        reply.Body.Should().Be("Partly.");
        reply.AuthorEmployeeId.Should().Be(replierId);
        reply.Mentions.Select(m => m.EmployeeId).Should().Equal(_authorId);

        var posted = reply.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<DiscussionMessagePostedEvent>().Subject;
        posted.ParentMessageId.Should().Be(note.Id);
        posted.Kind.Should().Be(DiscussionMessageKind.InternalNote);
    }

    [Fact]
    public void Reply_ShouldFail_ForAReplyToAReply()
    {
        // Arrange
        var channel = OpenChannel(Gadget);
        var first = PostTopLevel(channel, DiscussionMessageKind.Public);
        var reply = DiscussionMessage.Reply(channel, first, Guid.NewGuid(), "One level", [], Actor, Now).Value;

        // Act
        var result = DiscussionMessage.Reply(channel, reply, Guid.NewGuid(), "Two levels", [], Actor, Later);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionErrors.ReplyToReply);
    }

    [Fact]
    public void Reply_ShouldFail_WhenTheParentIsInAnotherChannel()
    {
        // Arrange
        var parent = PostTopLevel(OpenChannel(Gadget), DiscussionMessageKind.Public);

        // Act
        var result = DiscussionMessage.Reply(OpenChannel(Gadget), parent, Guid.NewGuid(), "Hello", [], Actor, Later);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionErrors.MessageInAnotherChannel);
    }

    [Fact]
    public void Reply_ShouldFail_WhenTheChannelIsLocked()
    {
        // Arrange
        var channel = OpenChannel(Gadget);
        var parent = PostTopLevel(channel, DiscussionMessageKind.Public);
        channel.Lock(Guid.NewGuid(), Actor, Now);

        // Act
        var result = DiscussionMessage.Reply(channel, parent, Guid.NewGuid(), "Hello", [], Actor, Later);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionErrors.ChannelLocked);
    }

    [Fact]
    public void Edit_ShouldKeepTheReplacedVersionAndRecordTheMentionChange()
    {
        // Arrange
        var channel = OpenChannel(Gadget);
        var dana = Guid.NewGuid();
        var tom = Guid.NewGuid();
        var message = DiscussionMessage.Post(channel, DiscussionMessageKind.Public, _authorId, "Fields", "First draft", [dana], Actor, Now).Value;
        message.ClearDomainEvents();

        // Act
        var result = message.Edit(channel, _authorId, "Fields and timing", "Second draft", [tom], Actor, Later);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        message.Subject.Should().Be("Fields and timing");
        message.Body.Should().Be("Second draft");
        message.EditedOn.Should().Be(Later);
        var revision = message.Revisions.Should().ContainSingle().Subject;
        revision.Subject.Should().Be("Fields");
        revision.Body.Should().Be("First draft");
        revision.ReplacedOn.Should().Be(Later);
        message.Mentions.Select(m => m.EmployeeId).Should().Equal(tom);

        var edited = message.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<DiscussionMessageEditedEvent>().Subject;
        edited.Id.Should().Be(message.Id);
        edited.TextChanged.Should().BeTrue();
        edited.AddedMentionIds.Should().Equal(tom);
        edited.RemovedMentionIds.Should().Equal(dana);
        edited.MentionedEmployeeIds.Should().Equal(tom);
        edited.Timestamp.Should().Be(Later);
    }

    [Fact]
    public void Edit_ShouldKeepNoRevision_WhenOnlyTheMentionsChange()
    {
        // Arrange
        var channel = OpenChannel(Gadget);
        var message = DiscussionMessage.Post(channel, DiscussionMessageKind.Public, _authorId, null, "Same text", [], Actor, Now).Value;
        message.ClearDomainEvents();
        var tom = Guid.NewGuid();

        // Act
        var result = message.Edit(channel, _authorId, null, "Same text", [tom], Actor, Later);

        // Assert
        result.IsSuccess.Should().BeTrue();
        message.Revisions.Should().BeEmpty();
        message.EditedOn.Should().BeNull();
        var edited = message.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<DiscussionMessageEditedEvent>().Subject;
        edited.TextChanged.Should().BeFalse();
        edited.AddedMentionIds.Should().Equal(tom);
    }

    [Fact]
    public void Edit_ShouldRecordNothing_WhenNothingChanged()
    {
        // Arrange
        var channel = OpenChannel(Gadget);
        var dana = Guid.NewGuid();
        var message = DiscussionMessage.Post(channel, DiscussionMessageKind.Public, _authorId, "Topic", "Same text", [dana], Actor, Now).Value;
        message.ClearDomainEvents();

        // Act
        var result = message.Edit(channel, _authorId, " Topic ", " Same text ", [dana], Actor, Later);

        // Assert
        result.IsSuccess.Should().BeTrue();
        message.Revisions.Should().BeEmpty();
        message.EditedOn.Should().BeNull();
        message.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Edit_ShouldFail_WhenTheEditorIsNotTheAuthor()
    {
        // Arrange
        var channel = OpenChannel(Gadget);
        var message = PostTopLevel(channel, DiscussionMessageKind.Public);

        // Act
        var result = message.Edit(channel, Guid.NewGuid(), null, "Rewritten", [], Actor, Later);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionErrors.NotTheAuthor);
        message.Body.Should().Be("Original");
        message.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Edit_ShouldFail_WhenTheChannelIsLocked()
    {
        // Arrange
        var channel = OpenChannel(Gadget);
        var message = PostTopLevel(channel, DiscussionMessageKind.Public);
        channel.Lock(Guid.NewGuid(), Actor, Now);

        // Act
        var result = message.Edit(channel, _authorId, null, "Rewritten", [], Actor, Later);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionErrors.ChannelLocked);
        message.Body.Should().Be("Original");
    }

    [Fact]
    public void Edit_ShouldFail_ForASubjectOnAReply()
    {
        // Arrange
        var channel = OpenChannel(Gadget);
        var parent = PostTopLevel(channel, DiscussionMessageKind.Public);
        var reply = DiscussionMessage.Reply(channel, parent, _authorId, "Reply", [], Actor, Now).Value;

        // Act
        var result = reply.Edit(channel, _authorId, "A subject", "Reply", [], Actor, Later);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionErrors.SubjectNotAllowed);
        reply.Subject.Should().BeNull();
    }

    [Fact]
    public void Edit_ShouldFail_WhenTheMessageIsDeleted()
    {
        // Arrange
        var channel = OpenChannel(Gadget);
        var message = PostTopLevel(channel, DiscussionMessageKind.Public);
        message.Delete(channel, _authorId, Actor, Now);

        // Act
        var result = message.Edit(channel, _authorId, null, "Back again", [], Actor, Later);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionErrors.MessageDeleted);
        message.Body.Should().BeNull();
    }

    [Fact]
    public void Delete_ShouldClearTheTextAndHistoryAndLeaveATombstone()
    {
        // Arrange
        var channel = OpenChannel(Gadget);
        var message = DiscussionMessage.Post(channel, DiscussionMessageKind.Public, _authorId, "Topic", "First", [Guid.NewGuid()], Actor, Now).Value;
        message.Edit(channel, _authorId, "Topic", "Second", [], Actor, Now);
        message.ClearDomainEvents();
        var moderatorId = Guid.NewGuid();

        // Act
        var result = message.Delete(channel, moderatorId, Actor, Later);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        message.IsDeleted.Should().BeTrue();
        message.Subject.Should().BeNull();
        message.Body.Should().BeNull();
        message.Revisions.Should().BeEmpty();
        message.Mentions.Should().BeEmpty();
        message.DeletedOn.Should().Be(Later);
        message.DeletedById.Should().Be(moderatorId);
        // The tombstone still says who wrote it and when.
        message.AuthorEmployeeId.Should().Be(_authorId);
        message.PostedOn.Should().Be(Now);

        var deleted = message.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<DiscussionMessageDeletedEvent>().Subject;
        deleted.Id.Should().Be(message.Id);
        deleted.ChannelType.Should().Be(Gadget.Key);
        deleted.DeletedById.Should().Be(moderatorId);
        deleted.Timestamp.Should().Be(Later);
    }

    [Fact]
    public void Delete_ShouldFail_WhenAlreadyDeleted()
    {
        // Arrange
        var channel = OpenChannel(Gadget);
        var message = PostTopLevel(channel, DiscussionMessageKind.Public);
        var firstDeleterId = Guid.NewGuid();
        message.Delete(channel, firstDeleterId, Actor, Now);
        message.ClearDomainEvents();

        // Act
        var result = message.Delete(channel, Guid.NewGuid(), Actor, Later);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionErrors.MessageDeleted);
        message.DeletedById.Should().Be(firstDeleterId);
        message.DomainEvents.Should().BeEmpty();
    }

    private static DiscussionChannel OpenChannel(DiscussionChannelTypeDescriptor type) =>
        DiscussionChannel.OpenForRecord(type.Key, Guid.NewGuid(), Actor, Now).Value;

    private DiscussionMessage PostTopLevel(DiscussionChannel channel, DiscussionMessageKind kind)
    {
        var message = DiscussionMessage.Post(channel, kind, _authorId, null, "Original", [], Actor, Now).Value;
        message.ClearDomainEvents();
        return message;
    }
}
