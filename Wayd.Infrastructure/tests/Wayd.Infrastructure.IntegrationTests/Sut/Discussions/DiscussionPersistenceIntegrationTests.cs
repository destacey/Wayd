using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Discussions;
using Wayd.Common.Application.Discussions.Queries;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Events;
using Wayd.Common.Models;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Discussions;

/// <summary>
/// The discussion tables against real SQL Server: what the in-memory fakes cannot see — the includes that load a
/// message's mentions and revisions, the one-conversation-per-record index, the cascade that removes a conversation
/// through its reply foreign key, and the conversation query's translation.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class DiscussionPersistenceIntegrationTests(SqlServerDbContextFixture fixture)
{
    private const string GadgetType = "test.discussion-persistence.gadget";
    private static readonly DiscussionChannelTypeDescriptor Gadget = new(GadgetType, "Gadget", "Gadgets", supportsInternalNotes: true);
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 10, 29, 9, 30);
    private static readonly EventActor Actor = EventActor.System;

    static DiscussionPersistenceIntegrationTests()
    {
        DiscussionChannelTypes.Register(Gadget);
    }

    [Fact]
    public async Task Save_ShouldKeepAMessagesMentionsAndRevisions()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var author = await SeedEmployee();
        var mentioned = await SeedEmployee();
        Guid messageId;
        await using (var context = fixture.CreateContext())
        {
            var channel = DiscussionChannel.OpenForRecord(GadgetType, Guid.CreateVersion7(), Actor, Timestamp).Value;
            var message = DiscussionMessage.Post(channel, DiscussionMessageKind.InternalNote, author.Id, "Topic", "First", [mentioned.Id], Actor, Timestamp).Value;
            message.Edit(channel, author.Id, "Topic", "Second", [mentioned.Id], Actor, Timestamp.Plus(Duration.FromMinutes(5)));
            context.DiscussionChannels.Add(channel);
            context.DiscussionMessages.Add(message);
            messageId = message.Id;

            // Act
            await context.SaveChangesAsync(cancellationToken);
        }

        // Assert
        await using var reader = fixture.CreateContext();
        var stored = await reader.DiscussionMessages
            .Include(m => m.Mentions)
            .Include(m => m.Revisions)
            .SingleAsync(m => m.Id == messageId, cancellationToken);
        stored.Kind.Should().Be(DiscussionMessageKind.InternalNote);
        stored.Subject.Should().Be("Topic");
        stored.Body.Should().Be("Second");
        stored.EditedOn.Should().Be(Timestamp.Plus(Duration.FromMinutes(5)));
        stored.Mentions.Select(m => m.EmployeeId).Should().Equal(mentioned.Id);
        stored.Revisions.Should().ContainSingle().Which.Body.Should().Be("First");
    }

    [Fact]
    public async Task Save_ShouldRefuseASecondConversationOnTheSameRecord()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var ownerId = Guid.CreateVersion7();
        await using (var first = fixture.CreateContext())
        {
            first.DiscussionChannels.Add(DiscussionChannel.OpenForRecord(GadgetType, ownerId, Actor, Timestamp).Value);
            await first.SaveChangesAsync(cancellationToken);
        }

        await using var second = fixture.CreateContext();
        second.DiscussionChannels.Add(DiscussionChannel.OpenForRecord(GadgetType, ownerId, Actor, Timestamp).Value);

        // Act
        var act = () => second.SaveChangesAsync(cancellationToken);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task RemovingAConversation_ShouldRemoveItsThreadsRevisionsAndMentions()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var author = await SeedEmployee();
        var mentioned = await SeedEmployee();
        var ownerId = Guid.CreateVersion7();
        Guid channelId;
        Guid parentId;
        await using (var seed = fixture.CreateContext())
        {
            var channel = DiscussionChannel.OpenForRecord(GadgetType, ownerId, Actor, Timestamp).Value;
            var parent = DiscussionMessage.Post(channel, DiscussionMessageKind.Public, author.Id, null, "Question", [mentioned.Id], Actor, Timestamp).Value;
            parentId = parent.Id;
            parent.Edit(channel, author.Id, null, "Question, edited", [], Actor, Timestamp);
            var reply = DiscussionMessage.Reply(channel, parent, author.Id, "Answer", [], Actor, Timestamp).Value;
            seed.DiscussionChannels.Add(channel);
            seed.DiscussionMessages.AddRange(parent, reply);
            await seed.SaveChangesAsync(cancellationToken);
            channelId = channel.Id;
        }

        await using (var context = fixture.CreateContext())
        {
            // Act
            await NewWriter(context, author.Id).DeleteChannel(GadgetType, ownerId, Actor, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        // Assert
        await using var reader = fixture.CreateContext();
        (await reader.DiscussionChannels.AnyAsync(c => c.Id == channelId, cancellationToken)).Should().BeFalse();
        (await reader.DiscussionMessages.AnyAsync(m => m.ChannelId == channelId, cancellationToken)).Should().BeFalse();
        (await reader.Set<DiscussionMessageRevision>().AnyAsync(r => r.MessageId == parentId, cancellationToken)).Should().BeFalse();
        (await reader.Set<DiscussionMention>().AnyAsync(m => m.MessageId == parentId, cancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task Save_ShouldRefuseAnEditThatRacedADelete()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var author = await SeedEmployee();
        Guid messageId;
        await using (var seed = fixture.CreateContext())
        {
            var channel = DiscussionChannel.OpenForRecord(GadgetType, Guid.CreateVersion7(), Actor, Timestamp).Value;
            var message = DiscussionMessage.Post(channel, DiscussionMessageKind.InternalNote, author.Id, null, "Sensitive", [], Actor, Timestamp).Value;
            seed.DiscussionChannels.Add(channel);
            seed.DiscussionMessages.Add(message);
            await seed.SaveChangesAsync(cancellationToken);
            messageId = message.Id;
        }

        // The author starts an edit and a moderator starts a delete, each from what they read.
        await using var editing = fixture.CreateContext();
        await using var deleting = fixture.CreateContext();
        var toEdit = await editing.DiscussionMessages.Include(m => m.Mentions).Include(m => m.Revisions).SingleAsync(m => m.Id == messageId, cancellationToken);
        var editChannel = await editing.DiscussionChannels.SingleAsync(c => c.Id == toEdit.ChannelId, cancellationToken);
        var toDelete = await deleting.DiscussionMessages.Include(m => m.Mentions).Include(m => m.Revisions).SingleAsync(m => m.Id == messageId, cancellationToken);
        var deleteChannel = await deleting.DiscussionChannels.SingleAsync(c => c.Id == toDelete.ChannelId, cancellationToken);

        toDelete.Delete(deleteChannel, author.Id, Actor, Timestamp);
        await deleting.SaveChangesAsync(cancellationToken);
        toEdit.Edit(editChannel, author.Id, null, "Sensitive, edited", [], Actor, Timestamp);

        // Act
        var act = () => editing.SaveChangesAsync(cancellationToken);

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        await using var reader = fixture.CreateContext();
        var stored = await reader.DiscussionMessages.Include(m => m.Revisions).SingleAsync(m => m.Id == messageId, cancellationToken);
        stored.IsDeleted.Should().BeTrue();
        stored.Body.Should().BeNull("an edit saved after a delete must not put text back on the tombstone");
        stored.Revisions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDiscussionEntries_ShouldPageTopLevelMessagesInOrder()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var author = await SeedEmployee();
        var ownerId = Guid.CreateVersion7();
        await using (var seed = fixture.CreateContext())
        {
            var channel = DiscussionChannel.OpenForRecord(GadgetType, ownerId, Actor, Timestamp).Value;
            seed.DiscussionChannels.Add(channel);
            for (var i = 0; i < 3; i++)
            {
                // Two share an instant, so the id breaks the tie on the database's own ordering.
                var postedOn = Timestamp.Plus(Duration.FromMinutes(i == 2 ? 1 : i));
                seed.DiscussionMessages.Add(DiscussionMessage.Post(channel, DiscussionMessageKind.Public, author.Id, null, $"Message {i}", [], Actor, postedOn).Value);
            }

            seed.DiscussionMessages.Add(DiscussionMessage.Post(channel, DiscussionMessageKind.InternalNote, author.Id, null, "Note", [], Actor, Timestamp).Value);
            await seed.SaveChangesAsync(cancellationToken);
        }

        await using var context = fixture.CreateContext();
        var handler = NewQueryHandler(context, author.Id, canReadInternal: false);

        // Act
        var first = await handler.Handle(new GetDiscussionEntriesQuery(GadgetType, ownerId, new CursorRequest(null, 2), MessagesOnly: true), cancellationToken);
        var second = await handler.Handle(new GetDiscussionEntriesQuery(GadgetType, ownerId, new CursorRequest(first.Value!.NextCursor, 2), MessagesOnly: true), cancellationToken);

        // Assert
        var bodies = first.Value.Items.Concat(second.Value!.Items).Select(e => e.Message!.Body).ToList();
        bodies.Should().HaveCount(3).And.OnlyHaveUniqueItems().And.NotContain("Note");
        bodies[0].Should().Be("Message 0");
        second.Value.NextCursor.Should().BeNull();
    }

    private static DiscussionWriter NewWriter(WaydDbContext context, Guid employeeId) =>
        new(context, [new OpenChannelType(canReadInternal: true)], Principal(employeeId), Mock.Of<ICurrentUser>(u => u.GetUserId() == "user-1"), Mock.Of<IDateTimeProvider>(d => d.Now == Timestamp));

    private static GetDiscussionEntriesQueryHandler NewQueryHandler(WaydDbContext context, Guid employeeId, bool canReadInternal) =>
        new(context, [new OpenChannelType(canReadInternal)], [], Mock.Of<IActivityLogReader>(), Principal(employeeId));

    private static ICurrentPrincipal Principal(Guid employeeId)
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);
        return principal.Object;
    }

    private async Task<Employee> SeedEmployee()
    {
        var suffix = $"{Guid.CreateVersion7():N}"[..12];
        var employee = Employee.Create(
            new PersonName("Avery", null, "Chen"),
            suffix,
            Timestamp,
            new EmailAddress($"{suffix}@acme.example"),
            jobTitle: null,
            department: null,
            officeLocation: null,
            managerId: null,
            isActive: true,
            employeeType: null,
            EventActor.System,
            Timestamp);

        await using WaydDbContext context = fixture.CreateContext();
        context.Employees.Add(employee);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return employee;
    }

    /// <summary>A channel type that lets the caller do everything but, optionally, read internal notes.</summary>
    private sealed class OpenChannelType(bool canReadInternal) : IDiscussionChannelType
    {
        public string ChannelType => GadgetType;

        public Task<DiscussionAccess?> Resolve(Guid ownerId, CancellationToken cancellationToken) =>
            Task.FromResult<DiscussionAccess?>(new DiscussionAccess(canReadInternal, CanPost: true, CanPostInternal: canReadInternal, CanModerate: true));

        public Task<IReadOnlySet<Guid>> MentionableAmong(Guid ownerId, IReadOnlyCollection<Guid> employeeIds, DiscussionMessageKind kind, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<Guid>>(employeeIds.ToHashSet());

        public Task OnPosted(DiscussionChannel channel, DiscussionMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
