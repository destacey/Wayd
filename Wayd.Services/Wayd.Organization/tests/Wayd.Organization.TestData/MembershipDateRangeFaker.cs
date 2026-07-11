using Bogus;
using Wayd.Organization.Domain.Models;
using NodaTime;

namespace Wayd.Organization.TestData;

public sealed class MembershipDateRangeFaker : Faker<MembershipDateRange>
{
    public MembershipDateRangeFaker()
    {
        CustomInstantiator(f =>
        {
            var startDateTime = f.Date.Recent(days: 180); // Within last 180 days
            return new MembershipDateRange(LocalDate.FromDateTime(startDateTime), null);
        });
    }
}

public static class MembershipDateRangeFakerExtensions
{
    public static MembershipDateRangeFaker WithStart(this MembershipDateRangeFaker faker, LocalDate start)
    {
        faker.CustomInstantiator(f => new MembershipDateRange(start, null));
        return faker;
    }

    public static MembershipDateRangeFaker WithEnd(this MembershipDateRangeFaker faker, LocalDate start, LocalDate end)
    {
        faker.CustomInstantiator(f => new MembershipDateRange(start, end));
        return faker;
    }

    public static MembershipDateRangeFaker AsActive(this MembershipDateRangeFaker faker, LocalDate? start = null)
    {
        var actualStart = start ?? LocalDate.FromDateTime(DateTime.UtcNow.AddMonths(-3));
        faker.CustomInstantiator(f => new MembershipDateRange(actualStart, null));
        return faker;
    }

    public static MembershipDateRangeFaker AsPast(this MembershipDateRangeFaker faker)
    {
        var start = LocalDate.FromDateTime(DateTime.UtcNow.AddMonths(-6));
        var end = LocalDate.FromDateTime(DateTime.UtcNow.AddMonths(-1));
        faker.CustomInstantiator(f => new MembershipDateRange(start, end));
        return faker;
    }

    public static MembershipDateRangeFaker AsFuture(this MembershipDateRangeFaker faker)
    {
        var start = LocalDate.FromDateTime(DateTime.UtcNow.AddMonths(1));
        faker.CustomInstantiator(f => new MembershipDateRange(start, null));
        return faker;
    }
}
