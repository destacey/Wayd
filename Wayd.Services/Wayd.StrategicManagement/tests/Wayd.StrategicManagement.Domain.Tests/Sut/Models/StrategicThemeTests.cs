using FluentAssertions;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.StrategicManagement;
using Wayd.StrategicManagement.Domain.Models;
using Wayd.StrategicManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using NodaTime.Extensions;
using NodaTime.Testing;

namespace Wayd.StrategicManagement.Domain.Tests.Sut.Models;

public class StrategicThemeTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly StrategicThemeFaker _faker;

    public StrategicThemeTests()
    {
        _dateTimeProvider = new(new FakeClock(DateTime.UtcNow.ToInstant()));
        _faker = new StrategicThemeFaker();
    }

    [Fact]
    public void UpdateDetails_WhenValuesChange_RaisesEventCarryingBothEnds()
    {
        // Arrange
        var theme = _faker.Generate();
        var before = new StrategicThemeDetails(theme.Name, theme.Description);

        // Act
        var result = theme.UpdateDetails("New Name ", "New Description ", EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        theme.Name.Should().Be("New Name");
        theme.Description.Should().Be("New Description");
        var updatedEvent = theme.DomainEvents.OfType<StrategicThemeDetailsUpdatedEvent>().Should().ContainSingle().Subject;
        updatedEvent.Id.Should().Be(theme.Id);
        updatedEvent.Key.Should().Be(theme.Key);
        updatedEvent.Name.Should().Be("New Name");
        updatedEvent.Description.Should().Be("New Description");
        updatedEvent.Previous.Should().Be(before);
    }

    [Fact]
    public void UpdateDetails_BeforeTheFirstSave_WaitsForTheKey()
    {
        // Arrange
        var theme = StrategicTheme.Create("Name", "Description", StrategicThemeState.Proposed, EventActor.System, _dateTimeProvider.Now);

        // Act
        theme.UpdateDetails("New Name", "Description", EventActor.System, _dateTimeProvider.Now);

        // Assert
        theme.DomainEvents.Should().BeEmpty();
        theme.ExecutePostPersistenceActions();
        theme.DomainEvents.OfType<StrategicThemeCreatedEvent>().Should().ContainSingle().Which.Name.Should().Be("Name");
        theme.DomainEvents.OfType<StrategicThemeDetailsUpdatedEvent>().Should().ContainSingle().Which.Name.Should().Be("New Name");
    }

    [Fact]
    public void UpdateDetails_WhenNothingChanged_RaisesNoEvent()
    {
        // Arrange — the values a whole-record save sends back, before the setters trim them
        var theme = _faker.Generate();

        // Act
        var result = theme.UpdateDetails($"{theme.Name} ", $" {theme.Description}", EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        theme.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Activate_WhenProposed_RaisesActivatedEvent()
    {
        // Arrange
        var theme = _faker.AsProposed().Generate();

        // Act
        var result = theme.Activate(EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        theme.State.Should().Be(StrategicThemeState.Active);
        theme.DomainEvents.OfType<StrategicThemeActivatedEvent>().Should().ContainSingle()
            .Which.Id.Should().Be(theme.Id);
        theme.DomainEvents.OfType<StrategicThemeDetailsUpdatedEvent>().Should().BeEmpty();
    }

    [Fact]
    public void Activate_WhenNotProposed_FailsAndRaisesNoEvent()
    {
        // Arrange
        var theme = _faker.AsActive().Generate();

        // Act
        var result = theme.Activate(EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        theme.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Archive_WhenActive_RaisesArchivedEvent()
    {
        // Arrange
        var theme = _faker.AsActive().Generate();

        // Act
        var result = theme.Archive(EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        theme.State.Should().Be(StrategicThemeState.Archived);
        theme.DomainEvents.OfType<StrategicThemeArchivedEvent>().Should().ContainSingle()
            .Which.Id.Should().Be(theme.Id);
        theme.DomainEvents.OfType<StrategicThemeDetailsUpdatedEvent>().Should().BeEmpty();
    }

    [Fact]
    public void Archive_WhenNotActive_FailsAndRaisesNoEvent()
    {
        // Arrange
        var theme = _faker.AsProposed().Generate();

        // Act
        var result = theme.Archive(EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        theme.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Delete_WhenProposed_RaisesDeletedEvent()
    {
        // Arrange
        var theme = _faker.AsProposed().Generate();

        // Act
        var result = theme.Delete(EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        theme.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<StrategicThemeDeletedEvent>()
            .Which.Id.Should().Be(theme.Id);
    }

    [Fact]
    public void Delete_WhenNotProposed_FailsAndRaisesNoEvent()
    {
        // Arrange
        var theme = _faker.AsActive().Generate();

        // Act
        var result = theme.Delete(EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        theme.DomainEvents.Should().BeEmpty();
    }
}
