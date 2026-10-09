using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.TestData;
using Wayd.Tests.Shared.Extensions;
using NodaTime;

namespace Wayd.Organization.Domain.Tests.Sut.Models;

public class HolidayCalendarTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 8, 12, 0);
    private static readonly LocalDate NewYearsDay = new(2027, 1, 1);

    private readonly HolidayCalendarFaker _calendarFaker = new();

    [Fact]
    public void Create_RaisesTheCreationAfterTheSaveAssignsTheKey()
    {
        // Act
        var calendar = HolidayCalendar.Create(" United States ", "  ", EventActor.System, Now);

        // Assert
        calendar.Name.Should().Be("United States");
        calendar.Description.Should().BeNull();
        calendar.DomainEvents.Should().BeEmpty();
        calendar.SetPrivate(c => c.Key, 7);
        calendar.ExecutePostPersistenceActions();
        var raised = calendar.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<HolidayCalendarCreatedEvent>().Subject;
        raised.Key.Should().Be(7);
        raised.Name.Should().Be("United States");
    }

    [Fact]
    public void UpdateDetails_WhenChanged_RaisesEventCarryingBothEnds()
    {
        // Arrange
        var calendar = _calendarFaker.WithName("US").WithDescription(null).Generate();

        // Act
        var result = calendar.UpdateDetails("United States", "Federal holidays", EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var raised = calendar.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<HolidayCalendarDetailsUpdatedEvent>().Subject;
        raised.Name.Should().Be("United States");
        raised.Description.Should().Be("Federal holidays");
        raised.PreviousName.Should().Be("US");
        raised.PreviousDescription.Should().BeNull();
    }

    [Fact]
    public void UpdateDetails_WhenNothingChangedAfterTrimming_RaisesNoEvent()
    {
        // Arrange
        var calendar = _calendarFaker.WithName("US").WithDescription(null).Generate();

        // Act
        var result = calendar.UpdateDetails(" US ", " ", EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        calendar.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AddHoliday_AddsItAndRaisesEvent()
    {
        // Arrange
        var calendar = _calendarFaker.Generate();

        // Act
        var result = calendar.AddHoliday(NewYearsDay, "New Year's Day", EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        calendar.Holidays.Should().ContainSingle(h => h.Date == NewYearsDay && h.Name == "New Year's Day");
        var raised = calendar.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<HolidayAddedEvent>().Subject;
        raised.HolidayId.Should().Be(result.Value.Id);
        raised.Date.Should().Be(NewYearsDay);
    }

    [Fact]
    public void AddHoliday_OnADateAlreadyTaken_FailsAndRaisesNoEvent()
    {
        // Arrange
        var calendar = _calendarFaker.Generate();
        calendar.AddHoliday(NewYearsDay, "New Year's Day", EventActor.System, Now);
        calendar.ClearDomainEvents();

        // Act
        var result = calendar.AddHoliday(NewYearsDay, "Another", EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        calendar.Holidays.Should().HaveCount(1);
        calendar.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeHoliday_WhenMoved_RaisesEventCarryingBothEnds()
    {
        // Arrange
        var calendar = _calendarFaker.Generate();
        var holiday = calendar.AddHoliday(NewYearsDay, "New Year's Day", EventActor.System, Now).Value;
        calendar.ClearDomainEvents();
        var observed = NewYearsDay.PlusDays(-1);

        // Act
        var result = calendar.ChangeHoliday(holiday.Id, observed, "New Year's Day (observed)", EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        holiday.Date.Should().Be(observed);
        var raised = calendar.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<HolidayChangedEvent>().Subject;
        raised.Date.Should().Be(observed);
        raised.Name.Should().Be("New Year's Day (observed)");
        raised.PreviousDate.Should().Be(NewYearsDay);
        raised.PreviousName.Should().Be("New Year's Day");
    }

    [Fact]
    public void ChangeHoliday_WhenNothingChanged_RaisesNoEvent()
    {
        // Arrange
        var calendar = _calendarFaker.Generate();
        var holiday = calendar.AddHoliday(NewYearsDay, "New Year's Day", EventActor.System, Now).Value;
        calendar.ClearDomainEvents();

        // Act
        var result = calendar.ChangeHoliday(holiday.Id, NewYearsDay, " New Year's Day ", EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        calendar.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeHoliday_OntoAnotherHolidaysDate_Fails()
    {
        // Arrange
        var calendar = _calendarFaker.Generate();
        var holiday = calendar.AddHoliday(NewYearsDay, "New Year's Day", EventActor.System, Now).Value;
        var otherDate = NewYearsDay.PlusDays(17);
        calendar.AddHoliday(otherDate, "Martin Luther King Jr. Day", EventActor.System, Now);
        calendar.ClearDomainEvents();

        // Act
        var result = calendar.ChangeHoliday(holiday.Id, otherDate, "New Year's Day", EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        holiday.Date.Should().Be(NewYearsDay);
        calendar.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RemoveHoliday_RemovesItAndRaisesEvent()
    {
        // Arrange
        var calendar = _calendarFaker.Generate();
        var holiday = calendar.AddHoliday(NewYearsDay, "New Year's Day", EventActor.System, Now).Value;
        calendar.ClearDomainEvents();

        // Act
        var result = calendar.RemoveHoliday(holiday.Id, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        calendar.Holidays.Should().BeEmpty();
        var raised = calendar.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<HolidayRemovedEvent>().Subject;
        raised.HolidayId.Should().Be(holiday.Id);
        raised.Date.Should().Be(NewYearsDay);
    }

    [Fact]
    public void RemoveHoliday_WhenNotInTheCalendar_Fails()
    {
        // Arrange
        var calendar = _calendarFaker.Generate();

        // Act
        var result = calendar.RemoveHoliday(Guid.NewGuid(), EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        calendar.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Delete_RaisesEvent()
    {
        // Arrange
        var calendar = _calendarFaker.WithName("United States").Generate();

        // Act
        calendar.Delete(EventActor.System, Now);

        // Assert
        var raised = calendar.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<HolidayCalendarDeletedEvent>().Subject;
        raised.Name.Should().Be("United States");
    }
}
