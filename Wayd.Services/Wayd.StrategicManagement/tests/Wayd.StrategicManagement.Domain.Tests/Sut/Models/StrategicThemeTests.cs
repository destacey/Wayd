using FluentAssertions;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.StrategicManagement;
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
    public void Update_WhenValuesChange_RaisesUpdatedEvent()
    {
        // Arrange
        var theme = _faker.Generate();

        // Act
        var result = theme.Update("New Name ", "New Description ", EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        theme.Name.Should().Be("New Name");
        theme.Description.Should().Be("New Description");
        var updatedEvent = theme.DomainEvents.OfType<StrategicThemeUpdatedEvent>().Should().ContainSingle().Subject;
        updatedEvent.Name.Should().Be("New Name");
        updatedEvent.Description.Should().Be("New Description");
    }

    [Fact]
    public void Update_WhenNothingChanged_RaisesNoEvent()
    {
        // Arrange — the values a whole-record save sends back, before the setters trim them
        var theme = _faker.Generate();

        // Act
        var result = theme.Update($"{theme.Name} ", $" {theme.Description}", EventActor.System, _dateTimeProvider.Now);

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
        theme.DomainEvents.OfType<StrategicThemeUpdatedEvent>().Should().BeEmpty();
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
        theme.DomainEvents.OfType<StrategicThemeUpdatedEvent>().Should().BeEmpty();
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
}
