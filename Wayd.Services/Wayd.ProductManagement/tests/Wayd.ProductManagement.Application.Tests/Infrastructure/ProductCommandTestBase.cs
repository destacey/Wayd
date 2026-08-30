using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Tests.Infrastructure;

/// <summary>
/// The scaffolding every Product command handler test needs: a fake context, a frozen clock, a known
/// actor, and seeding for the rows the handlers look up before touching the aggregate.
/// </summary>
public abstract class ProductCommandTestBase
{
    protected static readonly Instant Now = Instant.FromUtc(2026, 4, 1, 9, 0, 0);

    protected readonly FakeProductManagementDbContext DbContext = new();
    protected readonly Mock<ICurrentUser> CurrentUser = new();
    protected readonly Mock<IDateTimeProvider> DateTimeProvider = new();

    protected ProductCommandTestBase()
    {
        CurrentUser.Setup(u => u.GetUserId()).Returns(Guid.CreateVersion7().ToString());
        DateTimeProvider.SetupGet(d => d.Now).Returns(Now);
    }

    protected static ILogger<T> Logger<T>() => Mock.Of<ILogger<T>>();

    protected static StatusRef Status(
        string name = "Active",
        StatusCategory category = StatusCategory.Active,
        ProductStatusAlias alias = ProductStatusAlias.Active) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), name, category, (int)alias);

    protected ProductType SeedType(string name = "Application", bool isReleasable = true, bool isActive = true)
    {
        var productType = ProductType.Create(name, null, isReleasable, 1);

        if (!isActive)
        {
            productType.Deactivate();
        }

        DbContext.AddProductType(productType);

        return productType;
    }

    protected Product SeedProduct(
        string name = "Checkout",
        Guid? parentId = null,
        Guid? productTypeId = null,
        StatusRef? status = null,
        string? description = null,
        string? externalId = null)
    {
        var product = Product.Create(
            name,
            description,
            productTypeId ?? Guid.CreateVersion7(),
            parentId,
            externalId,
            status ?? Status(),
            EventActor.System,
            Now);

        // Created rows are already persisted as far as these tests are concerned, so the opening
        // transition and the deferred creation event are not part of what the command under test did.
        product.ClearDomainEvents();
        DbContext.AddProduct(product);

        return product;
    }

    /// <summary>
    /// A release against a product, for the handlers that refuse a change once one exists.
    /// </summary>
    protected Release SeedRelease(Guid productId, string version = "1.0")
    {
        var release = Release.Create(
            productId,
            version,
            null,
            null,
            null,
            isProductReleasable: true,
            Status("Planned", StatusCategory.Proposed, ProductStatusAlias.None),
            "Checkout",
            EventActor.System,
            Now).Value;

        DbContext.AddRelease(release);

        return release;
    }

    protected (ProductTagCategory Category, ProductTag Tag) SeedTag(
        string categoryName = "Platform",
        string tagName = "ios",
        bool allowsMany = true,
        bool categoryActive = true,
        bool tagActive = true)
    {
        var category = ProductTagCategory.Create(categoryName, null, allowsMany, 1);
        var tag = category.AddTag(tagName).Value;

        if (!tagActive)
        {
            tag.Deactivate();
        }

        if (!categoryActive)
        {
            category.Deactivate();
        }

        DbContext.AddProductTagCategory(category);
        DbContext.AddProductTag(tag);

        return (category, tag);
    }
}
