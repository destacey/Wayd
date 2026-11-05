using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Discussions;
using Wayd.Common.Application.Discussions.Queries;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.Events;

namespace Wayd.Common.Application.Tests.Sut.Discussions.Queries;

public sealed class GetDiscussionMessageRevisionsQueryHandlerTests
{
    private const string GadgetType = "test.discussion-revisions.gadget";
    private static readonly DiscussionChannelTypeDescriptor Gadget = new(GadgetType, "Gadget", "Gadgets", supportsInternalNotes: true);
    private static readonly Instant Start = Instant.FromUtc(2026, 10, 20, 9, 0);
    private static readonly EventActor Actor = EventActor.User("user-1");

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly FakeDiscussionChannelType _gadgets = new(GadgetType);
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Guid _authorId = Guid.NewGuid();
    private readonly Guid _ownerId;
    private readonly DiscussionChannel _channel;

    public GetDiscussionMessageRevisionsQueryHandlerTests()
    {
        DiscussionChannelTypes.Register(Gadget);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        _ownerId = _gadgets.AddRecord();
        _channel = DiscussionChannel.OpenForRecord(GadgetType, _ownerId, Actor, Start).Value;
        _dbContext.AddDiscussionChannel(_channel);
    }

    private GetDiscussionMessageRevisionsQueryHandler CreateHandler() => new(_dbContext, [_gadgets], _currentPrincipal.Object);

    [Fact]
    public async Task Handle_ShouldReturnTheReplacedVersionsNewestFirst()
    {
        // Arrange
        var message = PostEditedTwice(DiscussionMessageKind.Public);

        // Act
        var result = await CreateHandler().Handle(new GetDiscussionMessageRevisionsQuery(GadgetType, _ownerId, message.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Value!.Select(r => r.Body).Should().Equal("Second", "First");
        result.Value[0].ReplacedOn.Should().Be(Start.Plus(Duration.FromMinutes(20)));
    }

    [Fact]
    public async Task Handle_ShouldBeNull_ForANoteTheCallerCannotRead()
    {
        // Arrange
        var note = PostEditedTwice(DiscussionMessageKind.InternalNote);
        _gadgets.Access = new DiscussionAccess(CanReadInternal: false, CanPost: true, CanPostInternal: false, CanModerate: false);

        // Act
        var result = await CreateHandler().Handle(new GetDiscussionMessageRevisionsQuery(GadgetType, _ownerId, note.Id), TestContext.Current.CancellationToken);

        // Assert
        // A revision of an internal note is an internal note.
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldBeEmpty_ForADeletedMessage()
    {
        // Arrange
        var message = PostEditedTwice(DiscussionMessageKind.Public);
        message.Delete(_channel, Guid.NewGuid(), Actor, Start.Plus(Duration.FromHours(1)));

        // Act
        var result = await CreateHandler().Handle(new GetDiscussionMessageRevisionsQuery(GadgetType, _ownerId, message.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().NotBeNull().And.BeEmpty();
    }

    private DiscussionMessage PostEditedTwice(DiscussionMessageKind kind)
    {
        var message = DiscussionMessage.Post(_channel, kind, _authorId, null, "First", [], Actor, Start).Value;
        message.Edit(_channel, _authorId, null, "Second", [], Actor, Start.Plus(Duration.FromMinutes(10)));
        message.Edit(_channel, _authorId, null, "Third", [], Actor, Start.Plus(Duration.FromMinutes(20)));
        _dbContext.AddDiscussionMessage(message);
        return message;
    }
}
