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
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Application.Versions.Commands;
using Wayd.ProductManagement.Application.Versions.Dtos;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Tests.Sut.Versions.Commands;

/// <summary>
/// Importing a batch of versions. The handler's own work is resolving products by name, refusing the
/// ones whose type cannot carry a version, and walking each row to the state its dates describe.
/// </summary>
public sealed class ImportVersionsCommandHandlerTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 4, 1, 9, 0, 0);

    private readonly FakeProductManagementDbContext _dbContext = new();
    private readonly Mock<IStatusResolver> _statusResolver = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    private readonly string _userId = Guid.CreateVersion7().ToString();
    private readonly WorkflowStatus _planned;
    private readonly WorkflowStatus _ready;
    private readonly WorkflowStatus _released;

    public ImportVersionsCommandHandlerTests()
    {
        ProductWorkflowOwners.Register();

        _currentUser.Setup(u => u.GetUserId()).Returns(_userId);
        _dateTimeProvider.SetupGet(d => d.Now).Returns(Now);

        var workflow = StatusWorkflow
            .CreateSystem("Version Lifecycle", null, ProductWorkflowOwners.Version.Key).Value;
        _planned = workflow.AddSystemStatus("Planned", null, StatusCategory.Proposed, StatusWorkflow.NoAlias);
        _ready = workflow.AddSystemStatus("Ready", null, StatusCategory.Active, (int)ProductStatusAlias.Ready);
        _released = workflow.AddSystemStatus("Released", null, StatusCategory.Done, (int)ProductStatusAlias.Released);
        workflow.PublishSystem();

        _statusResolver
            .Setup(r => r.Initial(ProductWorkflowOwners.Version.Key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(StatusRef.From(_planned)));
        _statusResolver
            .Setup(r => r.ForAlias(
                ProductWorkflowOwners.Version.Key, null, (int)ProductStatusAlias.Ready, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(StatusRef.From(_ready)));
        _statusResolver
            .Setup(r => r.ForAlias(
                ProductWorkflowOwners.Version.Key, null, (int)ProductStatusAlias.Released, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(StatusRef.From(_released)));
    }

    private ImportVersionsCommandHandler CreateSut() =>
        new(_dbContext,
            _statusResolver.Object,
            _currentUser.Object,
            Mock.Of<ILogger<ImportVersionsCommandHandler>>(),
            _dateTimeProvider.Object);

    /// <summary>Seeds a product and the type that decides whether it can carry versions.</summary>
    private Product SeedProduct(string name = "Wayd API", bool isReleasable = true)
    {
        var productType = ProductType.Create(
            isReleasable ? "Service" : "Product Line", null, isReleasable, 1);
        _dbContext.AddProductType(productType);

        var status = new StatusRef(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Active", StatusCategory.Active,
            (int)ProductStatusAlias.Active);

        var product = Product.Create(name, null, productType.Id, null, null, status, EventActor.System, Now);
        _dbContext.AddProduct(product);

        return product;
    }

    private static ImportVersionDto Row(
        string productName = "Wayd API",
        string number = "1.0.0",
        string? name = null,
        LocalDate? targetDate = null,
        LocalDate? cutDate = null,
        LocalDate? releasedDate = null,
        long? sequence = null,
        string? notes = null) =>
        new(productName, number, name, targetDate, cutDate, releasedDate, sequence, notes);

    [Fact]
    public async Task Handle_WhenRowsAreValid_CreatesEveryVersion()
    {
        // Arrange
        SeedProduct();
        var sut = CreateSut();
        var command = new ImportVersionsCommand([Row(number: "1.0.0"), Row(number: "1.1.0")]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.Versions.Should().HaveCount(2);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenARowHasNoDates_LeavesTheVersionPlanned()
    {
        // Arrange
        SeedProduct();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportVersionsCommand([Row()]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var version = _dbContext.Versions.Single();
        version.StatusId.Should().Be(_planned.Id);
        version.CutDate.Should().BeNull();
        version.ReleasedDate.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenARowHasACutDate_MakesTheVersionReady()
    {
        // Arrange
        SeedProduct();
        var sut = CreateSut();
        var cutDate = new LocalDate(2026, 3, 1);

        // Act
        var result = await sut.Handle(
            new ImportVersionsCommand([Row(cutDate: cutDate)]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var version = _dbContext.Versions.Single();
        version.StatusId.Should().Be(_ready.Id);
        version.CutDate.Should().Be(cutDate);
        version.ReleasedDate.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenARowHasBothDates_MakesTheVersionReleased()
    {
        // Arrange
        SeedProduct();
        var sut = CreateSut();
        var cutDate = new LocalDate(2026, 3, 1);
        var releasedDate = new LocalDate(2026, 3, 15);

        // Act
        var result = await sut.Handle(
            new ImportVersionsCommand([Row(cutDate: cutDate, releasedDate: releasedDate)]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var version = _dbContext.Versions.Single();
        version.StatusId.Should().Be(_released.Id);
        version.CutDate.Should().Be(cutDate);
        version.ReleasedDate.Should().Be(releasedDate);
    }

    [Fact]
    public async Task Handle_WhenARowIsReleasedWithoutACutDate_StillReleasesIt()
    {
        // Arrange
        SeedProduct();
        var sut = CreateSut();
        var releasedDate = new LocalDate(2026, 3, 15);

        // Act
        var result = await sut.Handle(
            new ImportVersionsCommand([Row(releasedDate: releasedDate)]),
            TestContext.Current.CancellationToken);

        // Assert
        // Cutting is not a prerequisite for shipping, which is what makes a historical backfill
        // possible — a version recorded after the fact rarely says when scope froze.
        result.IsSuccess.Should().BeTrue();
        var version = _dbContext.Versions.Single();
        version.StatusId.Should().Be(_released.Id);
        version.CutDate.Should().BeNull();
        version.ReleasedDate.Should().Be(releasedDate);
    }

    [Fact]
    public async Task Handle_WhenARowIsReleased_RecordsEveryTransitionItPassedThrough()
    {
        // Arrange
        SeedProduct();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportVersionsCommand([Row(cutDate: new LocalDate(2026, 3, 1), releasedDate: new LocalDate(2026, 3, 15))]),
            TestContext.Current.CancellationToken);

        // Assert
        // Replaying the real transitions rather than assigning a final status is what gives an
        // imported version the same history a hand-entered one would have.
        result.IsSuccess.Should().BeTrue();
        var transitions = _dbContext.Versions.Single().StatusTransitions.ToList();
        transitions.Should().HaveCount(3);
        transitions.Select(t => t.ToStatusId).Should()
            .ContainInOrder(_planned.Id, _ready.Id, _released.Id);
    }

    [Fact]
    public async Task Handle_AttributesEveryRowToTheImport()
    {
        // Arrange
        SeedProduct();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportVersionsCommand([Row()]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var transition = _dbContext.Versions.Single().StatusTransitions.Single();
        transition.ActorKind.Should().Be(EventActorKind.Import);
        transition.ActorUserId.Should().Be(_userId);
    }

    [Fact]
    public async Task Handle_WhenARowCarriesNotes_AppliesThem()
    {
        // Arrange
        SeedProduct();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportVersionsCommand([Row(notes: "Bumped Npgsql to 9.0.2")]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.Versions.Single().Notes.Should().Be("Bumped Npgsql to 9.0.2");
    }

    [Fact]
    public async Task Handle_WhenTwoProductsShareAVersionNumber_ImportsBoth()
    {
        // Arrange
        var productType = ProductType.Create("Service", null, true, 1);
        _dbContext.AddProductType(productType);
        var status = new StatusRef(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Active", StatusCategory.Active,
            (int)ProductStatusAlias.Active);
        _dbContext.AddProduct(Product.Create("Alpha", null, productType.Id, null, null, status, EventActor.System, Now));
        _dbContext.AddProduct(Product.Create("Beta", null, productType.Id, null, null, status, EventActor.System, Now));

        var sut = CreateSut();

        // A version number is only unique within its product, which is why the key carries both.
        var command = new ImportVersionsCommand(
        [
            Row(productName: "Alpha", number: "1.0.0"),
            Row(productName: "Beta", number: "1.0.0"),
        ]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.Versions.Should().HaveCount(2);
        _dbContext.Versions.Select(v => v.ProductId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Handle_WhenTheSameVersionAppearsTwiceForOneProduct_FailsTheBatch()
    {
        // Arrange
        SeedProduct();
        var sut = CreateSut();
        var command = new ImportVersionsCommand([Row(number: "1.0.0"), Row(number: "1.0.0")]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("more than once");
        _dbContext.Versions.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenAVersionAlreadyExists_FailsTheBatch()
    {
        // Arrange
        var product = SeedProduct();
        var existing = Wayd.ProductManagement.Domain.Models.Version.Create(
            product.Id, "1.0.0", null, null, null, true, StatusRef.From(_planned), product.Name,
            EventActor.System, Now).Value;
        _dbContext.AddVersion(existing);

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportVersionsCommand([Row(number: "1.0.0")]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exist");
        _dbContext.Versions.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_WhenAProductCannotBeResolved_FailsTheBatch()
    {
        // Arrange
        SeedProduct();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportVersionsCommand([Row(productName: "Nonexistent")]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Nonexistent");
        _dbContext.Versions.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenAProductNameIsAmbiguous_FailsTheBatch()
    {
        // Arrange
        var productType = ProductType.Create("Service", null, true, 1);
        _dbContext.AddProductType(productType);
        var status = new StatusRef(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Active", StatusCategory.Active,
            (int)ProductStatusAlias.Active);

        // Product names carry no unique index, so two really can share one.
        _dbContext.AddProduct(Product.Create("Shared", null, productType.Id, null, null, status, EventActor.System, Now));
        _dbContext.AddProduct(Product.Create("Shared", null, productType.Id, null, null, status, EventActor.System, Now));

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportVersionsCommand([Row(productName: "Shared")]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("more than one product");
        _dbContext.Versions.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTheProductTypeIsNotReleasable_FailsTheBatch()
    {
        // Arrange
        SeedProduct("Wayd", isReleasable: false);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportVersionsCommand([Row(productName: "Wayd")]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not releasable");
        _dbContext.Versions.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenReferencesDifferOnlyByCaseAndWhitespace_StillResolvesThem()
    {
        // Arrange
        SeedProduct();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportVersionsCommand([Row(productName: " wayd api ", number: " 1.0.0 ")]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.Versions.Single().Number.Should().Be("1.0.0");
    }
}
