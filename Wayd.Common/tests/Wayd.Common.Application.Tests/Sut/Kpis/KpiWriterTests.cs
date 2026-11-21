using FluentAssertions;
using Wayd.Common.Application.Kpis;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Kpis;

namespace Wayd.Common.Application.Tests.Sut.Kpis;

public sealed class KpiWriterTests
{
    private readonly KpiTestHarness _harness = new();

    private KpiWriter CreateSut() => new(_harness.DbContext, _harness.DateTimeProvider);

    [Fact]
    public async Task RemoveAll_ShouldRemoveEveryKpiOnTheRecord_OpenAndClosed_WithAnEventEach()
    {
        // Arrange
        var open = _harness.AddKpi("Throughput");
        var closed = _harness.AddKpi("Cycle time", order: 1);
        closed.Close(EventActor.System, _harness.DateTimeProvider.Now).IsSuccess.Should().BeTrue();
        closed.ClearPostPersistenceActions();

        // Act
        await CreateSut().RemoveAll(KpiTestHarness.Gadget.Key, _harness.SubjectId, EventActor.System, TestContext.Current.CancellationToken);

        // Assert
        _harness.DbContext.Kpis.Should().BeEmpty();
        foreach (var kpi in new[] { open, closed })
        {
            kpi.PostPersistenceActions.Should().ContainSingle();
            kpi.ExecutePostPersistenceActions();
            kpi.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<KpiRemovedEvent>()
                .Which.SubjectId.Should().Be(_harness.SubjectId);
        }
        _harness.DbContext.SaveChangesCallCount.Should().Be(0, "the caller's save commits the removals with the deleted record");
    }

    [Fact]
    public async Task RemoveAll_ShouldLeaveAnotherRecordsKpis()
    {
        // Arrange
        var other = _harness.AddKpi("Throughput", subjectId: Guid.NewGuid());

        // Act
        await CreateSut().RemoveAll(KpiTestHarness.Gadget.Key, _harness.SubjectId, EventActor.System, TestContext.Current.CancellationToken);

        // Assert
        _harness.DbContext.Kpis.Should().ContainSingle().Which.Should().BeSameAs(other);
    }

    [Fact]
    public async Task RemoveAll_ShouldThrow_WhenTheSubjectTypeIsNotRegistered()
    {
        // Act
        var act = () => CreateSut().RemoveAll("test.kpi-writer.unknown", _harness.SubjectId, EventActor.System, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
