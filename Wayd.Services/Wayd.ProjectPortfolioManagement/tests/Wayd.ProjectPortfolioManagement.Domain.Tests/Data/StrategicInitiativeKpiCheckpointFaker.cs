using NodaTime;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Data;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

public sealed class StrategicInitiativeKpiCheckpointFaker : PrivateConstructorFaker<StrategicInitiativeKpiCheckpoint>
{
    public StrategicInitiativeKpiCheckpointFaker(TestingDateTimeProvider dateTimeProvider)
    {
        var pastDate = dateTimeProvider.Now.Minus(Duration.FromDays(FakerHub.Random.Int(1, 200)));

        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.KpiId, f => f.Random.Guid());
        RuleFor(x => x.TargetValue, f => f.Random.Double(0, 100));
        RuleFor(x => x.AtRiskValue, f => (double?)null);
        RuleFor(x => x.CheckpointDate, f => pastDate);
        RuleFor(x => x.DateLabel, f => f.Random.String2(1, 10));
    }
}

public static class StrategicInitiativeKpiCheckpointFakerExtensions
{
    public static StrategicInitiativeKpiCheckpointFaker WithId(this StrategicInitiativeKpiCheckpointFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static StrategicInitiativeKpiCheckpointFaker WithKpiId(this StrategicInitiativeKpiCheckpointFaker faker, Guid kpiId)
    {
        faker.RuleFor(x => x.KpiId, kpiId);

        return faker;
    }

    public static StrategicInitiativeKpiCheckpointFaker WithTargetValue(this StrategicInitiativeKpiCheckpointFaker faker, double targetValue)
    {
        faker.RuleFor(x => x.TargetValue, targetValue);

        return faker;
    }

    public static StrategicInitiativeKpiCheckpointFaker WithAtRiskValue(this StrategicInitiativeKpiCheckpointFaker faker, double? atRiskValue)
    {
        faker.RuleFor(x => x.AtRiskValue, atRiskValue);

        return faker;
    }

    public static StrategicInitiativeKpiCheckpointFaker WithCheckpointDate(this StrategicInitiativeKpiCheckpointFaker faker, Instant checkpointDate)
    {
        faker.RuleFor(x => x.CheckpointDate, checkpointDate);

        return faker;
    }

    public static StrategicInitiativeKpiCheckpointFaker WithDateLabel(this StrategicInitiativeKpiCheckpointFaker faker, string? dateLabel)
    {
        faker.RuleFor(x => x.DateLabel, dateLabel);

        return faker;
    }
}