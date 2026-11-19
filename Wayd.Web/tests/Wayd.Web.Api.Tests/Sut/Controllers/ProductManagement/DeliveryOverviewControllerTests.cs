using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.ProductManagement.Application.DeliveryOverview.Dtos;
using Wayd.ProductManagement.Application.DeliveryOverview.Queries;
using Wayd.Tests.Shared;
using Wayd.Web.Api.Controllers.ProductManagement;

namespace Wayd.Web.Api.Tests.Sut.Controllers.ProductManagement;

public sealed class DeliveryOverviewControllerTests
{
    private readonly AutoMocker _mocker = new();

    private DeliveryOverviewController CreateController()
    {
        var controller = _mocker.CreateInstance<DeliveryOverviewController>();
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    [Fact]
    public async Task GetDeliveryOverview_WithoutATimeZone_ReadsTheWindowInTheSystemZone()
    {
        // Arrange — both ends are 05:00 UTC, still the previous day in the system zone
        var calendar = new TestingSystemCalendar(new TestingDateTimeProvider(new FakeClock(Instant.FromUtc(2026, 6, 30, 5, 0))));
        var dispatcher = _mocker.GetMock<IDispatcher>();
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetDeliveryOverviewQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeliveryOverviewDto)null!);

        // Act
        await CreateController().GetDeliveryOverview(
            Instant.FromUtc(2026, 6, 2, 5, 0),
            Instant.FromUtc(2026, 6, 30, 5, 0),
            null,
            null,
            calendar,
            TestContext.Current.CancellationToken);

        // Assert
        dispatcher.Verify(d => d.Send(
            It.Is<GetDeliveryOverviewQuery>(q =>
                q.Zone.Id == TestingSystemCalendar.DefaultTimeZone
                && q.From == new LocalDate(2026, 6, 1)
                && q.To == new LocalDate(2026, 6, 29)),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
