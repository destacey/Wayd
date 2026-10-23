using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Models.KeyPerformanceIndicators;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands.Kpis;
using Wayd.Web.Api.Controllers.Ppm;
using Wayd.Web.Api.Models.Ppm.StrategicInitiatives;

namespace Wayd.Web.Api.Tests.Sut.Controllers.Ppm;

/// <summary>How the strategic initiative endpoints shape their responses.</summary>
public sealed class StrategicInitiativesControllerTests
{
    private readonly AutoMocker _mocker = new();

    private StrategicInitiativesController CreateController()
    {
        var controller = _mocker.CreateInstance<StrategicInitiativesController>();
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    [Fact]
    public async Task CreateKpi_PointsTheLocationAtTheNewKpiById()
    {
        // Arrange
        var initiativeId = Guid.NewGuid();
        var created = new ObjectIdAndKey(Guid.NewGuid(), 42);
        _mocker.GetMock<IDispatcher>()
            .Setup(d => d.Send(It.IsAny<CreateStrategicInitiativeKpiCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(created));
        var request = new CreateStrategicInitiativeKpiRequest
        {
            StrategicInitiativeId = initiativeId,
            Name = "Customer retention",
            TargetValue = 90,
            TargetDirection = KpiTargetDirection.Increase,
        };
        var controller = CreateController();

        // Act
        var result = await controller.CreateKpi(initiativeId, request, TestContext.Current.CancellationToken);

        // Assert
        var createdAt = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdAt.ActionName.Should().Be(nameof(StrategicInitiativesController.GetKpi));
        createdAt.RouteValues.Should().ContainKey("id").WhoseValue.Should().Be(initiativeId.ToString());
        createdAt.RouteValues.Should().ContainKey("kpiId").WhoseValue.Should().Be(created.Id.ToString());
        createdAt.Value.Should().Be(created);
    }
}
