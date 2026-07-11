using Wayd.Common.Domain.FeatureManagement;
using Wayd.TestData.Core;

namespace Wayd.Tests.Shared.Data;

public sealed class FeatureFlagFaker : PrivateConstructorFaker<FeatureFlag>
{
    public FeatureFlagFaker()
    {
        RuleFor(x => x.Id, f => f.IndexFaker + 1);
        RuleFor(x => x.Name, f => string.Join("-", f.Lorem.Words(f.Random.Int(2, 4))).ToLowerInvariant());
        RuleFor(x => x.DisplayName, f => f.Lorem.Sentence(3).TrimEnd('.'));
        RuleFor(x => x.Description, f => f.Random.Bool() ? f.Lorem.Sentence() : null);
        RuleFor(x => x.IsEnabled, f => f.Random.Bool());
        RuleFor(x => x.IsArchived, false);
        RuleFor(x => x.IsSystem, false);
        RuleFor(x => x.FiltersJson, (string?)null);
    }
}

public static class FeatureFlagFakerExtensions
{
    public static FeatureFlagFaker WithId(this FeatureFlagFaker faker, int id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static FeatureFlagFaker WithName(this FeatureFlagFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static FeatureFlagFaker WithDisplayName(this FeatureFlagFaker faker, string displayName)
    {
        faker.RuleFor(x => x.DisplayName, displayName);
        return faker;
    }

    public static FeatureFlagFaker WithDescription(this FeatureFlagFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);
        return faker;
    }

    public static FeatureFlagFaker AsEnabled(this FeatureFlagFaker faker)
    {
        faker.RuleFor(x => x.IsEnabled, true);
        return faker;
    }

    public static FeatureFlagFaker AsDisabled(this FeatureFlagFaker faker)
    {
        faker.RuleFor(x => x.IsEnabled, false);
        return faker;
    }

    public static FeatureFlagFaker AsArchived(this FeatureFlagFaker faker)
    {
        faker.RuleFor(x => x.IsArchived, true);
        faker.RuleFor(x => x.IsEnabled, false);
        return faker;
    }

    public static FeatureFlagFaker AsSystem(this FeatureFlagFaker faker)
    {
        faker.RuleFor(x => x.IsSystem, true);
        return faker;
    }
}
