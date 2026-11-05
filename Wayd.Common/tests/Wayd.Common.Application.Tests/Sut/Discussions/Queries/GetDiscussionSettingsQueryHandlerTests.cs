using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Discussions;
using Wayd.Common.Application.Discussions.Queries;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.Events;

namespace Wayd.Common.Application.Tests.Sut.Discussions.Queries;

public sealed class GetDiscussionSettingsQueryHandlerTests
{
    private const string TicketType = "test.get-discussion-settings.ticket";
    private static readonly DiscussionChannelTypeDescriptor Ticket = new(TicketType, "Ticket", "Tickets", supportsInternalNotes: true,
        supportsMentionAccess: true, settingsScopeType: "TicketDesk");
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 30, 9, 0);

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly FakeDiscussionChannelType _tickets = new(TicketType);
    private readonly Guid _deskId = Guid.NewGuid();

    public GetDiscussionSettingsQueryHandlerTests()
    {
        DiscussionChannelTypes.Register(Ticket);
    }

    private GetDiscussionSettingsQueryHandler CreateHandler() =>
        new(_dbContext, [_tickets]);

    [Fact]
    public async Task Handle_ShouldBeInherited_ForAScopeWithNoneOfItsOwn()
    {
        // Arrange
        _tickets.SettingsAccess = new DiscussionSettingsAccess(CanManage: false);

        // Act
        var result = await CreateHandler().Handle(new GetDiscussionSettingsQuery(TicketType, _deskId), TestContext.Current.CancellationToken);

        // Assert
        var settings = result.Value!;
        settings.ChannelType.Should().Be(TicketType);
        settings.ScopeId.Should().Be(_deskId);
        settings.IsInherited.Should().BeTrue();
        settings.InternalNotesEnabled.Should().BeTrue();
        settings.MentionsGrantAccess.Should().BeTrue();
        settings.SupportsInternalNotes.Should().BeTrue();
        settings.SupportsMentionAccess.Should().BeTrue();
        settings.CanManage.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldReturnTheScopesOwnSettings()
    {
        // Arrange
        _dbContext.AddDiscussionSettings(DiscussionSettings.Create(Ticket, _deskId, Ticket.DefaultSettings,
            new DiscussionSettingValues(false, false), EventActor.System, Now).Value!);

        // Act
        var result = await CreateHandler().Handle(new GetDiscussionSettingsQuery(TicketType, _deskId), TestContext.Current.CancellationToken);

        // Assert
        var settings = result.Value!;
        settings.IsInherited.Should().BeFalse();
        settings.InternalNotesEnabled.Should().BeFalse();
        settings.MentionsGrantAccess.Should().BeFalse();
        settings.CanManage.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldBeNull_WhenTheCallerCannotSeeTheScope()
    {
        // Arrange
        _tickets.SettingsAccess = null;

        // Act
        var result = await CreateHandler().Handle(new GetDiscussionSettingsQuery(TicketType, _deskId), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }
}
