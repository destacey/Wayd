using Bogus;
using Wayd.Organization.Domain.Models;
using NodaTime;

namespace Wayd.Organization.TestData;

public sealed class OperatingModelDateRangeFaker : Faker<OperatingModelDateRange>
{
    public OperatingModelDateRangeFaker()
    {
        CustomInstantiator(f =>
        {
            var startDateTime = f.Date.Recent(days: 180); // Within last 180 days
            return new OperatingModelDateRange(LocalDate.FromDateTime(startDateTime), null);
        });
    }
}

public static class OperatingModelDateRangeFakerExtensions
{
    public static OperatingModelDateRangeFaker WithStart(this OperatingModelDateRangeFaker faker, LocalDate start)
    {
        faker.CustomInstantiator(f => new OperatingModelDateRange(start, null));
        return faker;
    }

    public static OperatingModelDateRangeFaker WithEnd(this OperatingModelDateRangeFaker faker, LocalDate start, LocalDate end)
    {
        faker.CustomInstantiator(f => new OperatingModelDateRange(start, end));
        return faker;
    }

    public static OperatingModelDateRangeFaker AsCurrent(this OperatingModelDateRangeFaker faker, LocalDate? start = null)
    {
        var actualStart = start ?? LocalDate.FromDateTime(DateTime.UtcNow.AddMonths(-3));
        faker.CustomInstantiator(f => new OperatingModelDateRange(actualStart, null));
        return faker;
    }

    public static OperatingModelDateRangeFaker AsClosed(this OperatingModelDateRangeFaker faker)
    {
        var start = LocalDate.FromDateTime(DateTime.UtcNow.AddMonths(-6));
        var end = LocalDate.FromDateTime(DateTime.UtcNow.AddMonths(-1));
        faker.CustomInstantiator(f => new OperatingModelDateRange(start, end));
        return faker;
    }

    public static OperatingModelDateRangeFaker AsFuture(this OperatingModelDateRangeFaker faker)
    {
        var start = LocalDate.FromDateTime(DateTime.UtcNow.AddMonths(1));
        faker.CustomInstantiator(f => new OperatingModelDateRange(start, null));
        return faker;
    }
}
