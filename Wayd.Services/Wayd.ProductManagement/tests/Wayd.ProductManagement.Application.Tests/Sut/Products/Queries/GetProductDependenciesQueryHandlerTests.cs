using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.Products.Queries;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Queries;

/// <summary>
/// A product's dependencies rolled up across its subtree: a zoomed-out view of Storefront and Core Platform has to show
/// Storefront depending on Core Platform, though every link is recorded between their services.
/// </summary>
public sealed class GetProductDependenciesQueryHandlerTests : ProductCommandTestBase
{
    private GetProductDependenciesQueryHandler CreateSut() => new(DbContext);

    [Fact]
    public async Task Handle_ListsWhatAProductDependsOnAndWhatDependsOnIt()
    {
        // Arrange
        var identity = SeedProduct("Identity Service");
        var web = SeedProduct("Storefront Web");
        var shifts = SeedProduct("Storefront Mobile");
        var link = SeedDependency(web, identity.Id, DependencyStrength.Hard, description: "Validates SSO tokens");
        SeedDependency(shifts, web.Id, DependencyStrength.Soft);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetProductDependenciesQuery(new IdOrKey(web.Id)), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();

        var dependsOn = result!.DependsOn.Should().ContainSingle().Subject;
        dependsOn.Id.Should().Be(link.Id);
        dependsOn.Product.Name.Should().Be("Storefront Web");
        dependsOn.DependsOnProduct.Name.Should().Be("Identity Service");
        dependsOn.Strength.Should().Be(DependencyStrength.Hard);
        dependsOn.Description.Should().Be("Validates SSO tokens");

        var usedBy = result.UsedBy.Should().ContainSingle().Subject;
        usedBy.Product.Name.Should().Be("Storefront Mobile");
        usedBy.DependsOnProduct.Name.Should().Be("Storefront Web");
    }

    [Fact]
    public async Task Handle_RollsUpLinksFromEverythingBeneathTheProduct_KeepingTheSpecificEnds()
    {
        // Arrange
        var platform = SeedProduct("Core Platform");
        var identity = SeedProduct("Identity Service", platform.Id);
        var storefront = SeedProduct("Storefront");
        var web = SeedProduct("Storefront Web", storefront.Id);
        var mobile = SeedProduct("Storefront Mobile", storefront.Id);
        var shifts = SeedProduct("Storefront Mobile", mobile.Id);
        SeedDependency(web, identity.Id);
        SeedDependency(shifts, identity.Id);
        var sut = CreateSut();

        // Act
        var onStorefront = await sut.Handle(new GetProductDependenciesQuery(new IdOrKey(storefront.Id)), TestContext.Current.CancellationToken);
        var onPlatform = await sut.Handle(new GetProductDependenciesQuery(new IdOrKey(platform.Id)), TestContext.Current.CancellationToken);

        // Assert — a grandchild's link reaches the top, and each row still names the services involved
        onStorefront!.DependsOn.Select(d => (d.Product.Name, d.DependsOnProduct.Name))
            .Should().BeEquivalentTo([("Storefront Web", "Identity Service"), ("Storefront Mobile", "Identity Service")]);
        onStorefront.UsedBy.Should().BeEmpty();

        onPlatform!.UsedBy.Select(d => (d.Product.Name, d.DependsOnProduct.Name))
            .Should().BeEquivalentTo([("Storefront Web", "Identity Service"), ("Storefront Mobile", "Identity Service")]);
        onPlatform.DependsOn.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_CarriesWhereEachEndSitsInTheCatalog()
    {
        // Arrange — a link two levels down on each side.
        var platform = SeedProduct("Core Platform");
        var services = SeedProduct("Platform Services", platform.Id);
        var identity = SeedProduct("Identity Service", services.Id);
        var storefront = SeedProduct("Storefront");
        var apps = SeedProduct("Storefront Apps", storefront.Id);
        var web = SeedProduct("Storefront Web", apps.Id);
        SeedDependency(web, identity.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new GetProductDependenciesQuery(new IdOrKey(storefront.Id)), TestContext.Current.CancellationToken);

        // Assert
        // Full chains from the root, so a reader can place a rolled-up link where it sits rather than
        // listing every descendant that happened to hold one at the same level.
        var row = result!.DependsOn.Should().ContainSingle().Subject;
        row.ProductPath.Select(p => p.Name).Should().Equal("Storefront", "Storefront Apps");
        row.DependsOnProductPath.Select(p => p.Name).Should().Equal("Core Platform", "Platform Services");
    }

    [Fact]
    public async Task Handle_CarriesAnEmptyPathForARootProduct()
    {
        // Arrange
        var identity = SeedProduct("Identity Service");
        var web = SeedProduct("Storefront Web");
        SeedDependency(web, identity.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new GetProductDependenciesQuery(new IdOrKey(web.Id)), TestContext.Current.CancellationToken);

        // Assert
        var row = result!.DependsOn.Should().ContainSingle().Subject;
        row.ProductPath.Should().BeEmpty();
        row.DependsOnProductPath.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_LeavesOutLinksBetweenProductsInsideTheSubtree()
    {
        // Arrange
        var platform = SeedProduct("Core Platform");
        var identity = SeedProduct("Identity Service", platform.Id);
        var gateway = SeedProduct("Gateway Service", platform.Id);
        SeedDependency(gateway, identity.Id);
        var sut = CreateSut();

        // Act
        var onPlatform = await sut.Handle(new GetProductDependenciesQuery(new IdOrKey(platform.Id)), TestContext.Current.CancellationToken);
        var onGateway = await sut.Handle(new GetProductDependenciesQuery(new IdOrKey(gateway.Id)), TestContext.Current.CancellationToken);

        // Assert — from outside Core Platform it is Core Platform depending on itself; from the gateway it is an ordinary link
        onPlatform!.DependsOn.Should().BeEmpty();
        onPlatform.UsedBy.Should().BeEmpty();
        onGateway!.DependsOn.Should().ContainSingle().Which.DependsOnProduct.Name.Should().Be("Identity Service");
    }

    [Fact]
    public async Task Handle_LeavesOutEndedLinksUnlessAsked()
    {
        // Arrange
        var identity = SeedProduct("Identity Service");
        var notifications = SeedProduct("Notification Service");
        var web = SeedProduct("Storefront Web");
        SeedDependency(web, identity.Id);
        SeedDependency(web, notifications.Id, startsOn: Today.PlusDays(-60), endsOn: Today.PlusDays(-10));
        var sut = CreateSut();

        // Act
        var openOnly = await sut.Handle(new GetProductDependenciesQuery(new IdOrKey(web.Id)), TestContext.Current.CancellationToken);
        var withEnded = await sut.Handle(new GetProductDependenciesQuery(new IdOrKey(web.Id), includeEnded: true), TestContext.Current.CancellationToken);

        // Assert
        openOnly!.DependsOn.Should().ContainSingle().Which.DependsOnProduct.Name.Should().Be("Identity Service");

        // Open links first, whatever the names
        withEnded!.DependsOn.Select(d => d.DependsOnProduct.Name).Should().Equal("Identity Service", "Notification Service");
        withEnded.DependsOn.Last().EndsOn.Should().Be(Today.PlusDays(-10));
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenTheProductDoesNotExist()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetProductDependenciesQuery(new IdOrKey(Guid.CreateVersion7())), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }
}
