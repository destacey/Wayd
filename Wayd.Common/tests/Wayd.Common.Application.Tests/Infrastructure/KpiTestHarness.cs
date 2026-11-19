using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Kpis;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Kpis;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Infrastructure;

namespace Wayd.Common.Application.Tests.Infrastructure;

/// <summary>
/// The pieces every KPI handler test wires the same way: a fake context, a subject type the test decides the answer of,
/// and a signed-in caller.
/// </summary>
public sealed class KpiTestHarness
{
    /// <summary>The subject type every KPI in these tests is on.</summary>
    public static readonly KpiSubjectTypeDescriptor Gadget = new("test.kpi-handlers.gadget", "Gadget", "Gadget");

    public KpiTestHarness()
    {
        KpiSubjectTypes.Register(Gadget);
        SubjectType.Setup(t => t.SubjectType).Returns(Gadget.Key);
        CurrentUser.Setup(u => u.GetUserId()).Returns("user-1");
        CurrentUser.Setup(u => u.GetEmployeeId()).Returns(EmployeeId);
        Subject(canManage: true);
    }

    public FakeKpisDbContext DbContext { get; } = new();

    public Mock<IKpiSubjectType> SubjectType { get; } = new();

    public Mock<ICurrentPrincipal> CurrentPrincipal { get; } = new();

    public Mock<ICurrentUser> CurrentUser { get; } = new();

    public TestingDateTimeProvider DateTimeProvider { get; } = new(new FakeClock(Instant.FromUtc(2026, 11, 16, 9, 30)));

    /// <summary>The system time zone every test runs under: behind UTC, so a moment's day there is not its UTC day.</summary>
    public static DateTimeZone Zone { get; } = DateTimeZoneProviders.Tzdb["America/Chicago"];

    /// <summary>The system calendar, in <see cref="Zone"/>.</summary>
    public TestingSystemCalendar SystemCalendar => new(DateTimeProvider, Zone.Id);

    /// <summary>The caller's linked employee.</summary>
    public Guid EmployeeId { get; } = Guid.NewGuid();

    /// <summary>The record every test's KPIs are on.</summary>
    public Guid SubjectId { get; } = Guid.NewGuid();

    /// <summary>Makes the subject type find the record and answer whether the caller may manage its KPIs.</summary>
    public void Subject(bool canManage) =>
        SubjectType.Setup(t => t.Resolve(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => id == SubjectId ? new KpiSubjectAccess("Gadget One", "G1", canManage) : null);

    /// <summary>Makes the subject type say the record does not exist or cannot be read.</summary>
    public void SubjectHidden() =>
        SubjectType.Setup(t => t.Resolve(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((KpiSubjectAccess?)null);

    /// <summary>Gives or takes the KPI administrator permission.</summary>
    public void Administer(bool holds) =>
        CurrentPrincipal.Setup(p => p.HasPermission(KpiSubjectAccessResolver.AdministerPermission, It.IsAny<CancellationToken>())).ReturnsAsync(holds);

    /// <summary>
    /// A KPI on the given record, or the test's own, aiming to raise 10 to 20, added to the context with its creation
    /// events cleared.
    /// </summary>
    public Kpi AddKpi(
        string name = "Throughput", Guid? subjectId = null, int order = 0, Guid? categoryId = null,
        KpiMeasurementWindow? window = null, int? rollingAverageOf = null)
    {
        var goal = new KpiGoal(Domain.Enums.KpiTargetDirection.Increase, new KpiBaseline(10, null), KpiTarget.Absolute(20), KpiAtRiskThreshold.Absolute(16));
        var definition = new KpiDefinition(name, null, categoryId, null, "items", KpiExpressedAs.Value, goal, window, rollingAverageOf);
        var kpi = Kpi.Create(Gadget.Key, subjectId ?? SubjectId, definition, order, EventActor.System, DateTimeProvider.Now).Value;
        kpi.ClearPostPersistenceActions();
        DbContext.AddKpis(kpi);
        return kpi;
    }

    /// <summary>A measurement input for the given days.</summary>
    public static KpiMeasurementInput Measurement(double value, LocalDate start, LocalDate end, Guid? measuredById = null) =>
        new(value, KpiMeasurementTime.ForPeriod(new KpiPeriod(start, end)), null, null, end, measuredById, KpiMeasurementSource.Manual, null, null);

    public KpiSubjectAccessResolver Resolver() => new([SubjectType.Object], CurrentPrincipal.Object);

    public KpiCommandScope Scope() => new(Resolver(), DbContext, CurrentUser.Object, DateTimeProvider, SystemCalendar);
}