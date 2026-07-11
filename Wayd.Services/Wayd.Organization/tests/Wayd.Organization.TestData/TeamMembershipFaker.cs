using Wayd.Organization.Domain.Models;
using NodaTime;
using Wayd.TestData.Core;

namespace Wayd.Organization.TestData;

public sealed class TeamMembershipFaker : PrivateConstructorFaker<TeamMembership>
{
    public TeamMembershipFaker()
    {
        var start = LocalDate.FromDateTime(DateTime.UtcNow.AddMonths(-3));

        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.SourceId, f => f.Random.Guid());
        RuleFor(x => x.TargetId, f => f.Random.Guid());
        RuleFor(x => x.DateRange, f => new MembershipDateRange(start, null));
    }
}

public static class TeamMembershipFakerExtensions
{
    public static TeamMembershipFaker WithId(this TeamMembershipFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static TeamMembershipFaker WithSourceId(this TeamMembershipFaker faker, Guid sourceId)
    {
        faker.RuleFor(x => x.SourceId, sourceId);
        return faker;
    }

    public static TeamMembershipFaker WithTargetId(this TeamMembershipFaker faker, Guid targetId)
    {
        faker.RuleFor(x => x.TargetId, targetId);
        return faker;
    }

    public static TeamMembershipFaker WithDateRange(this TeamMembershipFaker faker, MembershipDateRange dateRange)
    {
        faker.RuleFor(x => x.DateRange, dateRange);
        return faker;
    }

    public static TeamMembershipFaker WithStartDate(this TeamMembershipFaker faker, LocalDate start)
    {
        faker.RuleFor(x => x.DateRange, new MembershipDateRange(start, null));
        return faker;
    }

    public static TeamMembershipFaker WithEndDate(this TeamMembershipFaker faker, LocalDate start, LocalDate end)
    {
        faker.RuleFor(x => x.DateRange, new MembershipDateRange(start, end));
        return faker;
    }

    public static TeamMembershipFaker AsActive(this TeamMembershipFaker faker, LocalDate? start = null)
    {
        var actualStart = start ?? LocalDate.FromDateTime(DateTime.UtcNow.AddMonths(-3));
        faker.RuleFor(x => x.DateRange, new MembershipDateRange(actualStart, null));
        return faker;
    }

    public static TeamMembershipFaker AsPast(this TeamMembershipFaker faker)
    {
        var start = new LocalDate(2025, 5, 20);
        var end = start.PlusMonths(3);
        faker.RuleFor(x => x.DateRange, new MembershipDateRange(start, end));
        return faker;
    }

    public static TeamMembershipFaker AsFuture(this TeamMembershipFaker faker)
    {
        var start = LocalDate.FromDateTime(DateTime.UtcNow.AddMonths(1));
        faker.RuleFor(x => x.DateRange, new MembershipDateRange(start, null));
        return faker;
    }
}
