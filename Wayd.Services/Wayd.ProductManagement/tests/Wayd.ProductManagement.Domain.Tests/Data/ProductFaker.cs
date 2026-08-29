using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Domain.Models;
using Wayd.TestData.Core;

namespace Wayd.ProductManagement.Domain.Tests.Data;

public sealed class ProductFaker : PrivateConstructorFaker<Product>
{
    public ProductFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1, 10000));
        RuleFor(x => x.Name, f => f.Commerce.ProductName());
        RuleFor(x => x.Description, f => f.Lorem.Sentence());
        RuleFor(x => x.ProductTypeId, f => f.Random.Guid());
        RuleFor(x => x.ParentId, f => null);
        RuleFor(x => x.ExternalId, f => null);
        RuleFor(x => x.StatusId, f => f.Random.Guid());
        RuleFor(x => x.StatusCategory, f => StatusCategory.Active);
        RuleFor(x => x.StatusAlias, f => ProductStatusAlias.Active);
    }
}

public static class ProductFakerExtensions
{
    public static ProductFaker WithId(this ProductFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static ProductFaker WithKey(this ProductFaker faker, int key)
    {
        faker.RuleFor(x => x.Key, key);

        return faker;
    }

    public static ProductFaker WithName(this ProductFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);

        return faker;
    }

    public static ProductFaker WithDescription(this ProductFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);

        return faker;
    }

    public static ProductFaker WithProductTypeId(this ProductFaker faker, Guid productTypeId)
    {
        faker.RuleFor(x => x.ProductTypeId, productTypeId);

        return faker;
    }

    public static ProductFaker WithParentId(this ProductFaker faker, Guid? parentId)
    {
        faker.RuleFor(x => x.ParentId, parentId);

        return faker;
    }

    public static ProductFaker WithExternalId(this ProductFaker faker, string? externalId)
    {
        faker.RuleFor(x => x.ExternalId, externalId);

        return faker;
    }

    public static ProductFaker WithStatusId(this ProductFaker faker, Guid statusId)
    {
        faker.RuleFor(x => x.StatusId, statusId);

        return faker;
    }

    public static ProductFaker WithStatusCategory(this ProductFaker faker, StatusCategory category)
    {
        faker.RuleFor(x => x.StatusCategory, category);

        return faker;
    }

    public static ProductFaker WithStatusAlias(this ProductFaker faker, ProductStatusAlias alias)
    {
        faker.RuleFor(x => x.StatusAlias, alias);

        return faker;
    }
}
