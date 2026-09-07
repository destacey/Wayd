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
using Wayd.ProductManagement.Application.ReleasePackages.Dtos;
using Wayd.ProductManagement.Application.ReleasePackages.Imports;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.ProductManagement.Application.Tests.Sut.ReleasePackages.Imports;

/// <summary>
/// Importing release packages. The definition's own work is linking each manifest line to a version record
/// where one matches, leaving the string alone where none does, and marking the package released where the
/// row says it shipped.
/// </summary>
public sealed class ReleasePackageImportDefinitionTests
{
    private const int CreatePass = 0;

    private static readonly Instant Now = Instant.FromUtc(2026, 4, 1, 9, 0, 0);

    private readonly FakeProductManagementDbContext _dbContext = new();
    private readonly Mock<IStatusResolver> _statusResolver = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    private readonly string _userId = Guid.CreateVersion7().ToString();
    private readonly WorkflowStatus _assembled;
    private readonly WorkflowStatus _released;
    private readonly ProductType _productType;
    private readonly ReleasePackageImportDefinition _definition;

    public ReleasePackageImportDefinitionTests()
    {
        ProductWorkflowOwners.Register();

        _currentUser.Setup(u => u.GetUserId()).Returns(_userId);
        _dateTimeProvider.SetupGet(d => d.Now).Returns(Now);

        var workflow = StatusWorkflow
            .CreateSystem("Release Package Lifecycle", null, ProductWorkflowOwners.ReleasePackage.Key).Value;
        _assembled = workflow.AddSystemStatus("Assembled", null, StatusCategory.Proposed, StatusWorkflow.NoAlias);
        _released = workflow.AddSystemStatus("Released", null, StatusCategory.Done, (int)ProductStatusAlias.Released);
        workflow.PublishSystem();

        _statusResolver
            .Setup(r => r.Initial(ProductWorkflowOwners.ReleasePackage.Key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(StatusRef.From(_assembled)));
        _statusResolver
            .Setup(r => r.ForAlias(
                ProductWorkflowOwners.ReleasePackage.Key, null, (int)ProductStatusAlias.Released, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(StatusRef.From(_released)));

        _productType = ProductType.Create("Service", null, true, 1);
        _dbContext.AddProductType(_productType);

        _definition = new ReleasePackageImportDefinition(
            _dbContext,
            _statusResolver.Object,
            _currentUser.Object,
            _dateTimeProvider.Object,
            Mock.Of<ILogger<ReleasePackageImportDefinition>>(),
            new ImportPayloadSerializer());
    }

    private Product SeedProduct(string name = "Wayd API")
    {
        var status = new StatusRef(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Active", StatusCategory.Active,
            (int)ProductStatusAlias.Active);

        var product = Product.Create(name, null, _productType.Id, null, null, status, EventActor.System, Now);
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

    private ImportProcessRow[] Rows(params ImportReleasePackageDto[] packages) =>
        [.. packages.Select((p, i) => ImportProcessRow.Create($"r{i + 1}", i + 1, _definition.SerializeRow(p)))];

    private Task<Result<ImportPassResult>> Run(params ImportReleasePackageDto[] packages) =>
        _definition.ExecutePass(
            Guid.CreateVersion7(), CreatePass, Rows(packages), isFinalChunk: true, TestContext.Current.CancellationToken);

    private static ImportReleasePackageComponentDto Component(
        Guid productId,
        string versionNumber = "4.10.0",
        ManifestEntryKind kind = ManifestEntryKind.Changed) =>
        new(productId, versionNumber, kind);

    private static ImportReleasePackageDto Row(
        Guid productId,
        string version = "WAYD-2026.09",
        LocalDate? releasedDate = null,
        params ImportReleasePackageComponentDto[] components) =>
        new(version, null, null, releasedDate,
            components.Length > 0 ? components : [Component(productId)]);

    [Fact]
    public void Definition_IsAtomic()
    {
        // Arrange & Act & Assert
        _definition.Atomicity.Should().Be(ImportAtomicity.Atomic);
        _definition.Passes.Single().Name.Should().Be("CreatePackages");
    }

    [Fact]
    public async Task CreatePackages_CreatesEveryRowInTheFile()
    {
        // Arrange
        var product = SeedProduct();

        // Act
        var result = await Run(Row(product.Id, "WAYD-2026.09"), Row(product.Id, "WAYD-2026.10"));

        // Assert
        result.Value.Rows.Should().AllSatisfy(r => r.Failed.Should().BeFalse());
        _dbContext.ReleasePackages.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreatePackages_LeavesARowWithNoReleasedDateAssembled()
    {
        // Arrange
        var product = SeedProduct();

        // Act
        var result = await Run(Row(product.Id));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeFalse();

        var package = _dbContext.ReleasePackages.Single();
        package.StatusId.Should().Be(_assembled.Id);
        package.ReleasedDate.Should().BeNull();
        outcome.CreatedEntityId.Should().Be(package.Id);
    }

    [Fact]
    public async Task CreatePackages_MarksARowWithAReleasedDateReleased()
    {
        // Arrange
        var product = SeedProduct();
        var releasedDate = new LocalDate(2026, 4, 5);

        // Act
        var result = await Run(Row(product.Id, releasedDate: releasedDate));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var package = _dbContext.ReleasePackages.Single();
        package.StatusId.Should().Be(_released.Id);
        package.ReleasedDate.Should().Be(releasedDate);
    }

    [Fact]
    public async Task CreatePackages_LinksAComponentThatMatchesAVersionRecord()
    {
        // Arrange
        var product = SeedProduct();
        var version = SeedVersion(product, "4.10.0");

        // Act
        var result = await Run(Row(product.Id, components: Component(product.Id, "4.10.0")));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var component = _dbContext.ReleasePackages.Single().Components.Single();
        component.VersionId.Should().Be(version.Id);
        component.Version.Should().Be("4.10.0");
    }

    [Fact]
    public async Task CreatePackages_KeepsTheStringWhenAComponentMatchesNoVersionRecord()
    {
        // Arrange — a carried-forward component was already running and was never cut here, so recording
        // the string without a link is the point rather than a failure
        var product = SeedProduct();

        // Act
        var result = await Run(
            Row(product.Id, components: Component(product.Id, "3.9.0", ManifestEntryKind.CarriedForward)));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var component = _dbContext.ReleasePackages.Single().Components.Single();
        component.VersionId.Should().BeNull();
        component.Version.Should().Be("3.9.0");
        component.Kind.Should().Be(ManifestEntryKind.CarriedForward);
    }

    [Fact]
    public async Task CreatePackages_LinksEachComponentWhenTwoProductsShareAVersionNumber()
    {
        // Arrange — both products carry a version numbered 4.10.0: different artifacts that happen to
        // share a number, which is exactly what a product-blind lookup would confuse
        var api = SeedProduct("Wayd API");
        var apiVersion = SeedVersion(api, "4.10.0");
        var client = SeedProduct("Wayd Client");
        var clientVersion = SeedVersion(client, "4.10.0");

        // Act
        var result = await Run(Row(api.Id, components:
        [
            Component(api.Id, "4.10.0"),
            Component(client.Id, "4.10.0"),
        ]));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var components = _dbContext.ReleasePackages.Single().Components.ToList();
        components.Single(c => c.ProductId == api.Id).VersionId.Should().Be(apiVersion.Id);
        components.Single(c => c.ProductId == client.Id).VersionId.Should().Be(clientVersion.Id);
    }

    [Fact]
    public async Task CreatePackages_RecordsEveryComponentAPackageCarries()
    {
        // Arrange
        var api = SeedProduct("Wayd API");
        var client = SeedProduct("Wayd Client");

        // Act
        var result = await Run(Row(api.Id, components:
        [
            Component(api.Id, "4.10.0"),
            Component(client.Id, "2026.05", ManifestEntryKind.CarriedForward),
        ]));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var package = _dbContext.ReleasePackages.Single();
        package.Components.Should().HaveCount(2);
        package.ChangedComponents.Should().ContainSingle();
    }

    [Fact]
    public async Task CreatePackages_AttributesEveryRowToTheImport()
    {
        // Arrange
        var product = SeedProduct();

        // Act
        await Run(Row(product.Id));

        // Assert
        var transition = _dbContext.ReleasePackages.Single().StatusTransitions.Single();
        transition.ActorKind.Should().Be(EventActorKind.Import);
        transition.ActorUserId.Should().Be(_userId);
    }

    [Fact]
    public async Task CreatePackages_RejectsTheSecondRowRepeatingAVersion()
    {
        // Arrange & Act — the submission command rejects a repeat before the run starts, but the pass
        // holds the rule too: rows are applied before anything is saved
        var product = SeedProduct();

        // Act
        var result = await Run(Row(product.Id), Row(product.Id));

        // Assert
        result.Value.Rows.Single(r => r.ImportId == "r1").Failed.Should().BeFalse();
        result.Value.Rows.Single(r => r.ImportId == "r2").Failed.Should().BeTrue();
        _dbContext.ReleasePackages.Should().ContainSingle();
    }

    [Fact]
    public async Task CreatePackages_RejectsARowWhoseVersionAlreadyExists()
    {
        // Arrange
        var product = SeedProduct();
        _dbContext.AddReleasePackage(ReleasePackage.Create(
            "WAYD-2026.09", null, null,
            [(product.Id, null, "4.10.0", ManifestEntryKind.Changed)],
            StatusRef.From(_assembled), EventActor.System, Now).Value);

        // Act
        var result = await Run(Row(product.Id));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("WAYD-2026.09");
        _dbContext.ReleasePackages.Should().ContainSingle();
    }

    [Fact]
    public async Task CreatePackages_RejectsARowNamingAComponentProductThatDoesNotExist()
    {
        // Arrange
        SeedProduct();
        var missing = Guid.CreateVersion7();

        // Act
        var result = await Run(Row(missing, components: Component(missing)));

        // Assert — named per row, so the person knows which line to fix
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain(missing.ToString());
        _dbContext.ReleasePackages.Should().BeEmpty();
    }

    [Fact]
    public async Task CreatePackages_RejectsARowWhoseComponentAppearsTwice()
    {
        // Arrange — the aggregate refuses it: one component cannot ship at two versions in one box
        var product = SeedProduct();

        // Act
        var result = await Run(Row(product.Id, components:
        [
            Component(product.Id, "4.10.0"),
            Component(product.Id, "4.11.0"),
        ]));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("only once");
        _dbContext.ReleasePackages.Should().BeEmpty();
    }

    [Fact]
    public async Task CreatePackages_TrimsAComponentVersionSurroundedByWhitespace()
    {
        // Arrange
        var product = SeedProduct();
        var version = SeedVersion(product, "4.10.0");

        // Act
        var result = await Run(Row(product.Id, components: Component(product.Id, " 4.10.0 ")));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var component = _dbContext.ReleasePackages.Single().Components.Single();
        component.Version.Should().Be("4.10.0");
        component.VersionId.Should().Be(version.Id);
    }
}
