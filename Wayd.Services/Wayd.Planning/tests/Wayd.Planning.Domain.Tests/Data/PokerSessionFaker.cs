using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Tests.Shared.Data;
using Wayd.TestData.Core;

namespace Wayd.Planning.Domain.Tests.Data;

/// <summary>
/// An active, public poker session facilitated by <see cref="PokerSessionActors.Facilitator"/>.
/// </summary>
public class PokerSessionFaker : PrivateConstructorFaker<PokerSession>
{
    public PokerSessionFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1, 10000));
        RuleFor(x => x.Name, f => f.Lorem.Sentence(3));
        RuleFor(x => x.EstimationScaleId, f => f.Random.Guid());
        RuleFor(x => x.EstimationScaleVersionId, f => f.Random.Guid());
        RuleFor(x => x.Visibility, Visibility.Public);
        RuleFor(x => x.Status, PokerSessionStatus.Active);
        RuleFor("_facilitators", (f, s) => new List<PokerSessionFacilitator> { new(s.Id, PokerSessionActors.FacilitatorEmployeeId) });
    }
}

public static class PokerSessionFakerExtensions
{
    public static PokerSessionFaker WithId(this PokerSessionFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static PokerSessionFaker WithKey(this PokerSessionFaker faker, int key)
    {
        faker.RuleFor(x => x.Key, key);
        return faker;
    }

    public static PokerSessionFaker WithName(this PokerSessionFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    /// <summary>
    /// Puts the session on <paramref name="estimationScale"/>, pinned to its current version.
    /// </summary>
    public static PokerSessionFaker WithEstimationScale(this PokerSessionFaker faker, EstimationScale estimationScale)
    {
        faker.RuleFor(x => x.EstimationScaleId, estimationScale.Id);
        faker.RuleFor(x => x.EstimationScaleVersionId, estimationScale.CurrentVersion.Id);
        return faker;
    }

    public static PokerSessionFaker WithStatus(this PokerSessionFaker faker, PokerSessionStatus status)
    {
        faker.RuleFor(x => x.Status, status);
        return faker;
    }

    public static PokerSessionFaker WithVisibility(this PokerSessionFaker faker, Visibility visibility)
    {
        faker.RuleFor(x => x.Visibility, visibility);
        return faker;
    }

    public static PokerSessionFaker WithFacilitators(this PokerSessionFaker faker, params Guid[] employeeIds)
    {
        faker.RuleFor("_facilitators", (f, s) => employeeIds.Select(id => new PokerSessionFacilitator(s.Id, id)).ToList());
        return faker;
    }

    public static PokerSessionFaker WithInvitedEmployees(this PokerSessionFaker faker, params Guid[] employeeIds)
    {
        faker.RuleFor("_invitedEmployees", (f, s) => employeeIds.Select(id => new PokerSessionInvitedEmployee(s.Id, id)).ToList());
        return faker;
    }

    public static PokerSessionFaker WithInvitedTeams(this PokerSessionFaker faker, params Guid[] teamIds)
    {
        faker.RuleFor("_invitedTeams", (f, s) => teamIds.Select(id => new PokerSessionInvitedTeam(s.Id, id)).ToList());
        return faker;
    }

    /// <summary>A session held by the given intake desk, private as every hosted session is.</summary>
    public static PokerSessionFaker WithDeskHost(this PokerSessionFaker faker, Guid deskId)
    {
        faker.RuleFor(x => x.Host, new PokerSessionHost(PokerSessionHostKind.IntakeDesk, deskId));
        faker.RuleFor(x => x.Visibility, Visibility.Private);
        return faker;
    }
}
