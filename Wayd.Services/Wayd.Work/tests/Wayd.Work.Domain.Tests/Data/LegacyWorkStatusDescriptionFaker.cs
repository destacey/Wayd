using Wayd.Tests.Shared.Data;
using Wayd.Work.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.Work.Domain.Tests.Data;

public sealed class LegacyWorkStatusDescriptionFaker : PrivateConstructorFaker<LegacyWorkStatusDescription>
{
    public LegacyWorkStatusDescriptionFaker()
    {
        RuleFor(x => x.Name, f => f.Lorem.Word());
        RuleFor(x => x.Description, f => f.Lorem.Sentence());
    }
}

public static class LegacyWorkStatusDescriptionFakerExtensions
{
    public static LegacyWorkStatusDescriptionFaker WithName(this LegacyWorkStatusDescriptionFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static LegacyWorkStatusDescriptionFaker WithDescription(this LegacyWorkStatusDescriptionFaker faker, string description)
    {
        faker.RuleFor(x => x.Description, description);
        return faker;
    }
}
