using Wayd.Common.Domain.Tests.Data.Models;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Data;
using Wayd.TestData.Core;

namespace Wayd.Common.Domain.Tests.Data;

public sealed class TestKpiMeasurementFaker : PrivateConstructorFaker<TestKpiMeasurement>
{
    public TestKpiMeasurementFaker(TestingDateTimeProvider dateTimeProvider)
    {
        var pastDate = dateTimeProvider.Now.Minus(Duration.FromDays(FakerHub.Random.Int(1, 200)));

        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.KpiId, f => f.Random.Guid());
        RuleFor(x => x.ActualValue, f => f.Random.Double(0, 100));
        RuleFor(x => x.MeasurementDate, f => pastDate);
        RuleFor(x => x.MeasuredById, f => f.Random.Guid());
        RuleFor(x => x.Note, f => f.Lorem.Sentence());
    }
}

public static class TestKpiMeasurementFakerExtensions
{
    public static TestKpiMeasurementFaker WithId(this TestKpiMeasurementFaker faker, Guid? id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static TestKpiMeasurementFaker WithKpiId(this TestKpiMeasurementFaker faker, Guid? kpiId)
    {
        faker.RuleFor(x => x.KpiId, kpiId);

        return faker;
    }

    public static TestKpiMeasurementFaker WithActualValue(this TestKpiMeasurementFaker faker, double? actualValue)
    {
        faker.RuleFor(x => x.ActualValue, actualValue);

        return faker;
    }

    public static TestKpiMeasurementFaker WithMeasurementDate(this TestKpiMeasurementFaker faker, Instant? measurementDate)
    {
        faker.RuleFor(x => x.MeasurementDate, measurementDate);

        return faker;
    }

    public static TestKpiMeasurementFaker WithMeasuredById(this TestKpiMeasurementFaker faker, Guid? measuredById)
    {
        faker.RuleFor(x => x.MeasuredById, measuredById);

        return faker;
    }

    public static TestKpiMeasurementFaker WithNote(this TestKpiMeasurementFaker faker, string? note)
    {
        faker.RuleFor(x => x.Note, note);

        return faker;
    }
}
