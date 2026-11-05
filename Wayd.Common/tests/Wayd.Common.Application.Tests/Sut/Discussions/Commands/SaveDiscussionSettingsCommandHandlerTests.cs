using CSharpFunctionalExtensions;
using FluentAssertions;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Discussions;
using Wayd.Common.Application.Discussions.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Discussions;
using Wayd.Tests.Shared;

namespace Wayd.Common.Application.Tests.Sut.Discussions.Commands;

public sealed class SaveDiscussionSettingsCommandHandlerTests
{
    private const string TicketType = "test.save-discussion-settings.ticket";
    private static readonly DiscussionChannelTypeDescriptor Ticket = new(TicketType, "Ticket", "Tickets", supportsInternalNotes: true,
        supportsMentionAccess: true, settingsScopeType: "TicketDesk");
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 30, 9, 0);

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly FakeDiscussionChannelType _tickets = new(TicketType);
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Guid _employeeId = Guid.NewGuid();
    private readonly Guid _deskId = Guid.NewGuid();

    public SaveDiscussionSettingsCommandHandlerTests()
    {
        DiscussionChannelTypes.Register(Ticket);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);
    }

    private SaveDiscussionSettingsCommandHandler CreateHandler() =>
        new(_dbContext, [_tickets], _currentPrincipal.Object,
            Mock.Of<ICurrentUser>(u => u.GetUserId() == "user-1"), new TestingDateTimeProvider(new FakeClock(Now)));

    private Task<Result> Save(Guid? scopeId, bool notes, bool mentions) =>
        CreateHandler().Handle(new SaveDiscussionSettingsCommand(TicketType, scopeId, notes, mentions), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Handle_ShouldCreateTheScopesRow_RecordingWhatItInherited()
    {
        // Arrange
        _dbContext.AddDiscussionSettings(DiscussionSettings.Create(Ticket, null, Ticket.DefaultSettings,
            new DiscussionSettingValues(true, false), EventActor.System, Now).Value!);

        // Act
        var result = await Save(_deskId, notes: false, mentions: false);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        var saved = _dbContext.DiscussionSettings.Single(s => s.ScopeId == _deskId);
        saved.Values.Should().Be(new DiscussionSettingValues(false, false));

        var changed = saved.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<DiscussionSettingsValuesChangedEvent>().Subject;
        changed.FromInternalNotesEnabled.Should().BeTrue();
        changed.ToInternalNotesEnabled.Should().BeFalse();
        changed.FromMentionsGrantAccess.Should().BeFalse();
        changed.ToMentionsGrantAccess.Should().BeFalse();
        changed.Actor.Should().Be(EventActor.User("user-1", _employeeId));
    }

    [Fact]
    public async Task Handle_ShouldSaveNothing_WhenTheValuesAreThoseInherited()
    {
        // Arrange
        var defaults = Ticket.DefaultSettings;

        // Act
        var result = await Save(_deskId, defaults.InternalNotesEnabled, defaults.MentionsGrantAccess);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.DiscussionSettings.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldChangeAnExistingRow()
    {
        // Arrange
        await Save(_deskId, notes: false, mentions: true);
        var saved = _dbContext.DiscussionSettings.Single();
        saved.ClearDomainEvents();

        // Act
        var result = await Save(_deskId, notes: false, mentions: false);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        saved.MentionsGrantAccess.Should().BeFalse();
        saved.DomainEvents.Should().ContainSingle();
        _dbContext.SaveChangesCallCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldRemoveTheRow_WhenItIsSetBackToWhatItInherits()
    {
        // Arrange
        await Save(_deskId, notes: false, mentions: true);
        var saved = _dbContext.DiscussionSettings.Single();
        saved.ClearDomainEvents();
        var defaults = Ticket.DefaultSettings;

        // Act
        var result = await Save(_deskId, defaults.InternalNotesEnabled, defaults.MentionsGrantAccess);

        // Assert
        // Removed, so the desk follows any later change to the default; the change is still recorded.
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _dbContext.DiscussionSettings.Should().BeEmpty();
        var changed = saved.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<DiscussionSettingsValuesChangedEvent>().Subject;
        changed.FromInternalNotesEnabled.Should().BeFalse();
        changed.ToInternalNotesEnabled.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldBeForbidden_ForSomeoneWhoDoesNotRunTheScope()
    {
        // Arrange
        _tickets.SettingsAccess = new DiscussionSettingsAccess(CanManage: false);

        // Act
        var result = await Save(_deskId, notes: false, mentions: false);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionAccessErrors.CannotManageSettings);
        _dbContext.DiscussionSettings.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldBeNotFound_ForAScopeTheCallerCannotSee()
    {
        // Arrange
        _tickets.SettingsAccess = null;

        // Act
        var result = await Save(_deskId, notes: false, mentions: false);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionAccessErrors.NotFound);
    }
}
