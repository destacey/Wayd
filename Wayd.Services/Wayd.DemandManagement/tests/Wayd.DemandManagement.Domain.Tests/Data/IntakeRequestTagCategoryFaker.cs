using System.Reflection;
using Wayd.DemandManagement.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.DemandManagement.Domain.Tests.Data;

/// <summary>
/// A persisted tag axis: active, company-managed and taking several tags unless told otherwise.
/// </summary>
public sealed class IntakeRequestTagCategoryFaker : PrivateConstructorFaker<IntakeRequestTagCategory>
{
    public IntakeRequestTagCategoryFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Name, f => f.PickRandom("Area", "Customer segment", "Review cycle") + " " + f.Random.AlphaNumeric(4));
        RuleFor(x => x.AllowsMany, true);
        RuleFor(x => x.Order, f => f.Random.Int(1, 8));
        RuleFor(x => x.IsSystem, false);
        RuleFor(x => x.IsActive, true);
    }
}

public static class IntakeRequestTagCategoryFakerExtensions
{
    public static IntakeRequestTagCategoryFaker WithName(this IntakeRequestTagCategoryFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);

        return faker;
    }

    public static IntakeRequestTagCategoryFaker WithDescription(this IntakeRequestTagCategoryFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);

        return faker;
    }

    public static IntakeRequestTagCategoryFaker WithAllowsMany(this IntakeRequestTagCategoryFaker faker, bool allowsMany)
    {
        faker.RuleFor(x => x.AllowsMany, allowsMany);

        return faker;
    }

    public static IntakeRequestTagCategoryFaker WithOrder(this IntakeRequestTagCategoryFaker faker, int order)
    {
        faker.RuleFor(x => x.Order, order);

        return faker;
    }

    public static IntakeRequestTagCategoryFaker WithIsSystem(this IntakeRequestTagCategoryFaker faker, bool isSystem)
    {
        faker.RuleFor(x => x.IsSystem, isSystem);

        return faker;
    }

    public static IntakeRequestTagCategoryFaker WithIsActive(this IntakeRequestTagCategoryFaker faker, bool isActive)
    {
        faker.RuleFor(x => x.IsActive, isActive);

        return faker;
    }

    /// <summary>
    /// Gives the axis active tags with the names, as already saved: no events, and on a system axis too, which
    /// refuses new tags through its own methods.
    /// </summary>
    public static IntakeRequestTagCategoryFaker WithTags(this IntakeRequestTagCategoryFaker faker, params string[] names)
    {
        faker.FinishWith((_, category) =>
        {
            var tags = (List<IntakeRequestTag>)typeof(IntakeRequestTagCategory)
                .GetField("_tags", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(category)!;
            tags.Clear();
            tags.AddRange(names.Select(name => new IntakeRequestTag(category.Id, name, null)));
        });

        return faker;
    }
}
