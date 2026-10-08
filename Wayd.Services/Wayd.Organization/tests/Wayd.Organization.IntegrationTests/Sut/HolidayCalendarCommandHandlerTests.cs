using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Settings;
using Wayd.Organization.Application.HolidayCalendars.Commands;
using Wayd.Organization.Domain.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// A calendar's holidays sit behind a backing field and are removed with it by a cascade; only a real context
/// shows both hold and that each change is recorded.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class HolidayCalendarCommandHandlerTests(SqlServerDbContextFixture fixture)
{
    private static readonly LocalDate NewYearsDay = new(2027, 1, 1);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task AddHoliday_PersistsTheHolidayAndRecordsIt()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var calendarId = await SeedCalendar(cancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = new AddHolidayCommandHandler(context, Clock(), CurrentUser(), NullLogger<AddHolidayCommandHandler>.Instance);

        // Act
        var result = await handler.Handle(new AddHolidayCommand(calendarId, NewYearsDay, "New Year's Day"), cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await using var assertContext = _fixture.CreateContext();
        var calendar = await assertContext.HolidayCalendars.Include(c => c.Holidays).SingleAsync(c => c.Id == calendarId, cancellationToken);
        calendar.Holidays.Should().ContainSingle(h => h.Id == result.Value && h.Date == NewYearsDay && h.Name == "New Year's Day");
        (await assertContext.ActivityLogs.AnyAsync(a => a.AggregateId == calendarId && a.EventType == "HolidayAddedEvent", cancellationToken))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Delete_RemovesTheCalendarWithItsHolidaysAndRecordsIt()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var calendarId = await SeedCalendar(cancellationToken, NewYearsDay);

        await using var context = _fixture.CreateContext();
        var settings = new Mock<ISettings<SchedulingSettings>>();
        settings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(new SchedulingSettings());
        var handler = new DeleteHolidayCalendarCommandHandler(context, settings.Object, Clock(), CurrentUser(), NullLogger<DeleteHolidayCalendarCommandHandler>.Instance);

        // Act
        var result = await handler.Handle(new DeleteHolidayCalendarCommand(calendarId), cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await using var assertContext = _fixture.CreateContext();
        (await assertContext.HolidayCalendars.AnyAsync(c => c.Id == calendarId, cancellationToken)).Should().BeFalse();
        (await assertContext.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM [Organization].[Holidays] WHERE [HolidayCalendarId] = {calendarId}")
            .SingleAsync(cancellationToken))
            .Should().Be(0);
        (await assertContext.ActivityLogs.AnyAsync(a => a.AggregateId == calendarId && a.EventType == "HolidayCalendarDeletedEvent", cancellationToken))
            .Should().BeTrue();
    }

    private async Task<Guid> SeedCalendar(CancellationToken cancellationToken, params LocalDate[] holidays)
    {
        await using var context = _fixture.CreateContext();
        var calendar = HolidayCalendar.Create($"Calendar {Guid.NewGuid():N}", null, EventActor.System, SqlServerDbContextFixture.FixedNow);
        await context.HolidayCalendars.AddAsync(calendar, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        foreach (var date in holidays)
            calendar.AddHoliday(date, "Holiday", EventActor.System, SqlServerDbContextFixture.FixedNow);
        await context.SaveChangesAsync(cancellationToken);

        return calendar.Id;
    }

    private static IDateTimeProvider Clock()
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(d => d.Now).Returns(SqlServerDbContextFixture.FixedNow);
        return clock.Object;
    }

    private static ICurrentUser CurrentUser()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns("user-1");
        return currentUser.Object;
    }
}
