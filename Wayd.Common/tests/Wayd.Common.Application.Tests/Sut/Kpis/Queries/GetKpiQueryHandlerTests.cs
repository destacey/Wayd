using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Kpis.Queries;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Tests.Data;

namespace Wayd.Common.Application.Tests.Sut.Kpis.Queries;

public sealed class GetKpiQueryHandlerTests
{
    private static readonly LocalDate Jan1 = new(2026, 1, 1);

    private readonly KpiTestHarness _harness = new();
    private readonly EmployeeFaker _employeeFaker = new();

    private GetKpiQueryHandler CreateHandler() => new(_harness.Resolver(), _harness.DbContext);

    [Fact]
    public async Task Handle_ShouldListEveryMeasurementNewestFirst_NamingWhoTookIt()
    {
        // Arrange
        var taker = _employeeFaker.Generate();
        _harness.DbContext.AddEmployees(taker);
        var kpi = _harness.AddKpi();
        var now = _harness.DateTimeProvider.Now;
        kpi.AddMeasurement(KpiTestHarness.Measurement(12, Jan1, new LocalDate(2026, 1, 31), taker.Id), EventActor.System, now);
        kpi.AddMeasurement(KpiTestHarness.Measurement(15, new LocalDate(2026, 3, 1), new LocalDate(2026, 3, 31)), EventActor.System, now);
        kpi.AddMeasurement(KpiTestHarness.Measurement(14, new LocalDate(2026, 2, 1), new LocalDate(2026, 2, 28)), EventActor.System, now);

        // Act
        var result = await CreateHandler().Handle(new GetKpiQuery(KpiTestHarness.Gadget.Key, _harness.SubjectId, kpi.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var details = result.Value!;
        details.Measurements.Select(m => m.Value).Should().Equal(15, 14, 12);
        details.Measurements[2].MeasuredBy!.Id.Should().Be(taker.Id);
        details.Measurements[0].MeasuredBy.Should().BeNull();
        details.Kpi.Reading.Value.Should().Be(15);
        details.CanManage.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldBeNull_ForAKpiOnAnotherRecord()
    {
        // Arrange
        var elsewhere = _harness.AddKpi(subjectId: Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(new GetKpiQuery(KpiTestHarness.Gadget.Key, _harness.SubjectId, elsewhere.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }
}