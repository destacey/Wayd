using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Discussions;
using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.Events;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Discussions;

/// <summary>
/// The discussion settings table against real SQL Server: the query that falls back from scope to default, and the
/// key that makes two first saves for one scope collide rather than store two rows.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class DiscussionSettingsPersistenceIntegrationTests(SqlServerDbContextFixture fixture)
{
    private const string TicketType = "test.discussion-settings-persistence.ticket";
    private static readonly DiscussionChannelTypeDescriptor Ticket = new(TicketType, "Ticket", "Tickets", supportsInternalNotes: true,
        supportsMentionAccess: true, settingsScopeType: "TicketDesk");
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 10, 30, 9, 0);

    static DiscussionSettingsPersistenceIntegrationTests()
    {
        DiscussionChannelTypes.Register(Ticket);
    }

    [Fact]
    public async Task DiscussionSettingsFor_ShouldReadTheScopesRow_AndFallBackToTheDefaultRow()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var deskId = Guid.CreateVersion7();
        var desk = new DiscussionSettingValues(InternalNotesEnabled: false, MentionsGrantAccess: true);
        var typeDefault = new DiscussionSettingValues(InternalNotesEnabled: true, MentionsGrantAccess: false);
        await using (var context = fixture.CreateContext())
        {
            // The default row may be left by an earlier run against the shared database.
            if (!await context.DiscussionSettings.AnyAsync(s => s.ChannelType == TicketType && s.ScopeId == null, cancellationToken))
                context.DiscussionSettings.Add(DiscussionSettings.Create(Ticket, null, Ticket.DefaultSettings, typeDefault, EventActor.System, Timestamp).Value!);
            context.DiscussionSettings.Add(DiscussionSettings.Create(Ticket, deskId, typeDefault, desk, EventActor.System, Timestamp).Value!);
            await context.SaveChangesAsync(cancellationToken);
        }

        // Act
        await using var reader = fixture.CreateContext();
        var forDesk = await reader.DiscussionSettingsFor(TicketType, deskId, cancellationToken);
        var forOtherDesk = await reader.DiscussionSettingsFor(TicketType, Guid.CreateVersion7(), cancellationToken);

        // Assert
        forDesk.Should().Be(desk);
        forOtherDesk.Should().Be(typeDefault);
    }

    [Fact]
    public async Task Save_ShouldRefuseASecondFirstSaveForTheSameScope()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var deskId = Guid.CreateVersion7();
        var values = new DiscussionSettingValues(InternalNotesEnabled: false, MentionsGrantAccess: false);
        await using (var first = fixture.CreateContext())
        {
            first.DiscussionSettings.Add(DiscussionSettings.Create(Ticket, deskId, Ticket.DefaultSettings, values, EventActor.System, Timestamp).Value!);
            await first.SaveChangesAsync(cancellationToken);
        }

        await using var second = fixture.CreateContext();
        second.DiscussionSettings.Add(DiscussionSettings.Create(Ticket, deskId, Ticket.DefaultSettings, values, EventActor.System, Timestamp).Value!);

        // Act
        var act = () => second.SaveChangesAsync(cancellationToken);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
