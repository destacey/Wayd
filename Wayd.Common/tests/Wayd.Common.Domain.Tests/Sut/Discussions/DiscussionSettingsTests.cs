using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Discussions;

namespace Wayd.Common.Domain.Tests.Sut.Discussions;

public sealed class DiscussionSettingsTests
{
    private static readonly DiscussionChannelTypeDescriptor Desked = new(
        "test.discussions.settings", "Ticket", "Tickets", supportsInternalNotes: true,
        supportsMentionAccess: true, settingsScopeType: "TicketDesk");

    private static readonly DiscussionChannelTypeDescriptor Unscoped = new(
        "test.discussions.settings-none", "Widget", "Widgets", supportsInternalNotes: false);

    private static readonly DiscussionChannelTypeDescriptor NoNotes = new(
        "test.discussions.settings-no-notes", "Sprocket", "Sprockets", supportsInternalNotes: false,
        settingsScopeType: "SprocketShop");

    private static readonly DiscussionChannelTypeDescriptor NotesOnly = new(
        "test.discussions.settings-notes-only", "Gizmo", "Gizmos", supportsInternalNotes: true,
        settingsScopeType: "GizmoShop");

    private static readonly Instant Now = Instant.FromUtc(2026, 10, 30, 9, 0);
    private static readonly EventActor Actor = EventActor.User("user-1", Guid.NewGuid());

    [Fact]
    public void DefaultSettings_ShouldTurnOnWhatTheTypeSupports()
    {
        // Arrange / Act
        var desked = Desked.DefaultSettings;
        var noNotes = NoNotes.DefaultSettings;

        // Assert
        desked.Should().Be(new DiscussionSettingValues(true, true));
        noNotes.Should().Be(new DiscussionSettingValues(false, false));
    }

    [Fact]
    public void Create_ShouldStoreTheValues_AndRecordBothEnds()
    {
        // Arrange
        var scopeId = Guid.NewGuid();
        var values = new DiscussionSettingValues(false, false);

        // Act
        var result = DiscussionSettings.Create(Desked, scopeId, Desked.DefaultSettings, values, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var settings = result.Value!;
        settings.Id.Should().Be(DiscussionSettings.IdFor(Desked.Key, scopeId));
        settings.ChannelType.Should().Be(Desked.Key);
        settings.ScopeId.Should().Be(scopeId);
        settings.Values.Should().Be(values);

        var changed = settings.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<DiscussionSettingsValuesChangedEvent>().Subject;
        changed.Id.Should().Be(settings.Id);
        changed.ScopeType.Should().Be("TicketDesk");
        changed.ScopeId.Should().Be(scopeId);
        changed.FromInternalNotesEnabled.Should().BeTrue();
        changed.ToInternalNotesEnabled.Should().BeFalse();
        changed.FromMentionsGrantAccess.Should().BeTrue();
        changed.ToMentionsGrantAccess.Should().BeFalse();
        changed.RelatedAggregates.Should().ContainSingle().Which.Should().Be(new AggregateReference("TicketDesk", scopeId));
        changed.Actor.Should().Be(Actor);
        changed.Timestamp.Should().Be(Now);
    }

    [Fact]
    public void Create_ShouldReturnNothing_WhenTheValuesAreThoseInherited()
    {
        // Arrange
        var inherited = new DiscussionSettingValues(true, false);

        // Act
        var result = DiscussionSettings.Create(Desked, Guid.NewGuid(), inherited, inherited with { }, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldRelateNothing_ForTheTypeDefault()
    {
        // Arrange
        var values = new DiscussionSettingValues(true, true);

        // Act
        var result = DiscussionSettings.Create(Desked, null, Desked.DefaultSettings, values, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var changed = result.Value!.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<DiscussionSettingsValuesChangedEvent>().Subject;
        changed.ScopeId.Should().BeNull();
        changed.RelatedAggregates.Should().BeEmpty();
    }

    [Fact]
    public void Create_ShouldFail_ForATypeWithNoSettings()
    {
        // Arrange / Act
        var result = DiscussionSettings.Create(Unscoped, Guid.NewGuid(), Unscoped.DefaultSettings,
            new DiscussionSettingValues(false, false), Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionErrors.SettingsNotSupported);
    }

    [Fact]
    public void Create_ShouldFail_WhenTurningOnInternalNotesTheTypeCannotHold()
    {
        // Arrange / Act
        var result = DiscussionSettings.Create(NoNotes, Guid.NewGuid(), NoNotes.DefaultSettings,
            new DiscussionSettingValues(true, false), Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionErrors.InternalNotesNotSupported);
    }

    [Fact]
    public void Create_ShouldFail_WhenLettingMentionsGrantAccessWhereTheyOnlyNotify()
    {
        // Arrange / Act
        var result = DiscussionSettings.Create(NoNotes, Guid.NewGuid(), NoNotes.DefaultSettings,
            new DiscussionSettingValues(false, true), Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionErrors.MentionAccessNotSupported);
    }

    [Fact]
    public void ChangeValues_ShouldRecordBothEnds_WhenSomethingChanged()
    {
        // Arrange
        var scopeId = Guid.NewGuid();
        var settings = DiscussionSettings.Create(Desked, scopeId, Desked.DefaultSettings,
            new DiscussionSettingValues(false, true), Actor, Now).Value!;
        settings.ClearDomainEvents();

        // Act
        var result = settings.ChangeValues(Desked, new DiscussionSettingValues(true, true), Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().BeTrue();
        settings.Values.Should().Be(new DiscussionSettingValues(true, true));

        var changed = settings.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<DiscussionSettingsValuesChangedEvent>().Subject;
        changed.FromInternalNotesEnabled.Should().BeFalse();
        changed.ToInternalNotesEnabled.Should().BeTrue();
    }

    [Fact]
    public void ChangeValues_ShouldRaiseNothing_WhenNothingChanged()
    {
        // Arrange
        var values = new DiscussionSettingValues(false, true);
        var settings = DiscussionSettings.Create(Desked, Guid.NewGuid(), Desked.DefaultSettings, values, Actor, Now).Value!;
        settings.ClearDomainEvents();

        // Act
        var result = settings.ChangeValues(Desked, values with { }, Actor, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
        settings.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeValues_ShouldLeaveTheValues_WhenTheChangeIsInvalid()
    {
        // Arrange
        var values = new DiscussionSettingValues(false, false);
        var settings = DiscussionSettings.Create(NotesOnly, Guid.NewGuid(), NotesOnly.DefaultSettings, values, Actor, Now).Value!;
        settings.ClearDomainEvents();

        // Act
        var result = settings.ChangeValues(NotesOnly, values with { MentionsGrantAccess = true }, Actor, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionErrors.MentionAccessNotSupported);
        settings.Values.Should().Be(values);
        settings.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeValues_ShouldThrow_ForAnotherChannelType()
    {
        // Arrange
        var settings = DiscussionSettings.Create(Desked, Guid.NewGuid(), Desked.DefaultSettings,
            new DiscussionSettingValues(false, true), Actor, Now).Value!;

        // Act
        var act = () => settings.ChangeValues(NotesOnly, new DiscussionSettingValues(true, false), Actor, Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_ForAnEmptyScopeId()
    {
        // Arrange / Act
        var act = () => DiscussionSettings.Create(Desked, Guid.Empty, Desked.DefaultSettings,
            new DiscussionSettingValues(false, false), Actor, Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Delete_ShouldRecordTheRemoval_OnTheScope()
    {
        // Arrange
        var scopeId = Guid.NewGuid();
        var settings = DiscussionSettings.Create(Desked, scopeId, Desked.DefaultSettings,
            new DiscussionSettingValues(false, true), Actor, Now).Value!;
        settings.ClearDomainEvents();

        // Act
        settings.Delete(Desked, Actor, Now);

        // Assert
        var deleted = settings.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<DiscussionSettingsDeletedEvent>().Subject;
        deleted.Id.Should().Be(settings.Id);
        deleted.ChannelType.Should().Be(Desked.Key);
        deleted.ScopeId.Should().Be(scopeId);
        deleted.RelatedAggregates.Should().ContainSingle().Which.Should().Be(new AggregateReference("TicketDesk", scopeId));
        deleted.Actor.Should().Be(Actor);
    }

    [Fact]
    public void Delete_ShouldThrow_ForAChannelTypesDefault()
    {
        // Arrange
        var settings = DiscussionSettings.Create(Desked, null, Desked.DefaultSettings,
            new DiscussionSettingValues(false, true), Actor, Now).Value!;

        // Act
        var act = () => settings.Delete(Desked, Actor, Now);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void IdFor_ShouldDifferByScope_AndBeStable()
    {
        // Arrange
        var scopeId = Guid.NewGuid();

        // Act
        var first = DiscussionSettings.IdFor(Desked.Key, scopeId);
        var again = DiscussionSettings.IdFor(Desked.Key, scopeId);
        var typeDefault = DiscussionSettings.IdFor(Desked.Key, null);

        // Assert
        again.Should().Be(first);
        typeDefault.Should().NotBe(first);
    }
}
