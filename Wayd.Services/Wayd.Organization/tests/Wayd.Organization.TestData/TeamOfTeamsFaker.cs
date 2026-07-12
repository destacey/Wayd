using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Domain.Models;
using NodaTime;
using Wayd.TestData.Core;

namespace Wayd.Organization.TestData;

public class TeamOfTeamsFaker : PrivateConstructorFaker<TeamOfTeams>
{
    public TeamOfTeamsFaker(LocalDate? activeDate = null)
    {
        activeDate ??= LocalDate.FromDateTime(FakerHub.Date.Recent()).PlusMonths(-6);

        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1000, 10000));
        RuleFor(x => x.Name, f => f.Random.Words(3));
        RuleFor(x => x.Code, f => new TeamCode(f.Random.AlphaNumeric(5)));
        RuleFor(x => x.Description, f => f.Random.Words(5));
        RuleFor(x => x.Type, f => TeamType.TeamOfTeams);
        RuleFor(x => x.ActiveDate, f => activeDate);
        RuleFor(x => x.IsActive, f => true);
    }
}

public static class TeamOfTeamsFakerExtensions
{
    public static TeamOfTeamsFaker WithId(this TeamOfTeamsFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static TeamOfTeamsFaker WithKey(this TeamOfTeamsFaker faker, int key)
    {
        faker.RuleFor(x => x.Key, key);
        return faker;
    }

    public static TeamOfTeamsFaker WithName(this TeamOfTeamsFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static TeamOfTeamsFaker WithCode(this TeamOfTeamsFaker faker, TeamCode code)
    {
        faker.RuleFor(x => x.Code, code);
        return faker;
    }

    public static TeamOfTeamsFaker WithDescription(this TeamOfTeamsFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);
        return faker;
    }

    public static TeamOfTeamsFaker WithActiveDate(this TeamOfTeamsFaker faker, LocalDate activeDate)
    {
        faker.RuleFor(x => x.ActiveDate, activeDate);
        return faker;
    }

    public static TeamOfTeamsFaker WithInactiveDate(this TeamOfTeamsFaker faker, LocalDate? inactiveDate)
    {
        faker.RuleFor(x => x.InactiveDate, inactiveDate);
        return faker;
    }

    public static TeamOfTeamsFaker WithIsActive(this TeamOfTeamsFaker faker, bool isActive)
    {
        faker.RuleFor(x => x.IsActive, isActive);
        return faker;
    }

    public static TeamOfTeamsFaker AsActive(this TeamOfTeamsFaker faker)
    {
        faker.RuleFor(x => x.IsActive, true);
        faker.RuleFor(x => x.InactiveDate, (LocalDate?)null);
        return faker;
    }

    public static TeamOfTeamsFaker AsInactive(this TeamOfTeamsFaker faker, LocalDate? inactiveDate = null)
    {
        var actualInactiveDate = inactiveDate ?? new LocalDate(2025, 5, 20);
        faker.RuleFor(x => x.IsActive, false);
        faker.RuleFor(x => x.InactiveDate, actualInactiveDate);
        return faker;
    }
}
