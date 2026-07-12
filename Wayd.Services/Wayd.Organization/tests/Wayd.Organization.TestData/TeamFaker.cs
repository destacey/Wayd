using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Domain.Models;
using NodaTime;
using Wayd.TestData.Core;

namespace Wayd.Organization.TestData;

public class TeamFaker : PrivateConstructorFaker<Team>
{
    public TeamFaker(LocalDate? activeDate = null)
    {
        activeDate ??= LocalDate.FromDateTime(FakerHub.Date.Recent()).PlusMonths(-6);

        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1000, 10000));
        RuleFor(x => x.Name, f => f.Random.Words(3));
        RuleFor(x => x.Code, f => new TeamCode(f.Random.AlphaNumeric(5)));
        RuleFor(x => x.Description, f => f.Random.Words(5));
        RuleFor(x => x.Type, f => TeamType.Team);
        RuleFor(x => x.ActiveDate, f => activeDate);
        RuleFor(x => x.IsActive, f => true);
    }
}

public static class TeamFakerExtensions
{
    public static TeamFaker WithId(this TeamFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static TeamFaker WithKey(this TeamFaker faker, int key)
    {
        faker.RuleFor(x => x.Key, key);
        return faker;
    }

    public static TeamFaker WithName(this TeamFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static TeamFaker WithCode(this TeamFaker faker, TeamCode code)
    {
        faker.RuleFor(x => x.Code, code);
        return faker;
    }

    public static TeamFaker WithDescription(this TeamFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);
        return faker;
    }

    public static TeamFaker WithActiveDate(this TeamFaker faker, LocalDate activeDate)
    {
        faker.RuleFor(x => x.ActiveDate, activeDate);
        return faker;
    }

    public static TeamFaker WithInactiveDate(this TeamFaker faker, LocalDate? inactiveDate)
    {
        faker.RuleFor(x => x.InactiveDate, inactiveDate);
        return faker;
    }

    public static TeamFaker WithIsActive(this TeamFaker faker, bool isActive)
    {
        faker.RuleFor(x => x.IsActive, isActive);
        return faker;
    }

    public static TeamFaker AsActive(this TeamFaker faker)
    {
        faker.RuleFor(x => x.IsActive, true);
        faker.RuleFor(x => x.InactiveDate, (LocalDate?)null);
        return faker;
    }

    public static TeamFaker AsInactive(this TeamFaker faker, LocalDate? inactiveDate = null)
    {
        var actualInactiveDate = inactiveDate ?? new LocalDate(2025, 5, 20);

        faker.RuleFor(x => x.IsActive, false);
        faker.RuleFor(x => x.InactiveDate, actualInactiveDate);
        return faker;
    }

    public static TeamFaker WithOperatingModel(this TeamFaker faker, TeamOperatingModelFaker operatingModelFaker, LocalDate? activeDate = null)
    {
        var actualActiveDate = activeDate ?? new LocalDate(2025, 5, 20);

        var operatingModel = operatingModelFaker.WithDateRange(actualActiveDate, null).Generate();

        faker.RuleFor(x => x.ActiveDate, actualActiveDate);
        faker.RuleFor("_operatingModels", f => new List<TeamOperatingModel> { operatingModel });

        return faker;
    }
}
