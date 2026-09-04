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
using Wayd.ProductManagement.Application.Releases.Commands;
using Wayd.ProductManagement.Application.Releases.Dtos;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.ProductManagement.Application.Tests.Sut.Releases.Commands;

/// <summary>
/// Importing a batch of releases. The handler's own work is resolving what each release announces,
/// setting those contents before the release can be announced, and refusing to announce one whose
/// contents have not shipped.
/// </summary>
public sealed class ImportReleasesCommandHandlerTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 4, 1, 9, 0, 0);

    private readonly FakeProductManagementDbContext _dbContext = new();
    private readonly Mock<IStatusResolver> _statusResolver = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    private readonly string _userId = Guid.CreateVersion7().ToString();
    private readonly WorkflowStatus _planned;
    private readonly WorkflowStatus _released;
    private readonly ProductType _productType;

    public ImportReleasesCommandHandlerTests()
    {
        ProductWorkflowOwners.Register();

        _currentUser.Setup(u => u.GetUserId()).Returns(_userId);
        _dateTimeProvider.SetupGet(d => d.Now).Returns(Now);

        var workflow = StatusWorkflow
            .CreateSystem("Release Lifecycle", null, ProductWorkflowOwners.Release.Key).Value;
        _planned = workflow.AddSystemStatus("Planned", null, StatusCategory.Proposed, StatusWorkflow.NoAlias);
        workflow.AddSystemStatus("Ready", null, StatusCategory.Active, (int)ProductStatusAlias.Ready);
        _released = workflow.AddSystemStatus("Released", null, StatusCategory.Done, (int)ProductStatusAlias.Released);
        workflow.PublishSystem();

        _statusResolver
            .Setup(r => r.Initial(ProductWorkflowOwners.Release.Key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(StatusRef.From(_planned)));
        _statusResolver
            .Setup(r => r.ForAlias(
                ProductWorkflowOwners.Release.Key, null, (int)ProductStatusAlias.Released, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(StatusRef.From(_released)));

        _productType = ProductType.Create("Service", null, true, 1);
        _dbContext.AddProductType(_productType);
    }

    private ImportReleasesCommandHandler CreateSut() =>
        new(_dbContext,
            _statusResolver.Object,
            _currentUser.Object,
            Mock.Of<ILogger<ImportReleasesCommandHandler>>(),
            _dateTimeProvider.Object);

    private static StatusRef ActiveStatus() =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), "Active", StatusCategory.Active,
            (int)ProductStatusAlias.Active);

    private Product SeedProduct(string name = "Wayd API")
    {
        var product = Product.Create(name, null, _productType.Id, null, null, ActiveStatus(), EventActor.System, Now);
        _dbContext.AddProduct(product);
        return product;
    }

    /// <param name="releasedDate">Null leaves the version unshipped, which is what blocks announcement.</param>
    private Version SeedVersion(Product product, string number, LocalDate? releasedDate)
    {
        var status = releasedDate is null ? StatusRef.From(_planned) : StatusRef.From(_released);

        var version = Version.Create(
            product.Id, number, null, null, null, true, status, product.Name, EventActor.System, Now).Value;

        if (releasedDate is not null)
        {
            version.MarkReleased(releasedDate.Value, StatusRef.From(_released), product.Name, EventActor.System, Now);
        }

        _dbContext.AddVersion(version);
        return version;
    }

    private ReleasePackage SeedPackage(
        string version, Product component, string componentVersion, LocalDate? releasedDate, Guid? versionId = null)
    {
        var package = ReleasePackage.Create(
            version, null, null,
            [(component.Id, versionId, componentVersion, ManifestEntryKind.Changed)],
            StatusRef.From(_planned), EventActor.System, Now).Value;

        if (releasedDate is not null)
        {
            package.MarkReleased(releasedDate.Value, StatusRef.From(_released), EventActor.System, Now);
        }

        _dbContext.AddReleasePackage(package);
        return package;
    }

    private static ImportReleaseDto Row(
        string version = "2026.07",
        string? name = null,
        string? productName = null,
        LocalDate? targetDate = null,
        LocalDate? releasedDate = null,
        string? notes = null,
        params ImportReleaseContentDto[] contents) =>
        new(version, name, productName, targetDate, releasedDate, null, notes, contents);

    private static ImportReleaseContentDto PackageContent(
        string releaseVersion = "2026.07", string packageVersion = "WAYD-2026.07") =>
        new(releaseVersion, ReleaseContentKind.Package, packageVersion, null, null);

    private static ImportReleaseContentDto VersionContent(
        string releaseVersion = "2026.07", string productName = "Wayd API", string versionNumber = "4.12.0") =>
        new(releaseVersion, ReleaseContentKind.Version, null, productName, versionNumber);

    [Fact]
    public async Task Handle_WhenRowsAreValid_CreatesEveryRelease()
    {
        // Arrange
        var sut = CreateSut();
        var command = new ImportReleasesCommand([Row(version: "2026.07"), Row(version: "2026.08")]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.Releases.Should().HaveCount(2);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenAReleaseHasNoContents_StillImportsIt()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportReleasesCommand([Row()]), TestContext.Current.CancellationToken);

        // Assert
        // An empty release is a legitimate state, not a draft: a repackaging or a pricing change is
        // announced with nothing deployed.
        result.IsSuccess.Should().BeTrue();
        _dbContext.Releases.Single().IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenAReleaseNamesNoProduct_LeavesItUnscoped()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportReleasesCommand([Row()]), TestContext.Current.CancellationToken);

        // Assert
        // A release spanning product lines has no single owner, which is why ProductId is nullable.
        result.IsSuccess.Should().BeTrue();
        _dbContext.Releases.Single().ProductId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenAReleaseNamesAProduct_ScopesItToThatProduct()
    {
        // Arrange
        var product = SeedProduct("Wayd");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportReleasesCommand([Row(productName: "Wayd")]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.Releases.Single().ProductId.Should().Be(product.Id);
    }

    [Fact]
    public async Task Handle_WhenAReleaseCarriesAPackage_RecordsIt()
    {
        // Arrange
        var product = SeedProduct();
        var package = SeedPackage("WAYD-2026.07", product, "4.12.0", releasedDate: null);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportReleasesCommand([Row(contents: PackageContent())]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.Releases.Single().Packages.Single().PackageId.Should().Be(package.Id);
    }

    [Fact]
    public async Task Handle_WhenAReleaseCarriesAVersionDirectly_RecordsIt()
    {
        // Arrange
        var product = SeedProduct();
        var version = SeedVersion(product, "4.12.0", releasedDate: null);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportReleasesCommand([Row(contents: VersionContent())]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.Releases.Single().Versions.Single().VersionId.Should().Be(version.Id);
    }

    [Fact]
    public async Task Handle_WhenAllContentsHaveShipped_AnnouncesTheRelease()
    {
        // Arrange
        var product = SeedProduct();
        SeedVersion(product, "4.12.0", releasedDate: new LocalDate(2026, 3, 20));
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportReleasesCommand(
                [Row(releasedDate: new LocalDate(2026, 4, 1), contents: VersionContent())]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var release = _dbContext.Releases.Single();
        release.StatusId.Should().Be(_released.Id);
        release.ReleasedDate.Should().Be(new LocalDate(2026, 4, 1));
    }

    [Fact]
    public async Task Handle_WhenAVersionItCarriesHasNotShipped_FailsTheBatch()
    {
        // Arrange
        var product = SeedProduct();
        SeedVersion(product, "4.12.0", releasedDate: null);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportReleasesCommand(
                [Row(releasedDate: new LocalDate(2026, 4, 1), contents: VersionContent())]),
            TestContext.Current.CancellationToken);

        // Assert
        // The one claim a release can make that its own contents contradict. The refusal names what
        // is holding it back rather than only that something is.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("has not shipped");
        result.Error.Should().Contain("4.12.0");
        _dbContext.Releases.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenAPackageItCarriesHasNotShipped_FailsTheBatch()
    {
        // Arrange
        var product = SeedProduct();
        SeedPackage("WAYD-2026.07", product, "4.12.0", releasedDate: null);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportReleasesCommand(
                [Row(releasedDate: new LocalDate(2026, 4, 1), contents: PackageContent())]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("has not shipped");
        result.Error.Should().Contain("WAYD-2026.07");
        _dbContext.Releases.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTheReleaseIsNotAnnounced_UnshippedContentsAreFine()
    {
        // Arrange
        var product = SeedProduct();
        SeedVersion(product, "4.12.0", releasedDate: null);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportReleasesCommand([Row(contents: VersionContent())]),
            TestContext.Current.CancellationToken);

        // Assert
        // Only announcement is constrained: a planned release may carry whatever it likes.
        result.IsSuccess.Should().BeTrue();
        _dbContext.Releases.Single().StatusId.Should().Be(_planned.Id);
    }

    [Fact]
    public async Task Handle_WhenAVersionIsCarriedBothDirectlyAndInAPackage_FailsTheBatch()
    {
        // Arrange
        var product = SeedProduct();
        var version = SeedVersion(product, "4.12.0", releasedDate: null);
        SeedPackage("WAYD-2026.07", product, "4.12.0", releasedDate: null, versionId: version.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportReleasesCommand([Row(contents: [PackageContent(), VersionContent()])]),
            TestContext.Current.CancellationToken);

        // Assert
        // Otherwise one shipment would be announced twice, and "what did 2026.07 contain" would have
        // two different answers.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot also be carried directly");
        _dbContext.Releases.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_AttributesEveryRowToTheImport()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportReleasesCommand([Row()]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var transition = _dbContext.Releases.Single().StatusTransitions.Single();
        transition.ActorKind.Should().Be(EventActorKind.Import);
        transition.ActorUserId.Should().Be(_userId);
    }

    [Fact]
    public async Task Handle_WhenARowCarriesNotes_AppliesThem()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportReleasesCommand([Row(notes: "Scoring now supports weighted criteria")]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.Releases.Single().Notes.Should().Be("Scoring now supports weighted criteria");
    }

    [Fact]
    public async Task Handle_WhenAReleaseVersionIsDuplicated_FailsTheBatch()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportReleasesCommand([Row(), Row()]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("more than once");
        _dbContext.Releases.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenAReleaseAlreadyExists_FailsTheBatch()
    {
        // Arrange
        var existing = Release.Create(
            null, "2026.07", null, null, null, StatusRef.From(_planned), EventActor.System, Now).Value;
        _dbContext.AddRelease(existing);

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportReleasesCommand([Row()]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exist");
        _dbContext.Releases.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_WhenAPackageCannotBeResolved_FailsTheBatch()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportReleasesCommand([Row(contents: PackageContent(packageVersion: "NOPE"))]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("NOPE");
        _dbContext.Releases.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenACarriedVersionCannotBeResolved_FailsTheBatch()
    {
        // Arrange
        SeedProduct();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportReleasesCommand([Row(contents: VersionContent(versionNumber: "9.9.9"))]),
            TestContext.Current.CancellationToken);

        // Assert
        // Unlike a package manifest, where an unmatched string is the carried-forward case, a
        // release's contents must resolve — announcing something that does not exist is a typo.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("9.9.9");
        _dbContext.Releases.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTwoProductsShareAVersionNumber_CarriesTheRightOne()
    {
        // Arrange
        var api = SeedProduct("Wayd API");
        var apiVersion = SeedVersion(api, "4.12.0", releasedDate: null);
        var client = SeedProduct("Wayd Client");
        var clientVersion = SeedVersion(client, "4.12.0", releasedDate: null);

        var sut = CreateSut();

        // Act
        // One release carrying both at the same number: whichever way a product-blind lookup
        // collapsed them, one of these assertions has to fail.
        var result = await sut.Handle(
            new ImportReleasesCommand(
            [
                Row(contents:
                [
                    VersionContent(productName: "Wayd API", versionNumber: "4.12.0"),
                    VersionContent(productName: "Wayd Client", versionNumber: "4.12.0"),
                ]),
            ]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.Releases.Single().Versions.Select(v => v.VersionId).Should()
            .BeEquivalentTo([apiVersion.Id, clientVersion.Id]);
    }

    [Fact]
    public async Task Handle_WhenReferencesDifferOnlyByCaseAndWhitespace_StillResolvesThem()
    {
        // Arrange
        var product = SeedProduct();
        var version = SeedVersion(product, "4.12.0", releasedDate: null);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new ImportReleasesCommand(
                [Row(contents: VersionContent(productName: " wayd api ", versionNumber: " 4.12.0 "))]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.Releases.Single().Versions.Single().VersionId.Should().Be(version.Id);
    }
}
