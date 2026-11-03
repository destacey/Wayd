using FluentAssertions;
using Wayd.Common.Application.Requests.DemandManagement;
using Wayd.DemandManagement.Application.DemandEstimationScales.Queries;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.DemandEstimationScales.Queries;

public sealed class IsEstimationScaleUsedByDemandQueryHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private IsEstimationScaleUsedByDemandQueryHandler CreateHandler() => new(_scenario.DbContext);

    [Fact]
    public async Task Handle_WhenADeskPinsOneOfItsVersions_IsTrue()
    {
        // Arrange
        var scale = _scenario.EstimationScale();
        _scenario.EstimatesOn(_scenario.Desk(), scale);
        scale.PublishNext();

        // Act
        var used = await CreateHandler().Handle(new IsEstimationScaleUsedByDemandQuery(scale.Id), TestContext.Current.CancellationToken);

        // Assert
        used.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenOnlyARequestsEstimateUsesIt_IsTrue()
    {
        // Arrange — the desk has since stopped estimating; the request keeps what it was given
        var scale = _scenario.EstimationScale();
        var desk = _scenario.EstimatesOn(_scenario.Desk(), scale);
        _scenario.Estimated(_scenario.Request(desk, _scenario.Employee()), desk, "M");
        _scenario.EstimatesOn(desk, null);

        // Act
        var used = await CreateHandler().Handle(new IsEstimationScaleUsedByDemandQuery(scale.Id), TestContext.Current.CancellationToken);

        // Assert
        used.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenOnlyAnotherScaleIsUsed_IsFalse()
    {
        // Arrange
        var unused = _scenario.EstimationScale("Unused");
        var desk = _scenario.EstimatesOn(_scenario.Desk(), _scenario.EstimationScale("Used"));
        _scenario.Estimated(_scenario.Request(desk, _scenario.Employee()), desk, "S");

        // Act
        var used = await CreateHandler().Handle(new IsEstimationScaleUsedByDemandQuery(unused.Id), TestContext.Current.CancellationToken);

        // Assert
        used.Should().BeFalse();
    }
}
