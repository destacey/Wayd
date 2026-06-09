using NodaTime;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Data;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

public sealed class StrategicInitiativeKpiMeasurementFaker : PrivateConstructorFaker<StrategicInitiativeKpiMeasurement>
{
    public StrategicInitiativeKpiMeasurementFaker(TestingDateTimeProvider dateTimeProvider)
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

public static class StrategicInitiativeKpiMeasurementFakerExtensions
{
    public static StrategicInitiativeKpiMeasurementFaker WithId(this StrategicInitiativeKpiMeasurementFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static StrategicInitiativeKpiMeasurementFaker WithKpiId(this StrategicInitiativeKpiMeasurementFaker faker, Guid kpiId)
    {
        faker.RuleFor(x => x.KpiId, kpiId);

        return faker;
    }

    public static StrategicInitiativeKpiMeasurementFaker WithActualValue(this StrategicInitiativeKpiMeasurementFaker faker, double actualValue)
    {
        faker.RuleFor(x => x.ActualValue, actualValue);

        return faker;
    }

    public static StrategicInitiativeKpiMeasurementFaker WithMeasurementDate(this StrategicInitiativeKpiMeasurementFaker faker, Instant measurementDate)
    {
        faker.RuleFor(x => x.MeasurementDate, measurementDate);

        return faker;
    }

    public static StrategicInitiativeKpiMeasurementFaker WithMeasuredById(this StrategicInitiativeKpiMeasurementFaker faker, Guid? measuredById)
    {
        faker.RuleFor(x => x.MeasuredById, measuredById);

        return faker;
    }

    public static StrategicInitiativeKpiMeasurementFaker WithNote(this StrategicInitiativeKpiMeasurementFaker faker, string? note)
    {
        faker.RuleFor(x => x.Note, note);

        return faker;
    }
}