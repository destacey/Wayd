using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Discussions;
using Wayd.Common.Application.Discussions.Queries;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.Events;

namespace Wayd.Common.Application.Tests.Sut.Discussions.Queries;

public sealed class GetDiscussionRepliesQueryHandlerTests
{
    private const string GadgetType = "test.discussion-replies.gadget";
    private static readonly DiscussionChannelTypeDescriptor Gadget = new(GadgetType, "Gadget", "Gadgets", supportsInternalNotes: true);
    private static readonly Instant Start = Instant.FromUtc(2026, 10, 20, 9, 0);
    private static readonly EventActor Actor = EventActor.User("user-1");

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly FakeDiscussionChannelType _gadgets = new(GadgetType);
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Guid _ownerId;
    private readonly DiscussionChannel _channel;

    public GetDiscussionRepliesQueryHandlerTests()
    {
        DiscussionChannelTypes.Register(Gadget);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        _ownerId = _gadgets.AddRecord();
        _channel = DiscussionChannel.OpenForRecord(GadgetType, _ownerId, Actor, Start).Value;
        _dbContext.AddDiscussionChannel(_channel);
    }

    private GetDiscussionRepliesQueryHandler CreateHandler() => new(_dbContext, [_gadgets], _currentPrincipal.Object);

    [Fact]
    public async Task Handle_ShouldReturnTheThreadsRepliesOldestFirst()
    {
        // Arrange
        var parent = Post(DiscussionMessageKind.Public, Start);
        Reply(parent, "Second", Start.Plus(Duration.FromMinutes(9)));
        Reply(parent, "First", Start.Plus(Duration.FromMinutes(5)));
        Reply(Post(DiscussionMessageKind.Public, Start), "Another thread", Start.Plus(Duration.FromMinutes(7)));

        // Act
        var result = await CreateHandler().Handle(new GetDiscussionRepliesQuery(GadgetType, _ownerId, parent.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Value!.Select(r => r.Body).Should().Equal("First", "Second");
        result.Value.Should().OnlyContain(r => r.ParentMessageId == parent.Id);
    }

    [Fact]
    public async Task Handle_ShouldBeNull_ForAThreadUnderANoteTheCallerCannotRead()
    {
        // Arrange
        var note = Post(DiscussionMessageKind.InternalNote, Start);
        Reply(note, "Agreed", Start.Plus(Duration.FromMinutes(5)));
        _gadgets.Access = new DiscussionAccess(CanReadInternal: false, CanPost: true, CanPostInternal: false, CanModerate: false);

        // Act
        var result = await CreateHandler().Handle(new GetDiscussionRepliesQuery(GadgetType, _ownerId, note.Id), TestContext.Current.CancellationToken);

        // Assert
        // Null rather than empty, so the endpoint answers 404 and never confirms the note exists.
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldBeNull_ForAReplyRatherThanTheMessageThatStartsTheThread()
    {
        // Arrange
        var parent = Post(DiscussionMessageKind.Public, Start);
        var reply = Reply(parent, "Reply", Start.Plus(Duration.FromMinutes(5)));

        // Act
        var result = await CreateHandler().Handle(new GetDiscussionRepliesQuery(GadgetType, _ownerId, reply.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().BeNull();
    }

    private DiscussionMessage Post(DiscussionMessageKind kind, Instant postedOn)
    {
        var message = DiscussionMessage.Post(_channel, kind, Guid.NewGuid(), null, "Top", [], Actor, postedOn).Value;
        _dbContext.AddDiscussionMessage(message);
        return message;
    }

    private DiscussionMessage Reply(DiscussionMessage parent, string body, Instant postedOn)
    {
        var reply = DiscussionMessage.Reply(_channel, parent, Guid.NewGuid(), body, [], Actor, postedOn).Value;
        _dbContext.AddDiscussionMessage(reply);
        return reply;
    }
}
