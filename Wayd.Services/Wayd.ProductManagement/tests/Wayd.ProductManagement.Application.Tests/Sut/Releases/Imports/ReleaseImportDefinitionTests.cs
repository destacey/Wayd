using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Releases.Dtos;
using Wayd.ProductManagement.Application.Releases.Imports;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.ProductManagement.Application.Tests.Sut.Releases.Imports;

/// <summary>
/// Importing releases. The definition's own work is resolving what each release announces, setting those
/// contents before the release can be announced, and refusing to announce one whose contents have not
/// shipped.
/// </summary>
public sealed class ReleaseImportDefinitionTests
{
    private const int CreatePass = 0;

    private static readonly Instant Now = Instant.FromUtc(2026, 4, 1, 9, 0, 0);

    private readonly FakeProductManagementDbContext _dbContext = new();
    private readonly Mock<IStatusResolver> _statusResolver = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    private readonly string _userId = Guid.CreateVersion7().ToString();
    private readonly WorkflowStatus _planned;
    private readonly WorkflowStatus _released;
    private readonly ProductType _productType;
    private readonly ReleaseImportDefinition _definition;

    public ReleaseImportDefinitionTests()
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

        _definition = new ReleaseImportDefinition(
            _dbContext,
            _statusResolver.Object,
            _currentUser.Object,
            _dateTimeProvider.Object,
            Mock.Of<ILogger<ReleaseImportDefinition>>(),
            new ImportPayloadSerializer());
    }

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

    private ImportProcessRow[] Rows(params ImportReleaseDto[] releases) =>
        [.. releases.Select((r, i) => ImportProcessRow.Create($"r{i + 1}", i + 1, _definition.SerializeRow(r)))];

    private Task<Result<ImportPassResult>> Run(params ImportReleaseDto[] releases) =>
        _definition.ExecutePass(
            Guid.CreateVersion7(), CreatePass, Rows(releases), isFinalChunk: true, TestContext.Current.CancellationToken);

    private static ImportReleaseDto Row(
        string version = "2026.07",
        string? name = null,
        Guid? productId = null,
        LocalDate? targetDate = null,
        LocalDate? releasedDate = null,
        string? notes = null,
        params ImportReleaseContentDto[] contents) =>
        new(version, name, productId, targetDate, releasedDate, null, notes, contents);

    private static ImportReleaseContentDto PackageContent(Guid packageId) =>
        new(ReleaseContentKind.Package, packageId, null);

    private static ImportReleaseContentDto VersionContent(Guid versionId) =>
        new(ReleaseContentKind.Version, null, versionId);

    [Fact]
    public void Definition_IsAtomic()
    {
        // Arrange & Act & Assert
        _definition.Atomicity.Should().Be(ImportAtomicity.Atomic);
        _definition.Passes.Single().Name.Should().Be("CreateReleases");
    }

    [Fact]
    public async Task CreateReleases_CreatesEveryRowInTheFile()
    {
        // Arrange & Act
        var result = await Run(Row(version: "2026.07"), Row(version: "2026.08"));

        // Assert
        result.Value.Rows.Should().AllSatisfy(r => r.Failed.Should().BeFalse());
        _dbContext.Releases.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateReleases_ImportsAReleaseWithNoContents()
    {
        // Arrange & Act — an empty release is a legitimate state, not a draft: a repackaging or a pricing
        // change is announced with nothing deployed
        var result = await Run(Row());

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeFalse();

        var release = _dbContext.Releases.Single();
        release.IsEmpty.Should().BeTrue();
        release.ProductId.Should().BeNull();
        outcome.CreatedEntityId.Should().Be(release.Id);
    }

    [Fact]
    public async Task CreateReleases_ScopesAReleaseToTheProductItNames()
    {
        // Arrange
        var product = SeedProduct("Wayd");

        // Act
        var result = await Run(Row(productId: product.Id));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _dbContext.Releases.Single().ProductId.Should().Be(product.Id);
    }

    [Fact]
    public async Task CreateReleases_RecordsAPackageAReleaseCarries()
    {
        // Arrange
        var product = SeedProduct();
        var package = SeedPackage("WAYD-2026.07", product, "4.12.0", releasedDate: null);

        // Act
        var result = await Run(Row(contents: PackageContent(package.Id)));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _dbContext.Releases.Single().Packages.Single().PackageId.Should().Be(package.Id);
    }

    [Fact]
    public async Task CreateReleases_RecordsAVersionAReleaseCarriesDirectly()
    {
        // Arrange
        var product = SeedProduct();
        var version = SeedVersion(product, "4.12.0", releasedDate: null);

        // Act
        var result = await Run(Row(contents: VersionContent(version.Id)));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _dbContext.Releases.Single().Versions.Single().VersionId.Should().Be(version.Id);
    }

    [Fact]
    public async Task CreateReleases_AnnouncesAReleaseWhoseContentsHaveAllShipped()
    {
        // Arrange
        var product = SeedProduct();
        var version = SeedVersion(product, "4.12.0", releasedDate: new LocalDate(2026, 3, 20));

        // Act
        var result = await Run(
            Row(releasedDate: new LocalDate(2026, 4, 1), contents: VersionContent(version.Id)));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var release = _dbContext.Releases.Single();
        release.StatusId.Should().Be(_released.Id);
        release.ReleasedDate.Should().Be(new LocalDate(2026, 4, 1));
    }

    [Fact]
    public async Task CreateReleases_RejectsAnAnnouncedRowCarryingAVersionThatHasNotShipped()
    {
        // Arrange — the one claim a release can make that its own contents contradict. The refusal names
        // what is holding it back rather than only that something is.
        var product = SeedProduct();
        var version = SeedVersion(product, "4.12.0", releasedDate: null);

        // Act
        var result = await Run(
            Row(releasedDate: new LocalDate(2026, 4, 1), contents: VersionContent(version.Id)));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("has not shipped").And.Contain("4.12.0");
        _dbContext.Releases.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateReleases_RejectsAnAnnouncedRowCarryingAPackageThatHasNotShipped()
    {
        // Arrange
        var product = SeedProduct();
        var package = SeedPackage("WAYD-2026.07", product, "4.12.0", releasedDate: null);

        // Act
        var result = await Run(
            Row(releasedDate: new LocalDate(2026, 4, 1), contents: PackageContent(package.Id)));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("has not shipped").And.Contain("WAYD-2026.07");
        _dbContext.Releases.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateReleases_AllowsUnshippedContentsOnAReleaseThatIsNotAnnounced()
    {
        // Arrange — only announcement is constrained: a planned release may carry whatever it likes
        var product = SeedProduct();
        var version = SeedVersion(product, "4.12.0", releasedDate: null);

        // Act
        var result = await Run(Row(contents: VersionContent(version.Id)));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _dbContext.Releases.Single().StatusId.Should().Be(_planned.Id);
    }

    [Fact]
    public async Task CreateReleases_RejectsARowCarryingAVersionBothDirectlyAndInAPackage()
    {
        // Arrange — otherwise one shipment would be announced twice, and "what did 2026.07 contain" would
        // have two different answers
        var product = SeedProduct();
        var version = SeedVersion(product, "4.12.0", releasedDate: null);
        var package = SeedPackage("WAYD-2026.07", product, "4.12.0", releasedDate: null, versionId: version.Id);

        // Act
        var result = await Run(Row(contents: [PackageContent(package.Id), VersionContent(version.Id)]));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("cannot also be carried directly");
        _dbContext.Releases.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateReleases_AttributesEveryRowToTheImport()
    {
        // Arrange & Act
        await Run(Row());

        // Assert
        var transition = _dbContext.Releases.Single().StatusTransitions.Single();
        transition.ActorKind.Should().Be(EventActorKind.Import);
        transition.ActorUserId.Should().Be(_userId);
    }

    [Fact]
    public async Task CreateReleases_AppliesTheNotesARowCarries()
    {
        // Arrange & Act
        var result = await Run(Row(notes: "Scoring now supports weighted criteria"));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _dbContext.Releases.Single().Notes.Should().Be("Scoring now supports weighted criteria");
    }

    [Fact]
    public async Task CreateReleases_RejectsTheSecondRowRepeatingAVersion()
    {
        // Arrange & Act — the submission command rejects a repeat before the run starts, but the pass
        // holds the rule too: rows are applied before anything is saved
        var result = await Run(Row(), Row());

        // Assert
        result.Value.Rows.Single(r => r.ImportId == "r1").Failed.Should().BeFalse();
        result.Value.Rows.Single(r => r.ImportId == "r2").Failed.Should().BeTrue();
        _dbContext.Releases.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateReleases_RejectsARowWhoseVersionAlreadyExists()
    {
        // Arrange
        _dbContext.AddRelease(Release.Create(
            null, "2026.07", null, null, null, StatusRef.From(_planned), EventActor.System, Now).Value);

        // Act
        var result = await Run(Row());

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("2026.07");
        _dbContext.Releases.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateReleases_RejectsARowNamingAProductThatDoesNotExist()
    {
        // Arrange
        var missing = Guid.CreateVersion7();

        // Act
        var result = await Run(Row(productId: missing));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain(missing.ToString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateReleases_RejectsARowAnnouncingContentThatDoesNotExist(bool isPackage)
    {
        // Arrange — unlike a package manifest, where an unmatched reference is the carried-forward case, a
        // release's contents must resolve: announcing something that does not exist is a typo
        var missing = Guid.CreateVersion7();

        // Act
        var result = await Run(Row(contents: isPackage ? PackageContent(missing) : VersionContent(missing)));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain(missing.ToString());
        _dbContext.Releases.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateReleases_CarriesTheRightVersionWhenTwoProductsShareANumber()
    {
        // Arrange
        var api = SeedProduct("Wayd API");
        var apiVersion = SeedVersion(api, "4.12.0", releasedDate: null);
        var client = SeedProduct("Wayd Client");
        var clientVersion = SeedVersion(client, "4.12.0", releasedDate: null);

        // Act — one release carrying both at the same number: whichever way a product-blind lookup
        // collapsed them, one of these assertions would fail
        var result = await Run(
            Row(contents: [VersionContent(apiVersion.Id), VersionContent(clientVersion.Id)]));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _dbContext.Releases.Single().Versions.Select(v => v.VersionId).Should()
            .BeEquivalentTo([apiVersion.Id, clientVersion.Id]);
    }
}
