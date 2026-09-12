using FluentAssertions;
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
}
