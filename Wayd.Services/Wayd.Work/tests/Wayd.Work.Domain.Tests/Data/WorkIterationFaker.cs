using NodaTime;
using NodaTime.Extensions;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Tests.Shared.Data;
using Wayd.Work.Domain.Models;

namespace Wayd.Work.Domain.Tests.Data;

public class WorkIterationFaker : PrivateConstructorFaker<WorkIteration>
{
    public WorkIterationFaker(Guid? teamId = null)
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1000, 10000));
        RuleFor(x => x.Name, f => f.Random.String2(10));
        RuleFor(x => x.Type, f => IterationType.Sprint);
        RuleFor(x => x.State, f => IterationState.Future);
        RuleFor(x => x.DateRange, f =>
        {
            var startDate = f.Date.Future().ToInstant();
            var endDate = startDate.Plus(Duration.FromDays(14));
            return new IterationDateRange(startDate, endDate);
        });
        RuleFor(x => x.TeamId, f => teamId ?? f.Random.Guid());
    }
}

public static class WorkIterationFakerExtensions
{
    public static WorkIterationFaker WithId(this WorkIterationFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static WorkIterationFaker WithKey(this WorkIterationFaker faker, int key)
    {
        faker.RuleFor(x => x.Key, key);

        return faker;
    }

    public static WorkIterationFaker WithName(this WorkIterationFaker faker, string? name)
    {
        faker.RuleFor(x => x.Name, name);

        return faker;
    }

    public static WorkIterationFaker WithType(this WorkIterationFaker faker, IterationType type)
    {
        faker.RuleFor(x => x.Type, type);

        return faker;
    }

    public static WorkIterationFaker WithState(this WorkIterationFaker faker, IterationState state)
    {
        faker.RuleFor(x => x.State, state);

        return faker;
    }

    public static WorkIterationFaker WithDateRange(this WorkIterationFaker faker, IterationDateRange dateRange)
    {
        faker.RuleFor(x => x.DateRange, dateRange);

        return faker;
    }

    public static WorkIterationFaker WithTeamId(this WorkIterationFaker faker, Guid? teamId)
    {
        faker.RuleFor(x => x.TeamId, teamId);

        return faker;
    }

    /// <summary>
    /// Creates a sprint with a specific end date. The start date is calculated as 14 days before the end date.
    /// </summary>
    /// <param name="faker">The WorkIterationFaker instance</param>
    /// <param name="endDate">The end date for the sprint</param>
    /// <param name="state">Optional state for the sprint (defaults to Active)</param>
    /// <param name="type">Optional type for the iteration (defaults to Sprint)</param>
    /// <returns>The configured faker</returns>
    public static WorkIterationFaker WithEndDate(this WorkIterationFaker faker, Instant endDate, IterationState state = IterationState.Active, IterationType type = IterationType.Sprint)
    {
        var startDate = endDate.Minus(Duration.FromDays(14));
        var dateRange = new IterationDateRange(startDate, endDate);

        faker.RuleFor(x => x.Type, type);
        faker.RuleFor(x => x.State, state);
        faker.RuleFor(x => x.DateRange, dateRange);

        return faker;
    }
}