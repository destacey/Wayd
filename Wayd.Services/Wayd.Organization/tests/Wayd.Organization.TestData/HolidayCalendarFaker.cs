using Wayd.Organization.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.Organization.TestData;

public class HolidayCalendarFaker : PrivateConstructorFaker<HolidayCalendar>
{
    public HolidayCalendarFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1000, 10000));
        RuleFor(x => x.Name, f => f.Address.Country());
        RuleFor(x => x.Description, f => f.Random.Words(5));
    }
}

public static class HolidayCalendarFakerExtensions
{
    public static HolidayCalendarFaker WithId(this HolidayCalendarFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static HolidayCalendarFaker WithKey(this HolidayCalendarFaker faker, int key)
    {
        faker.RuleFor(x => x.Key, key);
        return faker;
    }

    public static HolidayCalendarFaker WithName(this HolidayCalendarFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static HolidayCalendarFaker WithDescription(this HolidayCalendarFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);
        return faker;
    }
}
