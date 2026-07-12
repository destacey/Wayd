using Wayd.Common.Domain.Tests.Data.Models;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Data;
using Wayd.TestData.Core;

namespace Wayd.Common.Domain.Tests.Data;

public sealed class TestKpiCheckpointFaker : PrivateConstructorFaker<TestKpiCheckpoint>
{
    public TestKpiCheckpointFaker(TestingDateTimeProvider dateTimeProvider)
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

public static class TestKpiCheckpointFakerExtensions
{
    public static TestKpiCheckpointFaker WithId(this TestKpiCheckpointFaker faker, Guid? id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static TestKpiCheckpointFaker WithKpiId(this TestKpiCheckpointFaker faker, Guid? kpiId)
    {
        faker.RuleFor(x => x.KpiId, kpiId);

        return faker;
    }

    public static TestKpiCheckpointFaker WithTargetValue(this TestKpiCheckpointFaker faker, double? targetValue)
    {
        faker.RuleFor(x => x.TargetValue, targetValue);

        return faker;
    }

    public static TestKpiCheckpointFaker WithAtRiskValue(this TestKpiCheckpointFaker faker, double? atRiskValue)
    {
        faker.RuleFor(x => x.AtRiskValue, atRiskValue);

        return faker;
    }

    public static TestKpiCheckpointFaker WithCheckpointDate(this TestKpiCheckpointFaker faker, Instant? checkpointDate)
    {
        faker.RuleFor(x => x.CheckpointDate, checkpointDate);

        return faker;
    }

    public static TestKpiCheckpointFaker WithDateLabel(this TestKpiCheckpointFaker faker, string? dateLabel)
    {
        faker.RuleFor(x => x.DateLabel, dateLabel);

        return faker;
    }
}
