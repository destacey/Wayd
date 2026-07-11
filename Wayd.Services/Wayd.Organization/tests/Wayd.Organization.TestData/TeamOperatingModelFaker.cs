using Wayd.Organization.Domain.Enums;
using Wayd.Organization.Domain.Models;
using NodaTime;
using Wayd.TestData.Core;

namespace Wayd.Organization.TestData;

public class TeamOperatingModelFaker : PrivateConstructorFaker<TeamOperatingModel>
{
    public TeamOperatingModelFaker(LocalDate? startDate = null)
    {
        startDate ??= LocalDate.FromDateTime(FakerHub.Date.Past(1));

        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.DateRange, f => new OperatingModelDateRange(startDate.Value, null));
        RuleFor(x => x.Methodology, f => f.PickRandom<Methodology>());
        RuleFor(x => x.SizingMethod, f => f.PickRandom<SizingMethod>());
    }
}

public static class TeamOperatingModelFakerExtensions
{
    public static TeamOperatingModelFaker WithId(this TeamOperatingModelFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static TeamOperatingModelFaker WithDateRange(this TeamOperatingModelFaker faker, LocalDate start, LocalDate? end)
    {
        faker.RuleFor(x => x.DateRange, new OperatingModelDateRange(start, end));
        return faker;
    }

    public static TeamOperatingModelFaker WithStartDate(this TeamOperatingModelFaker faker, LocalDate startDate)
    {
        faker.RuleFor(x => x.DateRange, f => new OperatingModelDateRange(startDate, null));
        return faker;
    }

    public static TeamOperatingModelFaker WithMethodology(this TeamOperatingModelFaker faker, Methodology methodology)
    {
        faker.RuleFor(x => x.Methodology, methodology);
        return faker;
    }

    public static TeamOperatingModelFaker WithSizingMethod(this TeamOperatingModelFaker faker, SizingMethod sizingMethod)
    {
        faker.RuleFor(x => x.SizingMethod, sizingMethod);
        return faker;
    }

    public static TeamOperatingModelFaker AsCurrent(this TeamOperatingModelFaker faker)
    {
        faker.RuleFor(x => x.DateRange, f => new OperatingModelDateRange(
            LocalDate.FromDateTime(f.Date.Recent()).PlusMonths(-3),
            null));
        return faker;
    }

    public static TeamOperatingModelFaker AsClosed(this TeamOperatingModelFaker faker, LocalDate? endDate = null)
    {
        var actualEndDate = endDate ?? new LocalDate(2025, 5, 20);

        faker.RuleFor(x => x.DateRange, f => new OperatingModelDateRange(
            actualEndDate.PlusDays(-90),
            actualEndDate));
        return faker;
    }
}
