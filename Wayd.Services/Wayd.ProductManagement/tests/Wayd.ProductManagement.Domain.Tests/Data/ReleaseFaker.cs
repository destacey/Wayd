using NodaTime;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.ProductManagement.Domain.Tests.Data;

public sealed class ReleaseFaker : PrivateConstructorFaker<Release>
{
    public ReleaseFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1, 10000));
        // Null by default: a release spanning product lines is as ordinary as one scoped to a node.
        RuleFor(x => x.ProductId, f => null);
        RuleFor(x => x.Version, f => $"{f.Date.Past().Year}.{f.Random.Int(1, 12):00}");
        RuleFor(x => x.Name, f => null);
        RuleFor(x => x.Sequence, f => null);
        RuleFor(x => x.TargetDate, f => null);
        RuleFor(x => x.ReleasedDate, f => null);
        RuleFor(x => x.Notes, f => null);
        RuleFor(x => x.StatusId, f => f.Random.Guid());
        // A real record always has one: ApplyStatus sets it from the StatusRef it is given,
        // and building the outgoing side of a transition needs it.
        RuleFor(x => x.StatusWorkflowId, f => f.Random.Guid());
        RuleFor(x => x.StatusName, f => "Seeded Status");
        RuleFor(x => x.StatusCategory, f => StatusCategory.Proposed);
    }
}

public static class ReleaseFakerExtensions
{
    public static ReleaseFaker WithId(this ReleaseFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static ReleaseFaker WithKey(this ReleaseFaker faker, int key)
    {
        faker.RuleFor(x => x.Key, key);

        return faker;
    }

    public static ReleaseFaker WithProductId(this ReleaseFaker faker, Guid? productId)
    {
        faker.RuleFor(x => x.ProductId, productId);

        return faker;
    }

    public static ReleaseFaker WithVersion(this ReleaseFaker faker, string version)
    {
        faker.RuleFor(x => x.Version, version);

        return faker;
    }

    public static ReleaseFaker WithName(this ReleaseFaker faker, string? name)
    {
        faker.RuleFor(x => x.Name, name);

        return faker;
    }

    public static ReleaseFaker WithNotes(this ReleaseFaker faker, string? notes)
    {
        faker.RuleFor(x => x.Notes, notes);

        return faker;
    }

    public static ReleaseFaker WithSequence(this ReleaseFaker faker, long? sequence)
    {
        faker.RuleFor(x => x.Sequence, sequence);

        return faker;
    }

    public static ReleaseFaker WithTargetDate(this ReleaseFaker faker, LocalDate? targetDate)
    {
        faker.RuleFor(x => x.TargetDate, targetDate);

        return faker;
    }

    public static ReleaseFaker WithStatusCategory(this ReleaseFaker faker, StatusCategory statusCategory)
    {
        faker.RuleFor(x => x.StatusCategory, statusCategory);

        return faker;
    }

    /// <summary>
    /// A release that has been announced.
    /// </summary>
    public static ReleaseFaker AsReleased(this ReleaseFaker faker, LocalDate releasedDate)
    {
        faker.RuleFor(x => x.ReleasedDate, releasedDate);
        faker.RuleFor(x => x.StatusCategory, StatusCategory.Done);

        return faker;
    }

    /// <summary>
    /// A release that was retracted after being announced.
    /// </summary>
    public static ReleaseFaker AsWithdrawn(this ReleaseFaker faker)
    {
        faker.RuleFor(x => x.StatusCategory, StatusCategory.Removed);

        return faker;
    }
}
