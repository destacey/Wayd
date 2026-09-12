using Wayd.Common.Domain.Enums.Organization;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models;
using Wayd.Tests.Shared.Data;
using Wayd.TestData.Core;

namespace Wayd.Planning.Domain.Tests.Data;

public class PlanningIntervalObjectiveFaker : PrivateConstructorFaker<PlanningIntervalObjective>
{
    public PlanningIntervalObjectiveFaker(Guid planningIntervalId, PlanningTeam team, ObjectiveStatus status, bool isStretch)
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int());
        RuleFor(x => x.PlanningIntervalId, planningIntervalId);
        RuleFor(x => x.TeamId, team.Id);
        RuleFor(x => x.Team, team);
        RuleFor(x => x.Name, f => f.Lorem.Sentence(4));
        RuleFor(x => x.Description, f => f.Lorem.Sentence(10));
        RuleFor(x => x.Type, SetType(team));
        RuleFor(x => x.Status, status);
        RuleFor(x => x.Progress, SetProgress(status));
        RuleFor(x => x.IsStretch, isStretch);
    }

    private static PlanningIntervalObjectiveType SetType(PlanningTeam team)
    {
        return team.Type switch
        {
            TeamType.Team => PlanningIntervalObjectiveType.Team,
            TeamType.TeamOfTeams => PlanningIntervalObjectiveType.TeamOfTeams,
            _ => throw new ArgumentOutOfRangeException(nameof(team.Type), team.Type, null)
        };
    }

    private static double SetProgress(ObjectiveStatus status)
    {
        return status switch
        {
            ObjectiveStatus.NotStarted => 0,
            ObjectiveStatus.InProgress => 50,
            ObjectiveStatus.Completed => 100,
            ObjectiveStatus.Canceled => 0,
            ObjectiveStatus.Missed => 50,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }
}

public static class PlanningIntervalObjectiveFakerExtensions
{
    public static PlanningIntervalObjectiveFaker WithName(this PlanningIntervalObjectiveFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static PlanningIntervalObjectiveFaker WithDates(this PlanningIntervalObjectiveFaker faker, LocalDate? startDate, LocalDate? targetDate)
    {
        faker.RuleFor(x => x.StartDate, startDate);
        faker.RuleFor(x => x.TargetDate, targetDate);
        return faker;
    }

    public static PlanningIntervalObjectiveFaker WithOrder(this PlanningIntervalObjectiveFaker faker, int? order)
    {
        faker.RuleFor(x => x.Order, order);
        return faker;
    }
}
