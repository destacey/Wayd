using System.Reflection;
using Wayd.ProductManagement.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.ProductManagement.Domain.Tests.Data;

/// <summary>
/// A persisted tag axis: active, organization-managed and taking several tags unless told otherwise.
/// </summary>
public sealed class ProductTagCategoryFaker : PrivateConstructorFaker<ProductTagCategory>
{
    public ProductTagCategoryFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1, 10000));
        RuleFor(x => x.Name, f => f.PickRandom("Platform", "Tech Stack", "Compliance") + " " + f.Random.AlphaNumeric(4));
        RuleFor(x => x.AllowsMany, true);
        RuleFor(x => x.Order, f => f.Random.Int(1, 8));
        RuleFor(x => x.IsSystem, false);
        RuleFor(x => x.IsActive, true);
    }
}

public static class ProductTagCategoryFakerExtensions
{
    public static ProductTagCategoryFaker WithName(this ProductTagCategoryFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);

        return faker;
    }

    public static ProductTagCategoryFaker WithDescription(this ProductTagCategoryFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);

        return faker;
    }

    public static ProductTagCategoryFaker WithAllowsMany(this ProductTagCategoryFaker faker, bool allowsMany)
    {
        faker.RuleFor(x => x.AllowsMany, allowsMany);

        return faker;
    }

    public static ProductTagCategoryFaker WithOrder(this ProductTagCategoryFaker faker, int order)
    {
        faker.RuleFor(x => x.Order, order);

        return faker;
    }

    public static ProductTagCategoryFaker WithIsSystem(this ProductTagCategoryFaker faker, bool isSystem)
    {
        faker.RuleFor(x => x.IsSystem, isSystem);

        return faker;
    }

    public static ProductTagCategoryFaker WithIsActive(this ProductTagCategoryFaker faker, bool isActive)
    {
        faker.RuleFor(x => x.IsActive, isActive);

        return faker;
    }

    /// <summary>
    /// Gives the axis active tags with the names, as already saved: no events, and on a system axis too, which
    /// refuses new tags through its own methods.
    /// </summary>
    public static ProductTagCategoryFaker WithTags(this ProductTagCategoryFaker faker, params string[] names)
    {
        faker.FinishWith((_, category) =>
        {
            var tags = (List<ProductTag>)typeof(ProductTagCategory)
                .GetField("_tags", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(category)!;
            tags.Clear();
            tags.AddRange(names.Select(name => new ProductTag(category.Id, name, null)));
        });

        return faker;
    }
}
