using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Discussions;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.Events;

namespace Wayd.Common.Application.Tests.Sut.Discussions;

public sealed class DiscussionSettingsReadingTests
{
    private const string TicketType = "test.discussion-settings.ticket";
    private const string WidgetType = "test.discussion-settings.widget";
    private static readonly DiscussionChannelTypeDescriptor Ticket = new(TicketType, "Ticket", "Tickets", supportsInternalNotes: true,
        supportsMentionAccess: true, settingsScopeType: "TicketDesk");
    private static readonly DiscussionChannelTypeDescriptor Widget = new(WidgetType, "Widget", "Widgets", supportsInternalNotes: false);
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 30, 9, 0);

    private readonly FakeWaydDbContext _dbContext = new();

    public DiscussionSettingsReadingTests()
    {
        DiscussionChannelTypes.Register(Ticket, Widget);
    }

    private void Save(Guid? scopeId, DiscussionSettingValues values) =>
        _dbContext.AddDiscussionSettings(DiscussionSettings.Create(Ticket, scopeId, Ticket.DefaultSettings, values, EventActor.System, Now).Value!);

    [Fact]
    public async Task DiscussionSettingsFor_ShouldBeTheTypesDefaults_WhenNothingIsSaved()
    {
        // Arrange / Act
        var settings = await _dbContext.DiscussionSettingsFor(TicketType, Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        settings.Should().Be(Ticket.DefaultSettings);
    }

    [Fact]
    public async Task DiscussionSettingsFor_ShouldFallBackToTheTypesDefaultRow_ForAScopeWithNone()
    {
        // Arrange
        var typeDefault = new DiscussionSettingValues(InternalNotesEnabled: false, MentionsGrantAccess: true);
        Save(null, typeDefault);
        Save(Guid.NewGuid(), new DiscussionSettingValues(true, false));

        // Act
        var settings = await _dbContext.DiscussionSettingsFor(TicketType, Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        settings.Should().Be(typeDefault);
    }

    [Fact]
    public async Task DiscussionSettingsFor_ShouldPreferTheScopesRow()
    {
        // Arrange
        var deskId = Guid.NewGuid();
        var desk = new DiscussionSettingValues(InternalNotesEnabled: true, MentionsGrantAccess: false);
        Save(null, new DiscussionSettingValues(false, true));
        Save(deskId, desk);

        // Act
        var settings = await _dbContext.DiscussionSettingsFor(TicketType, deskId, TestContext.Current.CancellationToken);

        // Assert
        settings.Should().Be(desk);
    }

    [Fact]
    public async Task DiscussionSettingsFor_ShouldBeTheDescriptorsDefaults_ForATypeWithNoSettings()
    {
        // Arrange / Act
        var settings = await _dbContext.DiscussionSettingsFor(WidgetType, null, TestContext.Current.CancellationToken);

        // Assert
        settings.Should().Be(new DiscussionSettingValues(InternalNotesEnabled: false, MentionsGrantAccess: false));
    }

    [Fact]
    public async Task InheritedDiscussionSettingsFor_ShouldIgnoreTheScopesOwnRow()
    {
        // Arrange
        var deskId = Guid.NewGuid();
        var typeDefault = new DiscussionSettingValues(InternalNotesEnabled: false, MentionsGrantAccess: true);
        Save(null, typeDefault);
        Save(deskId, new DiscussionSettingValues(true, false));

        // Act
        var inherited = await _dbContext.InheritedDiscussionSettingsFor(TicketType, deskId, TestContext.Current.CancellationToken);

        // Assert
        inherited.Should().Be(typeDefault);
    }

    [Fact]
    public async Task InheritedDiscussionSettingsFor_ShouldBeTheDescriptorsDefaults_ForTheTypeDefault()
    {
        // Arrange
        Save(null, new DiscussionSettingValues(false, true));

        // Act
        var inherited = await _dbContext.InheritedDiscussionSettingsFor(TicketType, null, TestContext.Current.CancellationToken);

        // Assert
        inherited.Should().Be(Ticket.DefaultSettings);
    }

    [Fact]
    public async Task DiscussionSettingsFor_ShouldThrow_ForAnUnregisteredType()
    {
        // Arrange / Act
        var act = () => _dbContext.DiscussionSettingsFor("test.discussion-settings.nothing", null, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
