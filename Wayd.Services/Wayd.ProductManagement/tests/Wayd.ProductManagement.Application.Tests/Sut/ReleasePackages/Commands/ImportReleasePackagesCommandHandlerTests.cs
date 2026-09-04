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
using Wayd.ProductManagement.Application.ReleasePackages.Commands;
using Wayd.ProductManagement.Application.ReleasePackages.Dtos;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.ProductManagement.Application.Tests.Sut.ReleasePackages.Commands;

/// <summary>
/// Importing a batch of release packages. The handler's own work is resolving each manifest line's
/// product, linking the line to a version record where one matches, and marking the package released
/// where the row says it shipped.
/// </summary>
public sealed class ImportReleasePackagesCommandHandlerTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 4, 1, 9, 0, 0);

    private readonly FakeProductManagementDbContext _dbContext = new();
    private readonly Mock<IStatusResolver> _statusResolver = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    private readonly string _userId = Guid.CreateVersion7().ToString();
    private readonly WorkflowStatus _planned;
    private readonly WorkflowStatus _released;

    public ImportReleasePackagesCommandHandlerTests()
    {
        ProductWorkflowOwners.Register();

        _currentUser.Setup(u => u.GetUserId()).Returns(_userId);
        _dateTimeProvider.SetupGet(d => d.Now).Returns(Now);

        var workflow = StatusWorkflow
            .CreateSystem("Package Lifecycle", null, ProductWorkflowOwners.ReleasePackage.Key).Value;
        _planned = workflow.AddSystemStatus("Planned", null, StatusCategory.Proposed, StatusWorkflow.NoAlias);
        workflow.AddSystemStatus("Ready", null, StatusCategory.Active, (int)ProductStatusAlias.Ready);
        _released = workflow.AddSystemStatus("Released", null, StatusCategory.Done, (int)ProductStatusAlias.Released);
        workflow.PublishSystem();

        _statusResolver
            .Setup(r => r.Initial(ProductWorkflowOwners.ReleasePackage.Key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(StatusRef.From(_planned)));
        _statusResolver
            .Setup(r => r.ForAlias(
                ProductWorkflowOwners.ReleasePackage.Key, null, (int)ProductStatusAlias.Released, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(StatusRef.From(_released)));
    }

    private ImportReleasePackagesCommandHandler CreateSut() =>
        new(_dbContext,
            _statusResolver.Object,
            _currentUser.Object,
            Mock.Of<ILogger<ImportReleasePackagesCommandHandler>>(),
            _dateTimeProvider.Object);

    private Product SeedProduct(string name = "Wayd API")
    {
        var productType = ProductType.Create("Service", null, true, 1);
        _dbContext.AddProductType(productType);

        var status = new StatusRef(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Active", StatusCategory.Active,
            (int)ProductStatusAlias.Active);

        var product = Product.Create(name, null, productType.Id, null, null, status, EventActor.System, Now);
        _dbContext.AddProduct(product);

        return product;
    }

    private Version SeedVersion(Product product, string number)
    {
        var status = new StatusRef(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Released", StatusCategory.Done,
            (int)ProductStatusAlias.Released);

        var version = Version.Create(
            product.Id, number, null, null, null, true, status, product.Name, EventActor.System, Now).Value;
        _dbContext.AddVersion(version);

        return version;
    }

    private static ImportReleasePackageComponentDto Component(
        string packageVersion = "WAYD-2026.09",
        string productName = "Wayd API",
        string versionNumber = "4.10.0",
        ManifestEntryKind kind = ManifestEntryKind.Changed) =>
        new(packageVersion, productName, versionNumber, kind);

    private static ImportReleasePackageDto Row(
        string version = "WAYD-2026.09",
        string? name = null,
        LocalDate? targetDate = null,
        LocalDate? releasedDate = null,
        params ImportReleasePackageComponentDto[] components) =>
        new(version, name, targetDate, releasedDate,
            components.Length > 0 ? components : [Component(version)]);

    [Fact]
    public async Task Handle_WhenRowsAreValid_CreatesEveryPackage()
    {
        // Arrange
        SeedProduct();
        var sut = CreateSut();
        var command = new ImportReleasePackagesCommand(
        [
            Row(version: "WAYD-2026.09", components: Component("WAYD-2026.09")),
            Row(version: "WAYD-2026.10", components: Component("WAYD-2026.10")),
        ]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.ReleasePackages.Should().HaveCount(2);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenARowHasNoReleasedDate_LeavesThePackageAssembled()
    {
        // Arrange
        SeedProduct();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportReleasePackagesCommand([Row()]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var package = _dbContext.ReleasePackages.Single();
        package.StatusId.Should().Be(_planned.Id);
        package.ReleasedDate.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenARowHasAReleasedDate_MarksThePackageReleased()
    {
        // Arrange
        SeedProduct();
        var sut = CreateSut();
        var releasedDate = new LocalDate(2026, 4, 5);

        // Act
        var result = await sut.Handle(
            new ImportReleasePackagesCommand([Row(releasedDate: releasedDate)]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var package = _dbContext.ReleasePackages.Single();
        package.StatusId.Should().Be(_released.Id);
        package.ReleasedDate.Should().Be(releasedDate);
    }

    [Fact]
    public async Task Handle_WhenAComponentMatchesAVersionRecord_LinksTheManifestLineToIt()
    {
        // Arrange
        var product = SeedProduct();
        var version = SeedVersion(product, "4.10.0");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportReleasePackagesCommand([Row(components: Component(versionNumber: "4.10.0"))]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var component = _dbContext.ReleasePackages.Single().Components.Single();
        component.VersionId.Should().Be(version.Id);
        component.Version.Should().Be("4.10.0");
    }

    [Fact]
    public async Task Handle_WhenAComponentMatchesNoVersionRecord_KeepsTheStringWithoutALink()
    {
        // Arrange
        SeedProduct();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportReleasePackagesCommand(
                [Row(components: Component(versionNumber: "3.9.0", kind: ManifestEntryKind.CarriedForward))]),
            TestContext.Current.CancellationToken);

        // Assert
        // A carried-forward component was already running and was never cut here, so recording the
        // string without a link is the point rather than a failure.
        result.IsSuccess.Should().BeTrue();
        var component = _dbContext.ReleasePackages.Single().Components.Single();
        component.VersionId.Should().BeNull();
        component.Version.Should().Be("3.9.0");
        component.Kind.Should().Be(ManifestEntryKind.CarriedForward);
    }

    [Fact]
    public async Task Handle_WhenTwoProductsShareAVersionNumber_LinksEachToItsOwn()
    {
        // Arrange
        var api = SeedProduct("Wayd API");
        var apiVersion = SeedVersion(api, "4.10.0");

        var productType = _dbContext.ProductTypes.First();
        var status = new StatusRef(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Active", StatusCategory.Active,
            (int)ProductStatusAlias.Active);
        var client = Product.Create("Wayd Client", null, productType.Id, null, null, status, EventActor.System, Now);
        _dbContext.AddProduct(client);

        // Both products carry a version numbered 4.10.0 — different artifacts that happen to share a
        // number, which is exactly what a product-blind lookup would confuse.
        var clientVersion = SeedVersion(client, "4.10.0");

        var sut = CreateSut();

        // Act
        // One package carrying both products at the same number: whichever way a product-blind
        // lookup collapsed them, one of these two assertions has to fail.
        var result = await sut.Handle(
            new ImportReleasePackagesCommand(
            [
                Row(components:
                [
                    Component(productName: "Wayd API", versionNumber: "4.10.0"),
                    Component(productName: "Wayd Client", versionNumber: "4.10.0"),
                ]),
            ]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var components = _dbContext.ReleasePackages.Single().Components.ToList();
        components.Single(c => c.ProductId == api.Id).VersionId.Should().Be(apiVersion.Id);
        components.Single(c => c.ProductId == client.Id).VersionId.Should().Be(clientVersion.Id);
    }

    [Fact]
    public async Task Handle_WhenAPackageCarriesSeveralComponents_RecordsThemAll()
    {
        // Arrange
        SeedProduct("Wayd API");
        var productType = _dbContext.ProductTypes.First();
        var status = new StatusRef(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Active", StatusCategory.Active,
            (int)ProductStatusAlias.Active);
        _dbContext.AddProduct(
            Product.Create("Wayd Client", null, productType.Id, null, null, status, EventActor.System, Now));

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportReleasePackagesCommand(
            [
                Row(components:
                [
                    Component(productName: "Wayd API", versionNumber: "4.10.0"),
                    Component(productName: "Wayd Client", versionNumber: "2026.05", kind: ManifestEntryKind.CarriedForward),
                ]),
            ]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var package = _dbContext.ReleasePackages.Single();
        package.Components.Should().HaveCount(2);
        package.ChangedComponents.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_AttributesEveryRowToTheImport()
    {
        // Arrange
        SeedProduct();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportReleasePackagesCommand([Row()]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var transition = _dbContext.ReleasePackages.Single().StatusTransitions.Single();
        transition.ActorKind.Should().Be(EventActorKind.Import);
        transition.ActorUserId.Should().Be(_userId);
    }

    [Fact]
    public async Task Handle_WhenAPackageVersionIsDuplicated_FailsTheBatch()
    {
        // Arrange
        SeedProduct();
        var sut = CreateSut();
        var command = new ImportReleasePackagesCommand([Row(), Row()]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("more than once");
        _dbContext.ReleasePackages.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenAPackageAlreadyExists_FailsTheBatch()
    {
        // Arrange
        var product = SeedProduct();
        var existing = ReleasePackage.Create(
            "WAYD-2026.09", null, null,
            [(product.Id, null, "4.10.0", ManifestEntryKind.Changed)],
            StatusRef.From(_planned), EventActor.System, Now).Value;
        _dbContext.AddReleasePackage(existing);

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportReleasePackagesCommand([Row()]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exist");
        _dbContext.ReleasePackages.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_WhenAComponentProductCannotBeResolved_FailsTheBatch()
    {
        // Arrange
        SeedProduct();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportReleasePackagesCommand([Row(components: Component(productName: "Nonexistent"))]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Nonexistent");
        _dbContext.ReleasePackages.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenAComponentProductNameIsAmbiguous_FailsTheBatch()
    {
        // Arrange
        var productType = ProductType.Create("Service", null, true, 1);
        _dbContext.AddProductType(productType);
        var status = new StatusRef(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Active", StatusCategory.Active,
            (int)ProductStatusAlias.Active);
        _dbContext.AddProduct(Product.Create("Shared", null, productType.Id, null, null, status, EventActor.System, Now));
        _dbContext.AddProduct(Product.Create("Shared", null, productType.Id, null, null, status, EventActor.System, Now));

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportReleasePackagesCommand([Row(components: Component(productName: "Shared"))]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("more than one product");
        _dbContext.ReleasePackages.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenAComponentAppearsTwiceInOneManifest_FailsTheBatch()
    {
        // Arrange
        SeedProduct();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportReleasePackagesCommand(
            [
                Row(components:
                [
                    Component(versionNumber: "4.10.0"),
                    Component(versionNumber: "4.11.0"),
                ]),
            ]),
            TestContext.Current.CancellationToken);

        // Assert
        // The aggregate refuses it: one component cannot ship at two versions in one box.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("only once");
        _dbContext.ReleasePackages.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenReferencesDifferOnlyByCaseAndWhitespace_StillResolvesThem()
    {
        // Arrange
        var product = SeedProduct();
        var version = SeedVersion(product, "4.10.0");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportReleasePackagesCommand(
                [Row(components: Component(productName: " wayd api ", versionNumber: " 4.10.0 "))]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.ReleasePackages.Single().Components.Single().VersionId.Should().Be(version.Id);
    }
}
