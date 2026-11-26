using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Discussions;
using Wayd.Common.Application.Discussions.Queries;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.Events;

namespace Wayd.Common.Application.Tests.Sut.Discussions.Queries;

public sealed class GetDiscussionMessageQueryHandlerTests
{
    private const string GadgetType = "test.discussion-message.gadget";
    private static readonly DiscussionChannelTypeDescriptor Gadget = new(GadgetType, "Gadget", "Gadgets", supportsInternalNotes: true);
    private static readonly Instant Start = Instant.FromUtc(2026, 10, 20, 9, 0);
    private static readonly EventActor Actor = EventActor.User("user-1");

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly FakeDiscussionChannelType _gadgets = new(GadgetType);
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Guid _ownerId;
    private readonly DiscussionChannel _channel;

    public GetDiscussionMessageQueryHandlerTests()
    {
        DiscussionChannelTypes.Register(Gadget);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        _ownerId = _gadgets.AddRecord();
        _channel = DiscussionChannel.OpenForRecord(GadgetType, _ownerId, Actor, Start).Value;
        _dbContext.AddDiscussionChannel(_channel);
    }

    private GetDiscussionMessageQueryHandler CreateHandler() => new(_dbContext, [_gadgets], _currentPrincipal.Object);

    [Fact]
    public async Task Handle_ShouldReturnATopLevelMessageWithItsThreadCounts()
    {
        // Arrange
        var parent = Post(DiscussionMessageKind.Public, "Top");
        Reply(parent, "Reply", Start.Plus(Duration.FromMinutes(5)));

        // Act
        var result = await CreateHandler().Handle(new GetDiscussionMessageQuery(GadgetType, _ownerId, parent.Id), TestContext.Current.CancellationToken);

        // Assert
        var message = result.Value!;
        message.Id.Should().Be(parent.Id);
        message.Body.Should().Be("Top");
        message.ParentMessageId.Should().BeNull();
        message.ReplyCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldReturnAReplyWithTheMessageItsThreadHangsFrom()
    {
        // Arrange
        var parent = Post(DiscussionMessageKind.Public, "Top");
        var reply = Reply(parent, "Reply", Start.Plus(Duration.FromMinutes(5)));

        // Act
        var result = await CreateHandler().Handle(new GetDiscussionMessageQuery(GadgetType, _ownerId, reply.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Value!.Id.Should().Be(reply.Id);
        result.Value.Body.Should().Be("Reply");
        result.Value.ParentMessageId.Should().Be(parent.Id);
    }

    [Fact]
    public async Task Handle_ShouldBeNull_ForAReplyUnderANoteTheCallerCannotRead()
    {
        // Arrange
        var note = Post(DiscussionMessageKind.InternalNote, "Note");
        var reply = Reply(note, "Agreed", Start.Plus(Duration.FromMinutes(5)));
        _gadgets.Access = new DiscussionAccess(CanReadInternal: false, CanPost: true, CanPostInternal: false, CanModerate: false);

        // Act
        var result = await CreateHandler().Handle(new GetDiscussionMessageQuery(GadgetType, _ownerId, reply.Id), TestContext.Current.CancellationToken);

        // Assert
        // Null rather than a failure, so the endpoint answers 404 and never confirms the note exists.
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldBeNull_ForAMessageInAnotherRecordsConversation()
    {
        // Arrange
        var otherOwnerId = _gadgets.AddRecord();
        var otherChannel = DiscussionChannel.OpenForRecord(GadgetType, otherOwnerId, Actor, Start).Value;
        _dbContext.AddDiscussionChannel(otherChannel);
        var elsewhere = DiscussionMessage.Post(otherChannel, DiscussionMessageKind.Public, Guid.NewGuid(), null, "Elsewhere", [], Actor, Start).Value;
        _dbContext.AddDiscussionMessage(elsewhere);

        // Act
        var result = await CreateHandler().Handle(new GetDiscussionMessageQuery(GadgetType, _ownerId, elsewhere.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldBeNull_WhenTheCallerCannotReadTheRecord()
    {
        // Arrange — the channel type answers only for records it registered.
        var parent = Post(DiscussionMessageKind.Public, "Top");

        // Act
        var result = await CreateHandler().Handle(new GetDiscussionMessageQuery(GadgetType, Guid.NewGuid(), parent.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    private DiscussionMessage Post(DiscussionMessageKind kind, string body)
    {
        var message = DiscussionMessage.Post(_channel, kind, Guid.NewGuid(), null, body, [], Actor, Start).Value;
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
