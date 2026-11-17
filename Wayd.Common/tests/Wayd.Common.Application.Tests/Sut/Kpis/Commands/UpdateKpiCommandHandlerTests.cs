using FluentAssertions;
using Wayd.Common.Application.Kpis;
using Wayd.Common.Application.Kpis.Commands;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Tests.Sut.Kpis.Commands;

public sealed class UpdateKpiCommandHandlerTests
{
    private readonly KpiTestHarness _harness = new();

    private UpdateKpiCommandHandler CreateHandler() => new(_harness.Scope());

    private UpdateKpiCommand Command(Guid kpiId, string name = "Throughput", Guid? categoryId = null, double? baseline = 10, double target = 20) => new(
        KpiTestHarness.Gadget.Key,
        _harness.SubjectId,
        kpiId,
        name,
        null,
        categoryId,
        null,
        "items",
        KpiExpressedAs.Value,
        new KpiBaselineInput(baseline, null, null),
        new KpiTargetInput(KpiTargetDirection.Increase, KpiTargetKind.Absolute, target, KpiAtRiskKind.Absolute, 16));

    [Fact]
    public async Task Handle_ShouldSaveABaselineAndTargetThatOnlyHoldTogether()
    {
        // Arrange
        var kpi = _harness.AddKpi();

        // Act
        var result = await CreateHandler().Handle(Command(kpi.Id, baseline: 40, target: 50) with
        {
            Target = new KpiTargetInput(KpiTargetDirection.Increase, KpiTargetKind.Absolute, 50, null, null),
        }, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        kpi.Baseline.Value.Should().Be(40);
        kpi.Target.Value.Should().Be(50);
        _harness.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldSaveNothing_WhenTheGoalFails_EvenThoughTheDetailsHold()
    {
        // Arrange
        var kpi = _harness.AddKpi();

        // Act
        var result = await CreateHandler().Handle(Command(kpi.Id, name: "Renamed", baseline: 30), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldBeForbidden_WithoutManagingTheRecord()
    {
        // Arrange
        var kpi = _harness.AddKpi();
        _harness.Subject(canManage: false);
        _harness.Administer(false);

        // Act
        var result = await CreateHandler().Handle(Command(kpi.Id, name: "Renamed"), TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(KpiAccessErrors.CannotManage);
        kpi.Name.Should().Be("Throughput");
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFailWithCategoryNotFound_ForACategoryNoOneHas()
    {
        // Arrange
        var kpi = _harness.AddKpi();

        // Act
        var result = await CreateHandler().Handle(Command(kpi.Id, categoryId: Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(KpiAccessErrors.CategoryNotFound);
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
