using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Products.Dtos;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Commands;

/// <summary>
/// Importing a batch of products. The handler's own work is ordering the batch so parents precede
/// children, resolving types and statuses by name, and failing the whole batch on any reference it
/// cannot honour.
/// </summary>
public sealed class ImportProductsCommandHandlerTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 4, 1, 9, 0, 0);

    private readonly FakeProductManagementDbContext _dbContext = new();
    private readonly Mock<IStatusResolver> _statusResolver = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    private readonly string _userId = Guid.CreateVersion7().ToString();
    private readonly StatusWorkflow _workflow;
    private readonly WorkflowStatus _concept;

    public ImportProductsCommandHandlerTests()
    {
        ProductWorkflowOwners.Register();

        _currentUser.Setup(u => u.GetUserId()).Returns(_userId);
        _dateTimeProvider.SetupGet(d => d.Now).Returns(Now);

        _workflow = StatusWorkflow.CreateSystem("Product Lifecycle", null, ProductWorkflowOwners.Product.Key).Value;
        _concept = _workflow.AddSystemStatus("Concept", null, StatusCategory.Proposed, StatusWorkflow.NoAlias);
        _workflow.AddSystemStatus("Active", null, StatusCategory.Active, (int)ProductStatusAlias.Active);
        _workflow.AddSystemStatus("Retired", null, StatusCategory.Done, (int)ProductStatusAlias.Retired);
        _workflow.PublishSystem();

        _statusResolver
            .Setup(r => r.ForScope(ProductWorkflowOwners.Product.Key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(_workflow));

        _statusResolver
            .Setup(r => r.Initial(ProductWorkflowOwners.Product.Key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(StatusRef.From(_concept)));
    }

    private ImportProductsCommandHandler CreateSut() =>
        new(_dbContext,
            _statusResolver.Object,
            _currentUser.Object,
            Mock.Of<ILogger<ImportProductsCommandHandler>>(),
            _dateTimeProvider.Object);

    private ProductType SeedType(string name = "Application", bool isReleasable = true, bool isActive = true)
    {
        var productType = ProductType.Create(name, null, isReleasable, 1);

        if (!isActive)
        {
            productType.Deactivate();
        }

        _dbContext.AddProductType(productType);

        return productType;
    }

    private (ProductTagCategory Category, ProductTag Tag) SeedTag(
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

        _dbContext.AddProductTagCategory(category);
        _dbContext.AddProductTag(tag);

        return (category, tag);
    }

    private static ImportProductDto Row(
        string number,
        string name,
        string productTypeName = "Application",
        string? parentNumber = null,
        string? status = null,
        string? description = null,
        string? externalId = null,
        params (string Category, string Tag)[] tags) =>
        new(number, name, description, productTypeName, parentNumber, externalId, status,
            [.. tags.Select(t => new ProductTagReference(t.Category, t.Tag))]);

    [Fact]
    public async Task Handle_WhenRowsAreValid_CreatesEveryProduct()
    {
        // Arrange
        SeedType();
        var sut = CreateSut();
        var command = new ImportProductsCommand([Row("1", "Wayd"), Row("2", "Wayd API")]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.Products.Should().HaveCount(2);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenAChildPrecedesItsParent_StillLinksThemCorrectly()
    {
        // Arrange
        SeedType();
        var sut = CreateSut();

        // The child is listed first, which is the case creating in file order would break.
        var command = new ImportProductsCommand(
        [
            Row("2", "Wayd API", parentNumber: "1"),
            Row("1", "Wayd"),
        ]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var parent = _dbContext.Products.Single(p => p.Name == "Wayd");
        var child = _dbContext.Products.Single(p => p.Name == "Wayd API");
        child.ParentId.Should().Be(parent.Id);
        parent.ParentId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenTheTreeIsSeveralLevelsDeep_LinksEveryLevel()
    {
        // Arrange
        SeedType();
        var sut = CreateSut();

        // Deepest first, so the ordering has to walk the whole chain.
        var command = new ImportProductsCommand(
        [
            Row("3", "Checkout", parentNumber: "2"),
            Row("2", "Wayd API", parentNumber: "1"),
            Row("1", "Wayd"),
        ]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var root = _dbContext.Products.Single(p => p.Name == "Wayd");
        var middle = _dbContext.Products.Single(p => p.Name == "Wayd API");
        var leaf = _dbContext.Products.Single(p => p.Name == "Checkout");
        middle.ParentId.Should().Be(root.Id);
        leaf.ParentId.Should().Be(middle.Id);
    }

    [Fact]
    public async Task Handle_WhenTwoProductsShareANameUnderDifferentParents_ImportsBoth()
    {
        // Arrange
        SeedType();
        var sut = CreateSut();

        // The case that makes a file-local number necessary: names alone could not tell these apart.
        var command = new ImportProductsCommand(
        [
            Row("1", "Platform A"),
            Row("2", "Platform B"),
            Row("3", "API", parentNumber: "1"),
            Row("4", "API", parentNumber: "2"),
        ]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var apis = _dbContext.Products.Where(p => p.Name == "API").ToList();
        apis.Should().HaveCount(2);
        apis.Select(a => a.ParentId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Handle_WhenARowNamesAStatus_AppliesIt()
    {
        // Arrange
        SeedType();
        var sut = CreateSut();
        var command = new ImportProductsCommand([Row("1", "Wayd", status: "Retired")]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.Products.Single().StatusCategory.Should().Be(StatusCategory.Done);
    }

    [Fact]
    public async Task Handle_WhenARowOmitsTheStatus_UsesTheWorkflowsInitialStatus()
    {
        // Arrange
        SeedType();
        var sut = CreateSut();
        var command = new ImportProductsCommand([Row("1", "Wayd")]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.Products.Single().StatusCategory.Should().Be(StatusCategory.Proposed);
    }

    [Fact]
    public async Task Handle_AttributesEveryRowToTheImport()
    {
        // Arrange
        SeedType();
        var sut = CreateSut();
        var command = new ImportProductsCommand([Row("1", "Wayd")]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var product = _dbContext.Products.Single();
        product.StatusTransitions.Should().ContainSingle()
            .Which.ActorKind.Should().Be(EventActorKind.Import);
    }

    [Fact]
    public async Task Handle_WhenTheImportRecordsWhoRanIt_KeepsTheOriginatingUser()
    {
        // Arrange
        SeedType();
        var sut = CreateSut();
        var command = new ImportProductsCommand([Row("1", "Wayd")]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.Products.Single().StatusTransitions.Single().ActorUserId.Should().Be(_userId);
    }

    [Fact]
    public async Task Handle_WhenARowNumberIsDuplicated_FailsTheBatch()
    {
        // Arrange
        SeedType();
        var sut = CreateSut();
        var command = new ImportProductsCommand([Row("1", "Wayd"), Row("1", "Wayd API")]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("more than once");
        _dbContext.Products.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenAParentNumberMatchesNoRow_FailsTheBatch()
    {
        // Arrange
        SeedType();
        var sut = CreateSut();
        var command = new ImportProductsCommand([Row("2", "Wayd API", parentNumber: "99")]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("'99'");
        _dbContext.Products.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenAnExistingProductIsNamedAsAParent_FailsTheBatch()
    {
        // Arrange
        var productType = SeedType();
        var existing = Product.Create(
            "Already Here", null, productType.Id, null, null,
            StatusRef.From(_concept), EventActor.System, Now);
        _dbContext.AddProduct(existing);

        var sut = CreateSut();

        // A parent must be a row in this file: the import stands a catalog up rather than grafting
        // onto one, so an existing product's name is not a reference it can resolve.
        var command = new ImportProductsCommand([Row("1", "Wayd API", parentNumber: "Already Here")]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("same file");
        _dbContext.Products.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_WhenParentReferencesFormACycle_FailsTheBatch()
    {
        // Arrange
        SeedType();
        var sut = CreateSut();
        var command = new ImportProductsCommand(
        [
            Row("1", "One", parentNumber: "2"),
            Row("2", "Two", parentNumber: "1"),
        ]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("circular");
        _dbContext.Products.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenAProductTypeCannotBeResolved_FailsTheBatch()
    {
        // Arrange
        SeedType();
        var sut = CreateSut();
        var command = new ImportProductsCommand([Row("1", "Wayd", productTypeName: "Nonexistent")]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Nonexistent");
        _dbContext.Products.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenAProductTypeIsInactive_FailsTheBatch()
    {
        // Arrange
        SeedType("Application", isActive: false);
        var sut = CreateSut();
        var command = new ImportProductsCommand([Row("1", "Wayd")]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("inactive");
        _dbContext.Products.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenAStatusDoesNotBelongToTheWorkflow_FailsTheBatch()
    {
        // Arrange
        SeedType();
        var sut = CreateSut();
        var command = new ImportProductsCommand([Row("1", "Wayd", status: "Shipped")]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Shipped");
        _dbContext.Products.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenReferencesDifferOnlyByCaseAndWhitespace_StillResolvesThem()
    {
        // Arrange
        SeedType();
        var sut = CreateSut();
        var command = new ImportProductsCommand(
        [
            Row(" 1 ", "Wayd", productTypeName: " application "),
            Row("2", "Wayd API", productTypeName: "APPLICATION", parentNumber: "1"),
        ]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var parent = _dbContext.Products.Single(p => p.Name == "Wayd");
        _dbContext.Products.Single(p => p.Name == "Wayd API").ParentId.Should().Be(parent.Id);
    }

    [Fact]
    public async Task Handle_WhenARowNamesTags_AppliesThem()
    {
        // Arrange
        SeedType();
        var (category, ios) = SeedTag();
        var android = category.AddTag("android").Value;
        _dbContext.AddProductTag(android);
        var sut = CreateSut();
        var command = new ImportProductsCommand(
            [Row("1", "Wayd Client", tags: [("Platform", "ios"), ("Platform", "android")])]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var product = _dbContext.Products.Single();
        product.Tags.Select(t => t.TagId).Should().BeEquivalentTo([ios.Id, android.Id]);
    }

    [Fact]
    public async Task Handle_WhenTheSameTagNameExistsOnTwoAxes_ResolvesEachToItsOwnAxis()
    {
        // Arrange
        SeedType();
        var (_, supportGold) = SeedTag("Support", "gold");
        var (_, tierGold) = SeedTag("Tier", "gold");
        var sut = CreateSut();

        // The reason a tag reference carries its axis: 'gold' alone identifies neither of these.
        var command = new ImportProductsCommand(
        [
            Row("1", "Alpha", tags: [("Support", "gold")]),
            Row("2", "Beta", tags: [("Tier", "gold")]),
        ]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.Products.Single(p => p.Name == "Alpha").Tags.Single().TagId.Should().Be(supportGold.Id);
        _dbContext.Products.Single(p => p.Name == "Beta").Tags.Single().TagId.Should().Be(tierGold.Id);
    }

    [Fact]
    public async Task Handle_WhenTheAxisAllowsOneAndARowNamesTwo_KeepsTheLast()
    {
        // Arrange
        SeedType();
        var (category, _) = SeedTag(allowsMany: false);
        var android = category.AddTag("android").Value;
        _dbContext.AddProductTag(android);
        var sut = CreateSut();
        var command = new ImportProductsCommand(
            [Row("1", "Wayd Client", tags: [("Platform", "ios"), ("Platform", "android")])]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        // The aggregate treats a second tag on a single-value axis as a correction rather than an
        // error, so the row is imported carrying the last one it named.
        result.IsSuccess.Should().BeTrue();
        _dbContext.Products.Single().Tags.Should().ContainSingle()
            .Which.TagId.Should().Be(android.Id);
    }

    [Fact]
    public async Task Handle_WhenATagCategoryCannotBeResolved_FailsTheBatch()
    {
        // Arrange
        SeedType();
        SeedTag();
        var sut = CreateSut();
        var command = new ImportProductsCommand([Row("1", "Wayd", tags: [("Nonexistent", "ios")])]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Nonexistent");
        _dbContext.Products.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenATagCannotBeResolvedOnItsAxis_FailsTheBatch()
    {
        // Arrange
        SeedType();
        SeedTag();
        var sut = CreateSut();
        var command = new ImportProductsCommand([Row("1", "Wayd", tags: [("Platform", "windows")])]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("windows");
        _dbContext.Products.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenATagIsInactive_FailsTheBatch()
    {
        // Arrange
        SeedType();
        SeedTag(tagActive: false);
        var sut = CreateSut();
        var command = new ImportProductsCommand([Row("1", "Wayd", tags: [("Platform", "ios")])]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("inactive");
        _dbContext.Products.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenATagCategoryIsInactive_FailsTheBatch()
    {
        // Arrange
        SeedType();
        SeedTag(categoryActive: false);
        var sut = CreateSut();
        var command = new ImportProductsCommand([Row("1", "Wayd", tags: [("Platform", "ios")])]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("inactive");
        _dbContext.Products.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenARowNamesNoTags_LeavesTheProductUntagged()
    {
        // Arrange
        SeedType();
        SeedTag();
        var sut = CreateSut();
        var command = new ImportProductsCommand([Row("1", "Wayd")]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.Products.Single().Tags.Should().BeEmpty();
    }
}
