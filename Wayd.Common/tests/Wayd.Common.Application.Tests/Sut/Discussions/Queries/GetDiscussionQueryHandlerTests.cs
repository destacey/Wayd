using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Discussions;
using Wayd.Common.Application.Discussions.Queries;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.Events;

namespace Wayd.Common.Application.Tests.Sut.Discussions.Queries;

public sealed class GetDiscussionQueryHandlerTests
{
    private const string GadgetType = "test.discussion-state.gadget";
    private static readonly DiscussionChannelTypeDescriptor Gadget = new(GadgetType, "Gadget", "Gadgets", supportsInternalNotes: true,
        settingsScopeType: "GadgetShop");
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 29, 9, 30);

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly FakeDiscussionChannelType _gadgets = new(GadgetType);
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();

    public GetDiscussionQueryHandlerTests()
    {
        DiscussionChannelTypes.Register(Gadget);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
    }

    private GetDiscussionQueryHandler CreateHandler() => new(_dbContext, [_gadgets], _currentPrincipal.Object);

    [Fact]
    public async Task Handle_ShouldSayWhatTheCallerMayDo()
    {
        // Arrange
        var ownerId = _gadgets.AddRecord();
        _gadgets.Access = new DiscussionAccess(CanReadInternal: false, CanPost: true, CanPostInternal: true, CanModerate: false);

        // Act
        var result = await CreateHandler().Handle(new GetDiscussionQuery(GadgetType, ownerId), TestContext.Current.CancellationToken);

        // Assert
        var discussion = result.Value!;
        discussion.CanPost.Should().BeTrue();
        // Writing a note needs reading notes too, so a caller who cannot read them is offered none.
        discussion.CanPostInternal.Should().BeFalse();
        discussion.CanModerate.Should().BeFalse();
        discussion.IsLocked.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldOfferNoInternalNotes_WhenTheScopeTurnedThemOff()
    {
        // Arrange
        var ownerId = _gadgets.AddRecord();
        var shopId = Guid.NewGuid();
        _gadgets.Access = _gadgets.Access with { ScopeId = shopId };
        _dbContext.AddDiscussionSettings(DiscussionSettings.Create(Gadget, shopId, Gadget.DefaultSettings,
            new DiscussionSettingValues(InternalNotesEnabled: false, MentionsGrantAccess: false),
            EventActor.System, Now).Value!);

        // Act
        var result = await CreateHandler().Handle(new GetDiscussionQuery(GadgetType, ownerId), TestContext.Current.CancellationToken);

        // Assert
        result.Value!.CanPostInternal.Should().BeFalse();
        result.Value.CanPost.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldSayTheConversationIsLocked()
    {
        // Arrange
        var ownerId = _gadgets.AddRecord();
        var channel = DiscussionChannel.OpenForRecord(GadgetType, ownerId, EventActor.System, Now).Value;
        channel.Lock(Guid.NewGuid(), EventActor.System, Now);
        _dbContext.AddDiscussionChannel(channel);

        // Act
        var result = await CreateHandler().Handle(new GetDiscussionQuery(GadgetType, ownerId), TestContext.Current.CancellationToken);

        // Assert
        result.Value!.IsLocked.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldCountOnlyTheThreadsAndMessagesTheCallerCanRead()
    {
        // Arrange — a reply thread with one reply, a deleted message, and an internal note the caller cannot read
        var ownerId = _gadgets.AddRecord();
        var channel = DiscussionChannel.OpenForRecord(GadgetType, ownerId, EventActor.System, Now).Value;
        _dbContext.AddDiscussionChannel(channel);
        var author = Guid.NewGuid();
        var question = DiscussionMessage.Post(channel, DiscussionMessageKind.Public, author, null, "Question", [], EventActor.System, Now).Value;
        var answer = DiscussionMessage.Reply(channel, question, author, "Answer", [], EventActor.System, Now).Value;
        var deleted = DiscussionMessage.Post(channel, DiscussionMessageKind.Public, author, null, "Oops", [], EventActor.System, Now).Value;
        deleted.Delete(channel, author, EventActor.System, Now);
        var note = DiscussionMessage.Post(channel, DiscussionMessageKind.InternalNote, author, null, "Hidden", [], EventActor.System, Now).Value;
        foreach (var message in new[] { question, answer, deleted, note })
        {
            _dbContext.AddDiscussionMessage(message);
        }
        _gadgets.Access = new DiscussionAccess(CanReadInternal: false, CanPost: true, CanPostInternal: false, CanModerate: false);

        // Act
        var result = await CreateHandler().Handle(new GetDiscussionQuery(GadgetType, ownerId), TestContext.Current.CancellationToken);

        // Assert
        result.Value!.ThreadCount.Should().Be(1);
        result.Value.MessageCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldBeNull_WhenTheCallerCannotReadTheRecord()
    {
        // Arrange — a record the channel type does not know, so the caller cannot read it.
        var unreadableId = Guid.NewGuid();

        // Act
        var result = await CreateHandler().Handle(new GetDiscussionQuery(GadgetType, unreadableId), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }
}
