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
        RuleFor(x => x.ProductId, f => f.Random.Guid());
        RuleFor(x => x.Version, f => $"{f.Random.Int(1, 9)}.{f.Random.Int(0, 9)}.{f.Random.Int(0, 9)}");
        RuleFor(x => x.Name, f => null);
        RuleFor(x => x.Sequence, f => null);
        RuleFor(x => x.TargetDate, f => null);
        RuleFor(x => x.CutDate, f => null);
        RuleFor(x => x.ReleasedDate, f => null);
        RuleFor(x => x.Notes, f => null);
        RuleFor(x => x.PackageId, f => null);
        RuleFor(x => x.StatusId, f => f.Random.Guid());
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

    public static ReleaseFaker WithProductId(this ReleaseFaker faker, Guid productId)
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

    public static ReleaseFaker WithCutDate(this ReleaseFaker faker, LocalDate? cutDate)
    {
        faker.RuleFor(x => x.CutDate, cutDate);

        return faker;
    }

    public static ReleaseFaker WithReleasedDate(this ReleaseFaker faker, LocalDate? releasedDate)
    {
        faker.RuleFor(x => x.ReleasedDate, releasedDate);

        return faker;
    }

    public static ReleaseFaker WithNotes(this ReleaseFaker faker, string? notes)
    {
        faker.RuleFor(x => x.Notes, notes);

        return faker;
    }

    public static ReleaseFaker WithPackageId(this ReleaseFaker faker, Guid? packageId)
    {
        faker.RuleFor(x => x.PackageId, packageId);

        return faker;
    }

    public static ReleaseFaker WithStatusId(this ReleaseFaker faker, Guid statusId)
    {
        faker.RuleFor(x => x.StatusId, statusId);

        return faker;
    }

    public static ReleaseFaker WithStatusName(this ReleaseFaker faker, string statusName)
    {
        faker.RuleFor(x => x.StatusName, statusName);

        return faker;
    }

    public static ReleaseFaker WithStatusCategory(this ReleaseFaker faker, StatusCategory category)
    {
        faker.RuleFor(x => x.StatusCategory, category);

        return faker;
    }

    /// <summary>
    /// A release that has been cut but not yet released.
    /// </summary>
    public static ReleaseFaker AsCut(this ReleaseFaker faker, LocalDate cutDate)
    {
        faker.RuleFor(x => x.CutDate, cutDate);
        faker.RuleFor(x => x.StatusCategory, StatusCategory.Active);

        return faker;
    }

    /// <summary>
    /// A release that has shipped.
    /// </summary>
    public static ReleaseFaker AsReleased(this ReleaseFaker faker, LocalDate cutDate, LocalDate releasedDate)
    {
        faker.RuleFor(x => x.CutDate, cutDate);
        faker.RuleFor(x => x.ReleasedDate, releasedDate);
        faker.RuleFor(x => x.StatusCategory, StatusCategory.Done);

        return faker;
    }

    /// <summary>
    /// A release that was pulled after being cut.
    /// </summary>
    public static ReleaseFaker AsWithdrawn(this ReleaseFaker faker)
    {
        faker.RuleFor(x => x.StatusCategory, StatusCategory.Removed);

        return faker;
    }
}
