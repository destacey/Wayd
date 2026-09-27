using Wayd.Organization.Domain.Models;
using NodaTime;
using Wayd.TestData.Core;

namespace Wayd.Organization.TestData;

public class TeamOfTeamsOperatingModelFaker : PrivateConstructorFaker<TeamOfTeamsOperatingModel>
{
    public TeamOfTeamsOperatingModelFaker(LocalDate? startDate = null)
    {
        startDate ??= LocalDate.FromDateTime(FakerHub.Date.Past(1));

        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.DateRange, f => new OperatingModelDateRange(startDate.Value, null));
        RuleFor(x => x.TimeZone, f => f.PickRandom("UTC", "America/New_York", "Europe/London", "Asia/Tokyo"));
    }
}

public static class TeamOfTeamsOperatingModelFakerExtensions
{
    public static TeamOfTeamsOperatingModelFaker WithId(this TeamOfTeamsOperatingModelFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static TeamOfTeamsOperatingModelFaker WithDateRange(this TeamOfTeamsOperatingModelFaker faker, LocalDate start, LocalDate? end)
    {
        faker.RuleFor(x => x.DateRange, new OperatingModelDateRange(start, end));
        return faker;
    }

    public static TeamOfTeamsOperatingModelFaker WithTimeZone(this TeamOfTeamsOperatingModelFaker faker, string timeZone)
    {
        faker.RuleFor(x => x.TimeZone, timeZone);
        return faker;
    }
}
