using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Kpis;
using Wayd.Common.Application.Kpis.Commands;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Tests.Sut.Kpis.Commands;

public sealed class AddKpiCommandHandlerTests
{
    private readonly KpiTestHarness _harness = new();

    private AddKpiCommandHandler CreateHandler() => new(_harness.Scope());

    private AddKpiCommand Command(Guid? categoryId = null, KpiBaselineInput? baseline = null, KpiTargetInput? target = null) => new(
        KpiTestHarness.Gadget.Key,
        _harness.SubjectId,
        "Cycle time",
        null,
        categoryId,
        null,
        "days",
        KpiExpressedAs.ChangeFromBaseline,
        baseline ?? new KpiBaselineInput(20, new LocalDate(2026, 1, 1), new LocalDate(2026, 3, 31)),
        target ?? new KpiTargetInput(KpiTargetDirection.Decrease, KpiTargetKind.RelativeChange, 25, KpiAtRiskKind.ProgressToTarget, 50));

    [Fact]
    public async Task Handle_ShouldAddTheKpiLastOnTheScorecard()
    {
        // Arrange
        _harness.AddKpi(order: 0);
        _harness.AddKpi(order: 4);
        _harness.AddKpi(subjectId: Guid.NewGuid(), order: 9);

        // Act
        var result = await CreateHandler().Handle(Command(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var added = _harness.DbContext.Kpis.Single(k => k.Id == result.Value.Id);
        added.Order.Should().Be(5);
        added.SubjectType.Should().Be(KpiTestHarness.Gadget.Key);
        added.ExpressedAs.Should().Be(KpiExpressedAs.ChangeFromBaseline);
        added.Goal.TargetValue.Should().Be(15);
        added.Goal.AtRiskValue.Should().Be(17.5);
        added.Baseline.Window.Should().Be(new KpiPeriod(new LocalDate(2026, 1, 1), new LocalDate(2026, 3, 31)));
        _harness.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldBeForbidden_WithoutManagingTheRecord()
    {
        // Arrange
        _harness.Subject(canManage: false);
        _harness.Administer(false);

        // Act
        var result = await CreateHandler().Handle(Command(), TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(KpiAccessErrors.CannotManage);
        _harness.DbContext.Kpis.Should().BeEmpty();
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldLetAKpiAdministratorAddToARecordTheyAreNotPartOf()
    {
        // Arrange
        _harness.Subject(canManage: false);
        _harness.Administer(true);

        // Act
        var result = await CreateHandler().Handle(Command(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _harness.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_ForAnInactiveCategory()
    {
        // Arrange
        var category = KpiCategory.Create("Flow", null, 0, EventActor.System, _harness.DateTimeProvider.Now).Value;
        category.Deactivate(EventActor.System, _harness.DateTimeProvider.Now);
        _harness.DbContext.AddCategories(category);

        // Act
        var result = await CreateHandler().Handle(Command(categoryId: category.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(KpiAccessErrors.CategoryInactive);
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_ForAHalfGivenBaselineWindow()
    {
        // Arrange
        var baseline = new KpiBaselineInput(20, new LocalDate(2026, 1, 1), null);

        // Act
        var result = await CreateHandler().Handle(Command(baseline: baseline), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheDomainRefusesTheGoal()
    {
        // Arrange
        var target = new KpiTargetInput(KpiTargetDirection.Increase, KpiTargetKind.RelativeChange, 10, null, null);

        // Act
        var result = await CreateHandler().Handle(Command(baseline: new KpiBaselineInput(null, null, null), target: target), TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(KpiErrors.BaselineRequired);
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}