using NodaTime;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.ProductManagement.Domain.Tests.Data;

public sealed class ReleasePackageFaker : PrivateConstructorFaker<ReleasePackage>
{
    public ReleasePackageFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1, 10000));
        RuleFor(x => x.Version, f => $"{f.Date.Past().Year}.{f.Random.Int(1, 52):00}");
        RuleFor(x => x.Name, f => null);
        RuleFor(x => x.TargetDate, f => null);
        RuleFor(x => x.ReleasedDate, f => null);
        RuleFor(x => x.StatusId, f => f.Random.Guid());
        // A real record always has one: ApplyStatus sets it from the StatusRef it is given,
        // and building the outgoing side of a transition needs it.
        RuleFor(x => x.StatusWorkflowId, f => f.Random.Guid());
        RuleFor(x => x.StatusName, f => "Seeded Status");
        RuleFor(x => x.StatusCategory, f => StatusCategory.Proposed);
    }
}

public static class ReleasePackageFakerExtensions
{
    public static ReleasePackageFaker WithId(this ReleasePackageFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static ReleasePackageFaker WithKey(this ReleasePackageFaker faker, int key)
    {
        faker.RuleFor(x => x.Key, key);

        return faker;
    }

    public static ReleasePackageFaker WithVersion(this ReleasePackageFaker faker, string version)
    {
        faker.RuleFor(x => x.Version, version);

        return faker;
    }

    public static ReleasePackageFaker WithName(this ReleasePackageFaker faker, string? name)
    {
        faker.RuleFor(x => x.Name, name);

        return faker;
    }

    public static ReleasePackageFaker WithTargetDate(this ReleasePackageFaker faker, LocalDate? targetDate)
    {
        faker.RuleFor(x => x.TargetDate, targetDate);

        return faker;
    }

    public static ReleasePackageFaker WithReleasedDate(this ReleasePackageFaker faker, LocalDate? releasedDate)
    {
        faker.RuleFor(x => x.ReleasedDate, releasedDate);

        return faker;
    }

    public static ReleasePackageFaker WithStatusId(this ReleasePackageFaker faker, Guid statusId)
    {
        faker.RuleFor(x => x.StatusId, statusId);

        return faker;
    }

    public static ReleasePackageFaker WithStatusName(this ReleasePackageFaker faker, string statusName)
    {
        faker.RuleFor(x => x.StatusName, statusName);

        return faker;
    }

    public static ReleasePackageFaker WithStatusCategory(this ReleasePackageFaker faker, StatusCategory category)
    {
        faker.RuleFor(x => x.StatusCategory, category);

        return faker;
    }

    public static ReleasePackageFaker WithComponents(this ReleasePackageFaker faker, IEnumerable<ReleasePackageComponent> components)
    {
        faker.RuleFor("_components", _ => components.ToList());

        return faker;
    }

    public static ReleasePackageFaker AsWithdrawn(this ReleasePackageFaker faker)
    {
        faker.RuleFor(x => x.StatusCategory, StatusCategory.Removed);

        return faker;
    }
}
