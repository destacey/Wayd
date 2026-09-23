using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Imports;
using Wayd.ProductManagement.Application.Products.Dtos;
using Wayd.ProductManagement.Application.Products.Imports;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;
using Wayd.ProductManagement.Domain.Tests.Data;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Imports;

/// <summary>
/// Importing product dependencies. The definition's own work is applying each product's rows in date order
/// and stopping a product at its first rejection; keeping the rest of that product out is the runner's, and
/// is proved against a real provider.
/// </summary>
public sealed class ProductDependencyImportDefinitionTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 4, 1, 9, 0, 0);
    private static readonly LocalDate Today = new(2026, 4, 1);

    private readonly FakeProductManagementDbContext _dbContext = new();
    private readonly ProductDependencyImportDefinition _definition;

    public ProductDependencyImportDefinitionTests()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(Guid.CreateVersion7().ToString());

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Now).Returns(Now);
        dateTimeProvider.SetupGet(d => d.Today).Returns(Today);

        _definition = new ProductDependencyImportDefinition(
            _dbContext, currentUser.Object, dateTimeProvider.Object, new ImportPayloadSerializer());
    }

    private Product SeedProduct(Guid? parentId = null)
    {
        var product = new ProductFaker().WithParentId(parentId).Generate();
        _dbContext.AddProduct(product);

        return product;
    }

    private static ImportProductDependencyDto Link(
        Product from,
        Product to,
        DependencyStrength strength = DependencyStrength.Hard,
        LocalDate? startsOn = null,
        LocalDate? endsOn = null) =>
        new(from.Id, to.Id, strength, null, null, startsOn, endsOn);

    private async Task<(ImportPassResult Result, ImportProcessRow[] Rows)> Run(params (string ImportId, ImportProductDependencyDto Data)[] rows)
    {
        ImportProcessRow[] processRows =
            [.. rows.Select((r, i) => ImportProcessRow.Create(r.ImportId, i + 1, _definition.SerializeRow(r.Data)))];

        var result = await _definition.ExecutePass(
            Guid.CreateVersion7(), 0, processRows, isFinalChunk: true, TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();

        return (result.Value, processRows);
    }

    private static ImportRowResult Outcome(ImportPassResult result, string importId) =>
        result.Rows.Single(r => r.ImportId == importId);

    [Fact]
    public void Definition_AppliesProductByProduct()
    {
        // Assert
        _definition.Atomicity.Should().Be(ImportAtomicity.PerGroup);
        _definition.GroupNoun.Should().Be("product");
        _definition.Passes.Should().ContainSingle();
    }

    [Fact]
    public void GroupKeysOf_IsTheProductThatHoldsTheLink()
    {
        // Arrange
        var storefront = SeedProduct();
        var identity = SeedProduct();

        // Act
        var key = _definition.GroupKeysOf([("r1", _definition.SerializeRow(Link(storefront, identity)))]).Single();

        // Assert
        key.Should().Be(storefront.Id.ToString());
    }

    [Fact]
    public async Task ExecutePass_AddsALinkAndReportsItsId()
    {
        // Arrange
        var storefront = SeedProduct();
        var identity = SeedProduct();

        // Act
        var (result, _) = await Run(("d1", Link(storefront, identity, startsOn: Today.PlusDays(-30))));

        // Assert
        var dependency = storefront.Dependencies.Should().ContainSingle().Subject;
        dependency.DependsOnProductId.Should().Be(identity.Id);
        dependency.Period.Start.Should().Be(Today.PlusDays(-30));
        Outcome(result, "d1").CreatedEntityId.Should().Be(dependency.Id);
    }

    [Fact]
    public async Task ExecutePass_StartsALinkTodayWhenTheRowNamesNoDay()
    {
        // Arrange
        var storefront = SeedProduct();
        var identity = SeedProduct();

        // Act
        await Run(("d1", Link(storefront, identity)));

        // Assert
        storefront.Dependencies.Single().Period.Start.Should().Be(Today);
    }

    [Fact]
    public async Task ExecutePass_EndsALinkThatCarriesALastDay()
    {
        // Arrange
        var storefront = SeedProduct();
        var legacyAuth = SeedProduct();

        // Act
        await Run(("d1", Link(storefront, legacyAuth, startsOn: Today.PlusDays(-300), endsOn: Today.PlusDays(-100))));

        // Assert
        var dependency = storefront.Dependencies.Single();
        dependency.IsOpen.Should().BeFalse();
        dependency.Period.End.Should().Be(Today.PlusDays(-100));
    }

    [Fact]
    public async Task ExecutePass_AppliesAProductsRowsInDateOrder()
    {
        // Arrange — a strength change with the later row listed first
        var checkout = SeedProduct();
        var payments = SeedProduct();

        // Act
        var (result, _) = await Run(
            ("now", Link(checkout, payments, DependencyStrength.Hard, startsOn: Today.PlusDays(-40))),
            ("was", Link(checkout, payments, DependencyStrength.Soft, startsOn: Today.PlusDays(-200), endsOn: Today.PlusDays(-41))));

        // Assert — in file order the open link would come first and the earlier one would overlap it
        result.Rows.Should().AllSatisfy(r => r.Failed.Should().BeFalse());
        checkout.Dependencies.OrderBy(d => d.Period.Start).Select(d => d.Strength)
            .Should().Equal(DependencyStrength.Soft, DependencyStrength.Hard);
    }

    [Fact]
    public async Task ExecutePass_StopsAProductAtItsFirstRejection()
    {
        // Arrange — the second row depends on the product's own child, which is composition
        var storefront = SeedProduct();
        var storefrontWeb = SeedProduct(parentId: storefront.Id);
        var identity = SeedProduct();
        var search = SeedProduct();

        // Act
        var (result, _) = await Run(
            ("s1", Link(storefront, identity, startsOn: Today.PlusDays(-30))),
            ("s2", Link(storefront, storefrontWeb, startsOn: Today.PlusDays(-20))),
            ("s3", Link(storefront, search, startsOn: Today.PlusDays(-10))));

        // Assert — the runner keeps s1 and s3 out on s2's account; the definition does not pile on errors
        Outcome(result, "s2").Failed.Should().BeTrue();
        Outcome(result, "s2").Error.Should().Contain("composition");
        Outcome(result, "s3").Failed.Should().BeFalse();
        Outcome(result, "s3").CreatedEntityId.Should().BeNull();
    }

    [Fact]
    public async Task ExecutePass_RefusesALinkToAProductAboveIt()
    {
        // Arrange — the ancestry comes from the whole tree, not from the rows in the file
        var platform = SeedProduct();
        var identity = SeedProduct(parentId: platform.Id);

        // Act
        var (result, _) = await Run(("d1", Link(identity, platform)));

        // Assert
        Outcome(result, "d1").Failed.Should().BeTrue();
        identity.Dependencies.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecutePass_RefusesAProductThatDoesNotExist()
    {
        // Arrange
        var identity = SeedProduct();
        var missing = new ProductFaker().Generate();

        // Act
        var (result, _) = await Run(("d1", Link(missing, identity)));

        // Assert
        Outcome(result, "d1").Error.Should().Be("The product was not found.");
    }

    [Fact]
    public async Task ExecutePass_RefusesADependedOnProductThatDoesNotExist()
    {
        // Arrange
        var storefront = SeedProduct();
        var missing = new ProductFaker().Generate();

        // Act
        var (result, _) = await Run(("d1", Link(storefront, missing)));

        // Assert
        Outcome(result, "d1").Error.Should().Be("The product depended on was not found.");
    }

    [Fact]
    public async Task ExecutePass_RefusesALinkThatStartsInTheFuture()
    {
        // Arrange
        var storefront = SeedProduct();
        var identity = SeedProduct();

        // Act
        var (result, _) = await Run(("d1", Link(storefront, identity, startsOn: Today.PlusDays(1))));

        // Assert
        Outcome(result, "d1").Error.Should().Contain("future");
    }

    [Fact]
    public async Task ExecutePass_LeavesOneProductsRejectionToThatProduct()
    {
        // Arrange
        var storefront = SeedProduct();
        var billing = SeedProduct();
        var identity = SeedProduct();

        // Act
        var (result, _) = await Run(
            ("s1", Link(storefront, identity, startsOn: Today.PlusDays(1))),
            ("b1", Link(billing, identity, startsOn: Today.PlusDays(-5))));

        // Assert
        Outcome(result, "s1").Failed.Should().BeTrue();
        Outcome(result, "b1").Failed.Should().BeFalse();
        billing.Dependencies.Should().ContainSingle();
    }
}
