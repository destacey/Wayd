using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Kpis;
using Wayd.Common.Application.Kpis.Commands;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Kpis;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Tests.Sut.Kpis.Commands;

public sealed class ChangeKpiCheckpointPlanCommandHandlerTests
{
    private static readonly KpiCheckpointValues Q1 = new(new LocalDate(2026, 3, 31), "Q1", 12, 11);
    private static readonly KpiCheckpointValues Q2 = new(new LocalDate(2026, 6, 30), "Q2", 15, null);

    private readonly KpiTestHarness _harness = new();

    private ChangeKpiCheckpointPlanCommandHandler CreateHandler() => new(_harness.Scope());

    [Fact]
    public async Task Handle_ShouldReplaceThePlan_AndSave()
    {
        // Arrange
        var kpi = _harness.AddKpi();
        var command = new ChangeKpiCheckpointPlanCommand(KpiTestHarness.Gadget.Key, _harness.SubjectId, kpi.Id, [Q2, Q1]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        kpi.CheckpointPlan.Should().Equal(Q1, Q2);
        _harness.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_ACallerWhoMayNotManageTheRecordsKpis()
    {
        // Arrange
        var kpi = _harness.AddKpi();
        _harness.Subject(canManage: false);
        var command = new ChangeKpiCheckpointPlanCommand(KpiTestHarness.Gadget.Key, _harness.SubjectId, kpi.Id, [Q1]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(KpiAccessErrors.CannotManage);
        kpi.Checkpoints.Should().BeEmpty();
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WithoutSaving_WhenThePlanDoesNotHold()
    {
        // Arrange
        var kpi = _harness.AddKpi();
        var command = new ChangeKpiCheckpointPlanCommand(KpiTestHarness.Gadget.Key, _harness.SubjectId, kpi.Id, [Q1, Q1 with { Label = "Again" }]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(KpiErrors.CheckpointDateTaken);
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldRaiseNothing_AndNotSave_WhenThePlanIsUnchanged()
    {
        // Arrange
        var kpi = _harness.AddKpi();
        kpi.ChangeCheckpointPlan([Q1], EventActor.System, _harness.DateTimeProvider.Now);
        kpi.ClearDomainEvents();
        var command = new ChangeKpiCheckpointPlanCommand(KpiTestHarness.Gadget.Key, _harness.SubjectId, kpi.Id, [Q1]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        kpi.DomainEvents.OfType<KpiCheckpointPlanChangedEvent>().Should().BeEmpty();
        _harness.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldSave_WhenOnlyALabelChanges()
    {
        // Arrange
        var kpi = _harness.AddKpi();
        kpi.ChangeCheckpointPlan([Q1], EventActor.System, _harness.DateTimeProvider.Now);
        kpi.ClearDomainEvents();
        var command = new ChangeKpiCheckpointPlanCommand(KpiTestHarness.Gadget.Key, _harness.SubjectId, kpi.Id, [Q1 with { Label = "Spring" }]);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        kpi.Checkpoints.Should().ContainSingle().Which.Label.Should().Be("Spring");
        _harness.DbContext.SaveChangesCallCount.Should().Be(1);
    }
}
