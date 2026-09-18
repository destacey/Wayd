using CSharpFunctionalExtensions;
using Mapster;
using Mapster.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Infrastructure.Persistence.Initialization;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Products.Queries;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Product dependencies through the real handlers and SQL Server.
/// </summary>
/// <remarks>
/// Each command runs in a context that has never seen the product, which is the only way a missing
/// <c>.Include(p =&gt; p.Dependencies)</c> shows: the handler fakes hold a product's links in memory whatever the
/// query asked for, so a handler that forgot to load them passes there and refuses or duplicates here.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class ProductDependencyPersistenceTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    private static readonly Instant Now = Instant.FromUtc(2026, 6, 1, 12, 0, 0);
    private static readonly LocalDate Today = new(2026, 6, 1);

    private static readonly Lazy<bool> Mappings = new(() =>
    {
        var assembly = typeof(ActivityLogDto).Assembly;
        TypeAdapterConfig.GlobalSettings.Scan(assembly);
        TypeAdapterConfig.GlobalSettings.ScanInheritedTypes(assembly);
        return true;
    });

    [Fact]
    public async Task Add_WhenAnOpenLinkIsAlreadySaved_IsRefusedByTheAggregate()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (identity, web) = await SeedPair();
        await Add(web.Id, identity.Id, DependencyStrength.Hard);

        // Act — a fresh context, so the aggregate knows of the first link only if the handler loads it
        Result<Guid> second;
        await using (var context = _fixture.CreateContext())
        {
            second = await AddHandler(context).Handle(
                new AddProductDependencyCommand(web.Id, identity.Id, DependencyStrength.Soft, null, null), ct);
        }

        // Assert — the domain's refusal, not the unique index turning the save into a generic error
        second.IsFailure.Should().BeTrue();
        second.Error.Should().StartWith("This product already depends on that product.");

        await using var verify = _fixture.CreateContext();
        (await verify.ProductDependencies.CountAsync(d => d.ProductId == web.Id, ct)).Should().Be(1);
    }

    [Fact]
    public async Task TwoOpenLinksOnTheSamePair_SavedFromRacingContexts_AreRejectedByTheDatabase()
    {
        // Arrange — each context checks the aggregate before the other has saved
        var ct = TestContext.Current.CancellationToken;
        var (identity, web) = await SeedPair();

        await using var first = _fixture.CreateContext();
        await using var second = _fixture.CreateContext();

        var firstProduct = await first.Products.Include(p => p.Dependencies).SingleAsync(p => p.Id == web.Id, ct);
        var secondProduct = await second.Products.Include(p => p.Dependencies).SingleAsync(p => p.Id == web.Id, ct);

        firstProduct.AddDependency(identity.Id, DependencyStrength.Hard, null, Today, [], [], Today, EventActor.System, Now).IsSuccess.Should().BeTrue();
        secondProduct.AddDependency(identity.Id, DependencyStrength.Soft, null, Today, [], [], Today, EventActor.System, Now).IsSuccess.Should().BeTrue();

        await first.SaveChangesAsync(ct);

        // Act
        var act = () => second.SaveChangesAsync(ct);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task ChangeStrength_EndsTheSavedLinkAndOpensAnother()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (identity, web) = await SeedPair();
        var originalId = await Add(web.Id, identity.Id, DependencyStrength.Soft, "Validates SSO tokens", Today.PlusDays(-30));
        var changedOn = Today.PlusDays(-5);

        // Act
        Result<Guid> result;
        await using (var context = _fixture.CreateContext())
        {
            result = await new ChangeProductDependencyStrengthCommandHandler(
                    context, CurrentUser(), Mock.Of<ILogger<ChangeProductDependencyStrengthCommandHandler>>(), Clock())
                .Handle(new ChangeProductDependencyStrengthCommand(web.Id, originalId, DependencyStrength.Hard, changedOn), ct);
        }

        // Assert
        result.IsSuccess.Should().BeTrue();

        await using var verify = _fixture.CreateContext();
        var links = await verify.ProductDependencies.AsNoTracking().Where(d => d.ProductId == web.Id).ToListAsync(ct);

        links.Should().HaveCount(2);
        links.Single(d => d.Id == originalId).Should().BeEquivalentTo(new { Strength = DependencyStrength.Soft, Period = new { Start = Today.PlusDays(-30), End = (LocalDate?)changedOn.PlusDays(-1) } });
        links.Single(d => d.Id == result.Value).Should().BeEquivalentTo(new
        {
            Strength = DependencyStrength.Hard,
            Description = "Validates SSO tokens",
            Period = new { Start = changedOn, End = (LocalDate?)null },
        });
    }

    [Fact]
    public async Task Remove_DeletesTheSavedLink()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (identity, web) = await SeedPair();
        var dependencyId = await Add(web.Id, identity.Id, DependencyStrength.Hard);

        // Act
        Result result;
        await using (var context = _fixture.CreateContext())
        {
            result = await new RemoveProductDependencyCommandHandler(
                    context, CurrentUser(), Mock.Of<ILogger<RemoveProductDependencyCommandHandler>>(), Clock())
                .Handle(new RemoveProductDependencyCommand(web.Id, dependencyId, "Recorded against the wrong product"), ct);
        }

        // Assert
        result.IsSuccess.Should().BeTrue();

        await using var verify = _fixture.CreateContext();
        (await verify.ProductDependencies.AnyAsync(d => d.Id == dependencyId, ct)).Should().BeFalse();
    }

    [Fact]
    public async Task Query_RollsUpADescendantsLinks()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var platform = await SeedProduct("Core Platform");
        var identity = await SeedProduct("Identity Service", platform.Id);
        var storefront = await SeedProduct("Storefront");
        var web = await SeedProduct("Storefront Web", storefront.Id);
        await Add(web.Id, identity.Id, DependencyStrength.Hard);

        // Act
        await using var context = _fixture.CreateContext();
        var handler = new GetProductDependenciesQueryHandler(context);
        var onStorefront = await handler.Handle(new GetProductDependenciesQuery(new IdOrKey(storefront.Key)), ct);
        var onPlatform = await handler.Handle(new GetProductDependenciesQuery(new IdOrKey(platform.Id)), ct);

        // Assert
        onStorefront!.DependsOn.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Product = new { web.Id, web.Name },
            DependsOnProduct = new { identity.Id, identity.Name },
            Strength = DependencyStrength.Hard,
        });
        onPlatform!.UsedBy.Should().ContainSingle().Which.Product.Id.Should().Be(web.Id);
    }

    [Fact]
    public async Task Reparent_BeneathAProductADescendantDependsOn_IsRefused()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var platform = await SeedProduct("Core Platform");
        var identity = await SeedProduct("Identity Service", platform.Id);
        var storefront = await SeedProduct("Storefront");
        var web = await SeedProduct("Storefront Web", storefront.Id);
        await Add(web.Id, platform.Id, DependencyStrength.Hard);

        // Act
        Result result;
        await using (var context = _fixture.CreateContext())
        {
            result = await new ReparentProductCommandHandler(
                    context, CurrentUser(), Mock.Of<ILogger<ReparentProductCommandHandler>>(), Clock())
                .Handle(new ReparentProductCommand(storefront.Id, identity.Id), ct);
        }

        // Assert
        result.IsFailure.Should().BeTrue();
        await using var verify = _fixture.CreateContext();
        (await verify.Products.SingleAsync(p => p.Id == storefront.Id, ct)).ParentId.Should().BeNull();
    }

    [Fact]
    public async Task Add_IsListedInTheActivityOfTheProductDependedOn()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (identity, web) = await SeedPair();

        // Act
        await Add(web.Id, identity.Id, DependencyStrength.Hard);

        // Assert
        _ = Mappings.Value;
        await using var context = _fixture.CreateContext();
        var activity = await new ActivityLogReader(context).Read(identity.Id, "Product", cancellationToken: ct);

        activity.Items.Should().ContainSingle(a => a.EventType == nameof(ProductDependencyAddedEvent))
            .Which.Should().BeEquivalentTo(new { IsRelated = true, AggregateId = web.Id });
    }

    private async Task<Guid> Add(Guid productId, Guid dependsOnProductId, DependencyStrength strength, string? description = null, LocalDate? startsOn = null)
    {
        await using var context = _fixture.CreateContext();

        var result = await AddHandler(context).Handle(
            new AddProductDependencyCommand(productId, dependsOnProductId, strength, description, startsOn),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

        return result.Value;
    }

    private static AddProductDependencyCommandHandler AddHandler(WaydDbContext context) =>
        new(context, CurrentUser(), Mock.Of<ILogger<AddProductDependencyCommandHandler>>(), Clock());

    private static ICurrentUser CurrentUser()
    {
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.GetUserId()).Returns(Guid.CreateVersion7().ToString());
        return user.Object;
    }

    private static IDateTimeProvider Clock()
    {
        var provider = new Mock<IDateTimeProvider>();
        provider.SetupGet(d => d.Now).Returns(Now);
        provider.SetupGet(d => d.Today).Returns(Today);
        return provider.Object;
    }

    private async Task<(Product DependedOn, Product Dependent)> SeedPair() =>
        (await SeedProduct($"Identity {Guid.CreateVersion7()}"), await SeedProduct($"Storefront Web {Guid.CreateVersion7()}"));

    private async Task<Product> SeedProduct(string name, Guid? parentId = null)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();

        ProductWorkflowOwners.Register();

        var workflow = await context.StatusWorkflows
            .Include(w => w.Statuses)
            .FirstOrDefaultAsync(w => w.OwnerType == ProductWorkflowOwners.Product.Key && w.IsSystem, ct);

        if (workflow is null)
        {
            await new ProductManagementWorkflowSeeder().Initialize(context, Clock(), ct);

            workflow = await context.StatusWorkflows
                .Include(w => w.Statuses)
                .FirstAsync(w => w.OwnerType == ProductWorkflowOwners.Product.Key && w.IsSystem, ct);
        }

        var productTypeId = await context.ProductTypes.Select(t => t.Id).FirstOrDefaultAsync(ct);
        if (productTypeId == Guid.Empty)
        {
            await new ProductTypeSeeder().Initialize(context, Clock(), ct);
            productTypeId = await context.ProductTypes.Select(t => t.Id).FirstAsync(ct);
        }

        var product = Product.Create(
            name, null, productTypeId, parentId, null,
            StatusRef.From(workflow.Statuses.OrderBy(s => s.Order).First()),
            EventActor.System, Now.Minus(Duration.FromDays(365)));

        context.Products.Add(product);
        await context.SaveChangesAsync(ct);

        return product;
    }
}
