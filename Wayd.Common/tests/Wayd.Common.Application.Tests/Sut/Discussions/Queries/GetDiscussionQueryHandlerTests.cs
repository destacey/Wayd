using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Discussions;
using Wayd.Common.Application.Discussions.Dtos;
using Wayd.Common.Application.Discussions.Queries;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Tests.Data;

namespace Wayd.Common.Application.Tests.Sut.Discussions.Queries;

public sealed class GetDiscussionQueryHandlerTests
{
    private const string GadgetType = "test.discussion-query.gadget";
    private static readonly DiscussionChannelTypeDescriptor Gadget = new(GadgetType, "Gadget", "Gadgets", supportsInternalNotes: true);
    private static readonly Instant Start = Instant.FromUtc(2026, 10, 20, 9, 0);
    private static readonly EventActor Actor = EventActor.User("user-1");

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly FakeDiscussionChannelType _gadgets = new(GadgetType);
    private readonly FakeRecordReadRule _readRule = new(GadgetType);
    private readonly Mock<IActivityLogReader> _activityLogReader = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Guid _employeeId = Guid.NewGuid();
    private readonly Guid _ownerId;
    private readonly List<ActivityLogDto> _activityNewestFirst = [];

    public GetDiscussionQueryHandlerTests()
    {
        DiscussionChannelTypes.Register(Gadget);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);
        _ownerId = _gadgets.AddRecord();

        _activityLogReader
            .Setup(r => r.Read(It.IsAny<Guid>(), FakeRecordReadRule.ActivityType, It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, string? _, PageRequest? paging, CancellationToken _) =>
            {
                var page = paging ?? new PageRequest();
                return new PagedResponse<ActivityLogDto>([.. _activityNewestFirst.Skip(page.Skip).Take(page.PageSize)], _activityNewestFirst.Count, page.Page, page.PageSize);
            });
    }

    private GetDiscussionQueryHandler CreateHandler() =>
        new(_dbContext, [_gadgets], [_readRule], _activityLogReader.Object, _currentPrincipal.Object);

    [Fact]
    public async Task Handle_ShouldBeNull_WhenTheCallerCannotReadTheRecord()
    {
        // Act
        var result = await CreateHandler().Handle(Query(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReadAsAnEmptyConversation_WhenNobodyHasPosted()
    {
        // Act
        var result = await CreateHandler().Handle(Query(_ownerId), TestContext.Current.CancellationToken);

        // Assert
        result.Value!.Entries.Items.Should().BeEmpty();
        result.Value.Entries.NextCursor.Should().BeNull();
        result.Value.CanPost.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldLeaveOutInternalNotes_ForACallerWhoCannotReadThem()
    {
        // Arrange
        var channel = OpenChannel();
        Post(channel, DiscussionMessageKind.Reply, "Visible", Start);
        Post(channel, DiscussionMessageKind.InternalNote, "Hidden", Start.Plus(Duration.FromMinutes(1)));
        _gadgets.Access = new DiscussionAccess(CanReadInternal: false, CanPost: true, CanPostInternal: false, CanModerate: false);

        // Act
        var result = await CreateHandler().Handle(Query(_ownerId), TestContext.Current.CancellationToken);

        // Assert
        result.Value!.Entries.Items.Select(e => e.Message!.Body).Should().Equal("Visible");
        result.Value.CanPostInternal.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldPageOldestFirst_WithACursorThatContinuesWhereThePageEnded()
    {
        // Arrange
        var channel = OpenChannel();
        for (var i = 0; i < 5; i++)
        {
            Post(channel, DiscussionMessageKind.Reply, $"Message {i}", Start.Plus(Duration.FromMinutes(i)));
        }

        // Act
        var first = await CreateHandler().Handle(Query(_ownerId, pageSize: 2), TestContext.Current.CancellationToken);
        var second = await CreateHandler().Handle(Query(_ownerId, pageSize: 2, before: first.Value!.Entries.NextCursor), TestContext.Current.CancellationToken);
        var third = await CreateHandler().Handle(Query(_ownerId, pageSize: 2, before: second.Value!.Entries.NextCursor), TestContext.Current.CancellationToken);

        // Assert
        first.Value.Entries.Items.Select(e => e.Message!.Body).Should().Equal("Message 0", "Message 1");
        second.Value.Entries.Items.Select(e => e.Message!.Body).Should().Equal("Message 2", "Message 3");
        third.Value!.Entries.Items.Select(e => e.Message!.Body).Should().Equal("Message 4");
        third.Value.Entries.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldPageNewestFirst_WhenAsked()
    {
        // Arrange
        var channel = OpenChannel();
        for (var i = 0; i < 3; i++)
        {
            Post(channel, DiscussionMessageKind.Reply, $"Message {i}", Start.Plus(Duration.FromMinutes(i)));
        }

        // Act
        var first = await CreateHandler().Handle(Query(_ownerId, pageSize: 2, order: DiscussionOrder.NewestFirst), TestContext.Current.CancellationToken);
        var second = await CreateHandler().Handle(Query(_ownerId, pageSize: 2, order: DiscussionOrder.NewestFirst, before: first.Value!.Entries.NextCursor), TestContext.Current.CancellationToken);

        // Assert
        first.Value.Entries.Items.Select(e => e.Message!.Body).Should().Equal("Message 2", "Message 1");
        second.Value!.Entries.Items.Select(e => e.Message!.Body).Should().Equal("Message 0");
    }

    [Fact]
    public async Task Handle_ShouldPlaceEachChangeBetweenTheMessagesOfThePageItFallsIn()
    {
        // Arrange
        var channel = OpenChannel();
        Post(channel, DiscussionMessageKind.Reply, "Message 0", Start.Plus(Duration.FromMinutes(10)));
        Post(channel, DiscussionMessageKind.Reply, "Message 1", Start.Plus(Duration.FromMinutes(20)));
        Post(channel, DiscussionMessageKind.Reply, "Message 2", Start.Plus(Duration.FromMinutes(30)));
        AddActivity("Created", Start);
        AddActivity("Assigned", Start.Plus(Duration.FromMinutes(15)));
        AddActivity("Moved", Start.Plus(Duration.FromMinutes(25)));

        // Act
        var first = await CreateHandler().Handle(Query(_ownerId, pageSize: 2), TestContext.Current.CancellationToken);
        var second = await CreateHandler().Handle(Query(_ownerId, pageSize: 2, before: first.Value!.Entries.NextCursor), TestContext.Current.CancellationToken);

        // Assert
        first.Value.Entries.Items.Select(Describe).Should().Equal("Created", "Message 0", "Assigned", "Message 1");
        // The change after the first page's last message opens the second page, so nothing is shown twice or skipped.
        second.Value!.Entries.Items.Select(Describe).Should().Equal("Moved", "Message 2");
    }

    [Fact]
    public async Task Handle_ShouldShowAChangeAtThePageEdgeOnce_AfterTheMessageThatCausedIt()
    {
        // Arrange
        var channel = OpenChannel();
        var askedOn = Start.Plus(Duration.FromMinutes(20));
        Post(channel, DiscussionMessageKind.Reply, "Message 0", Start.Plus(Duration.FromMinutes(10)));
        Post(channel, DiscussionMessageKind.Reply, "Question", askedOn);
        Post(channel, DiscussionMessageKind.Reply, "Answer", Start.Plus(Duration.FromMinutes(30)));
        // Asking and moving the request is one save, so the change shares the question's instant.
        AddActivity("Moved", askedOn);

        // Act
        var first = await CreateHandler().Handle(Query(_ownerId, pageSize: 2), TestContext.Current.CancellationToken);
        var second = await CreateHandler().Handle(Query(_ownerId, pageSize: 2, before: first.Value!.Entries.NextCursor), TestContext.Current.CancellationToken);

        // Assert
        first.Value.Entries.Items.Select(Describe).Should().Equal("Message 0", "Question", "Moved");
        second.Value!.Entries.Items.Select(Describe).Should().Equal("Answer");
    }

    [Fact]
    public async Task Handle_ShouldLeaveOutActivity_WhenOnlyMessagesAreAsked()
    {
        // Arrange
        var channel = OpenChannel();
        Post(channel, DiscussionMessageKind.Reply, "Message 0", Start.Plus(Duration.FromMinutes(10)));
        AddActivity("Created", Start);

        // Act
        var result = await CreateHandler().Handle(Query(_ownerId, messagesOnly: true), TestContext.Current.CancellationToken);

        // Assert
        result.Value!.Entries.Items.Should().ContainSingle().Which.Type.Should().Be(DiscussionEntryType.Message);
    }

    [Fact]
    public async Task Handle_ShouldCountEachThreadsRepliesAndNameItsPeople()
    {
        // Arrange
        var author = new EmployeeFaker().WithId(_employeeId).Generate();
        var replier = new EmployeeFaker().Generate();
        _dbContext.AddEmployee(author);
        _dbContext.AddEmployee(replier);
        var channel = OpenChannel();
        var parent = Post(channel, DiscussionMessageKind.Reply, "Question", Start);
        Reply(channel, parent, replier.Id, Start.Plus(Duration.FromMinutes(5)));
        Reply(channel, parent, replier.Id, Start.Plus(Duration.FromMinutes(9)));

        // Act
        var result = await CreateHandler().Handle(Query(_ownerId), TestContext.Current.CancellationToken);

        // Assert
        var message = result.Value!.Entries.Items.Should().ContainSingle().Subject.Message!;
        message.Author.Id.Should().Be(author.Id);
        message.Author.Name.Should().Be(author.Name.DisplayName);
        message.ReplyCount.Should().Be(2);
        message.LastReplyOn.Should().Be(Start.Plus(Duration.FromMinutes(9)));
        message.RecentRepliers.Select(r => r.Id).Should().Equal(replier.Id);
        message.CanEdit.Should().BeTrue("the caller wrote it");
    }

    private static string Describe(DiscussionEntryDto entry) =>
        entry.Type == DiscussionEntryType.Message ? entry.Message!.Body! : entry.Activity!.EventType;

    private static GetDiscussionQuery Query(
        Guid ownerId,
        int pageSize = 50,
        string? before = null,
        DiscussionOrder order = DiscussionOrder.OldestFirst,
        bool messagesOnly = false) =>
        new(GadgetType, ownerId, new CursorRequest(before, pageSize), order, messagesOnly);

    private DiscussionChannel OpenChannel()
    {
        var channel = DiscussionChannel.OpenForRecord(GadgetType, _ownerId, Actor, Start).Value;
        _dbContext.AddDiscussionChannel(channel);
        return channel;
    }

    private DiscussionMessage Post(DiscussionChannel channel, DiscussionMessageKind kind, string body, Instant postedOn)
    {
        var message = DiscussionMessage.Post(channel, kind, _employeeId, null, body, [], Actor, postedOn).Value;
        _dbContext.AddDiscussionMessage(message);
        return message;
    }

    private void Reply(DiscussionChannel channel, DiscussionMessage parent, Guid authorId, Instant postedOn) =>
        _dbContext.AddDiscussionMessage(DiscussionMessage.Reply(channel, parent, authorId, "Reply", [], Actor, postedOn).Value);

    /// <summary>Adds a change to the record's activity, kept newest first as the log reads it.</summary>
    private void AddActivity(string eventType, Instant timestamp)
    {
        _activityNewestFirst.Add(new ActivityLogDto
        {
            EventType = eventType,
            DomainArea = "Test",
            AggregateType = FakeRecordReadRule.ActivityType,
            AggregateId = _ownerId,
            Payload = "{}",
            Timestamp = timestamp,
        });
        _activityNewestFirst.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
    }
}
