using Wayd.Common.Domain.Enums;
using Wayd.Tests.Shared.Data;
using Wayd.Work.Domain.Models;

namespace Wayd.Work.Domain.Tests.Data;

public class WorkProcessFaker : PrivateConstructorFaker<WorkProcess>
{
    public WorkProcessFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1, 10000));
        RuleFor(x => x.Name, f => f.Company.CompanyName());
        RuleFor(x => x.Description, f => f.Lorem.Sentence());
        RuleFor(x => x.Ownership, Ownership.Owned);
        RuleFor(x => x.ExternalId, (Guid?)null);
        RuleFor(x => x.IsActive, true);
        RuleFor("_schemes", f => new List<WorkProcessScheme>());
    }
}

public static class WorkProcessFakerExtensions
{
    public static WorkProcessFaker WithId(this WorkProcessFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static WorkProcessFaker WithKey(this WorkProcessFaker faker, int key)
    {
        faker.RuleFor(x => x.Key, key);
        return faker;
    }

    public static WorkProcessFaker WithName(this WorkProcessFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static WorkProcessFaker WithDescription(this WorkProcessFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);
        return faker;
    }

    public static WorkProcessFaker WithOwnership(this WorkProcessFaker faker, Ownership ownership)
    {
        faker.RuleFor(x => x.Ownership, ownership);
        return faker;
    }

    public static WorkProcessFaker WithExternalId(this WorkProcessFaker faker, Guid? externalId)
    {
        faker.RuleFor(x => x.ExternalId, externalId);
        return faker;
    }

    public static WorkProcessFaker WithIsActive(this WorkProcessFaker faker, bool isActive)
    {
        faker.RuleFor(x => x.IsActive, isActive);
        return faker;
    }

    public static WorkProcessFaker WithSchemes(this WorkProcessFaker faker, List<WorkProcessScheme> schemes)
    {
        faker.RuleFor("_schemes", f => schemes);
        return faker;
    }

    public static WorkProcessFaker AsExternal(this WorkProcessFaker faker, Guid? externalId = null)
    {
        var id = externalId ?? faker.Generate().Id;

        faker.WithOwnership(Ownership.Managed);
        faker.WithExternalId(externalId ?? id);

        return faker;
    }
}
