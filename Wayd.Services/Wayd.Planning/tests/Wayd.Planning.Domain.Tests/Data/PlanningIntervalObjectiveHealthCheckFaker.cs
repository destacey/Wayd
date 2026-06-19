using Wayd.Common.Domain.Enums;
using Wayd.Planning.Domain.Models;
using Wayd.Tests.Shared.Data;

namespace Wayd.Planning.Domain.Tests.Data;

public sealed class PlanningIntervalObjectiveHealthCheckFaker : PrivateConstructorFaker<PlanningIntervalObjectiveHealthCheck>
{
    public PlanningIntervalObjectiveHealthCheckFaker(Guid planningIntervalObjectiveId, Instant reportedOn, Duration? expirationOffset = null)
    {
        var offset = expirationOffset ?? Duration.FromDays(14);

        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.PlanningIntervalObjectiveId, planningIntervalObjectiveId);
        RuleFor(x => x.Status, f => f.PickRandom<HealthStatus>());
        RuleFor(x => x.ReportedById, f => f.Random.Guid());
        RuleFor(x => x.ReportedOn, reportedOn);
        RuleFor(x => x.Expiration, reportedOn.Plus(offset));
        RuleFor(x => x.Note, f => f.Lorem.Sentence(8));
    }
}

public static class PlanningIntervalObjectiveHealthCheckFakerExtensions
{
    public static PlanningIntervalObjectiveHealthCheckFaker WithStatus(
        this PlanningIntervalObjectiveHealthCheckFaker faker, HealthStatus status)
    {
        faker.RuleFor(x => x.Status, status);
        return faker;
    }

    public static PlanningIntervalObjectiveHealthCheckFaker WithExpiration(
        this PlanningIntervalObjectiveHealthCheckFaker faker, Instant expiration)
    {
        faker.RuleFor(x => x.Expiration, expiration);
        return faker;
    }

    public static PlanningIntervalObjectiveHealthCheckFaker WithReportedById(
        this PlanningIntervalObjectiveHealthCheckFaker faker, Guid reportedById)
    {
        faker.RuleFor(x => x.ReportedById, reportedById);
        return faker;
    }
}
