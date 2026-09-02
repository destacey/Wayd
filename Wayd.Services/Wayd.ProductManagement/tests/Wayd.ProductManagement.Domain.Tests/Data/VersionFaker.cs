using NodaTime;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Domain.Models;
using Wayd.TestData.Core;

// The delivery artifact record, not System.Version.
using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.ProductManagement.Domain.Tests.Data;

public sealed class VersionFaker : PrivateConstructorFaker<Version>
{
    public VersionFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1, 10000));
        RuleFor(x => x.ProductId, f => f.Random.Guid());
        RuleFor(x => x.Number, f => $"{f.Random.Int(1, 9)}.{f.Random.Int(0, 9)}.{f.Random.Int(0, 9)}");
        RuleFor(x => x.Name, f => null);
        RuleFor(x => x.Sequence, f => null);
        RuleFor(x => x.TargetDate, f => null);
        RuleFor(x => x.CutDate, f => null);
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

public static class VersionFakerExtensions
{
    public static VersionFaker WithId(this VersionFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static VersionFaker WithKey(this VersionFaker faker, int key)
    {
        faker.RuleFor(x => x.Key, key);

        return faker;
    }

    public static VersionFaker WithProductId(this VersionFaker faker, Guid productId)
    {
        faker.RuleFor(x => x.ProductId, productId);

        return faker;
    }

    public static VersionFaker WithNumber(this VersionFaker faker, string number)
    {
        faker.RuleFor(x => x.Number, number);

        return faker;
    }

    public static VersionFaker WithName(this VersionFaker faker, string? name)
    {
        faker.RuleFor(x => x.Name, name);

        return faker;
    }

    public static VersionFaker WithSequence(this VersionFaker faker, long? sequence)
    {
        faker.RuleFor(x => x.Sequence, sequence);

        return faker;
    }

    public static VersionFaker WithTargetDate(this VersionFaker faker, LocalDate? targetDate)
    {
        faker.RuleFor(x => x.TargetDate, targetDate);

        return faker;
    }

    public static VersionFaker WithCutDate(this VersionFaker faker, LocalDate? cutDate)
    {
        faker.RuleFor(x => x.CutDate, cutDate);

        return faker;
    }

    public static VersionFaker WithReleasedDate(this VersionFaker faker, LocalDate? releasedDate)
    {
        faker.RuleFor(x => x.ReleasedDate, releasedDate);

        return faker;
    }

    public static VersionFaker WithNotes(this VersionFaker faker, string? notes)
    {
        faker.RuleFor(x => x.Notes, notes);

        return faker;
    }

    public static VersionFaker WithStatusId(this VersionFaker faker, Guid statusId)
    {
        faker.RuleFor(x => x.StatusId, statusId);

        return faker;
    }

    public static VersionFaker WithStatusName(this VersionFaker faker, string statusName)
    {
        faker.RuleFor(x => x.StatusName, statusName);

        return faker;
    }

    public static VersionFaker WithStatusCategory(this VersionFaker faker, StatusCategory category)
    {
        faker.RuleFor(x => x.StatusCategory, category);

        return faker;
    }

    /// <summary>
    /// A version that has been cut but not yet released.
    /// </summary>
    public static VersionFaker AsCut(this VersionFaker faker, LocalDate cutDate)
    {
        faker.RuleFor(x => x.CutDate, cutDate);
        faker.RuleFor(x => x.StatusCategory, StatusCategory.Active);

        return faker;
    }

    /// <summary>
    /// A version that has shipped.
    /// </summary>
    /// <param name="cutDate">
    /// Nullable because cutting is not a prerequisite for releasing — a version entered after the fact
    /// legitimately ships with no cut date, and historical import depends on it.
    /// </param>
    public static VersionFaker AsReleased(this VersionFaker faker, LocalDate? cutDate, LocalDate releasedDate)
    {
        faker.RuleFor(x => x.CutDate, cutDate);
        faker.RuleFor(x => x.ReleasedDate, releasedDate);
        faker.RuleFor(x => x.StatusCategory, StatusCategory.Done);

        return faker;
    }

    /// <summary>
    /// A version that was pulled after being cut.
    /// </summary>
    public static VersionFaker AsWithdrawn(this VersionFaker faker)
    {
        faker.RuleFor(x => x.StatusCategory, StatusCategory.Removed);

        return faker;
    }
}
