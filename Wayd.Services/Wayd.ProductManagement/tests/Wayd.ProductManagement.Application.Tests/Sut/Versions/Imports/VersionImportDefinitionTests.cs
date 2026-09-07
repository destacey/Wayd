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
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Application.Versions.Dtos;
using Wayd.ProductManagement.Application.Versions.Imports;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

// The delivery artifact record, not System.Version.
using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.ProductManagement.Application.Tests.Sut.Versions.Imports;

/// <summary>
/// Importing versions. The definition's own work is resolving products, refusing the ones whose type
/// cannot carry a version, and walking each row to the state its dates describe.
/// </summary>
public sealed class VersionImportDefinitionTests
{
    private const int CreatePass = 0;

    private static readonly Instant Now = Instant.FromUtc(2026, 4, 1, 9, 0, 0);

    private readonly FakeProductManagementDbContext _dbContext = new();
    private readonly Mock<IStatusResolver> _statusResolver = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    private readonly string _userId = Guid.CreateVersion7().ToString();
    private readonly WorkflowStatus _planned;
    private readonly WorkflowStatus _ready;
    private readonly WorkflowStatus _released;
    private readonly VersionImportDefinition _definition;

    public VersionImportDefinitionTests()
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

        _definition = new VersionImportDefinition(
            _dbContext,
            _statusResolver.Object,
            _currentUser.Object,
            _dateTimeProvider.Object,
            Mock.Of<ILogger<VersionImportDefinition>>(),
            new ImportPayloadSerializer());
    }

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

    private ImportProcessRow[] Rows(params ImportVersionDto[] versions) =>
        [.. versions.Select((v, i) => ImportProcessRow.Create($"r{i + 1}", i + 1, _definition.SerializeRow(v)))];

    private Task<Result<ImportPassResult>> Run(params ImportVersionDto[] versions) =>
        _definition.ExecutePass(
            Guid.CreateVersion7(), CreatePass, Rows(versions), isFinalChunk: true, TestContext.Current.CancellationToken);

    private static ImportVersionDto Row(
        Guid productId,
        string number = "1.0.0",
        string? name = null,
        LocalDate? targetDate = null,
        LocalDate? cutDate = null,
        LocalDate? releasedDate = null,
        long? sequence = null,
        string? notes = null) =>
        new(productId, number, name, targetDate, cutDate, releasedDate, sequence, notes);

    [Fact]
    public void Definition_IsAtomic()
    {
        // Arrange & Act & Assert
        _definition.Atomicity.Should().Be(ImportAtomicity.Atomic);
        _definition.Passes.Single().Name.Should().Be("CreateVersions");
    }

    [Fact]
    public async Task CreateVersions_CreatesEveryRowInTheFile()
    {
        // Arrange
        var product = SeedProduct();

        // Act
        var result = await Run(Row(product.Id, "1.0.0"), Row(product.Id, "1.1.0"));

        // Assert
        result.Value.Rows.Should().AllSatisfy(r => r.Failed.Should().BeFalse());
        _dbContext.Versions.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateVersions_LeavesARowWithNoDatesPlanned()
    {
        // Arrange
        var product = SeedProduct();

        // Act
        var result = await Run(Row(product.Id));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeFalse();

        var version = _dbContext.Versions.Single();
        version.StatusId.Should().Be(_planned.Id);
        version.CutDate.Should().BeNull();
        version.ReleasedDate.Should().BeNull();
        outcome.CreatedEntityId.Should().Be(version.Id);
    }

    [Fact]
    public async Task CreateVersions_MakesARowWithACutDateReady()
    {
        // Arrange
        var product = SeedProduct();
        var cutDate = new LocalDate(2026, 3, 1);

        // Act
        var result = await Run(Row(product.Id, cutDate: cutDate));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var version = _dbContext.Versions.Single();
        version.StatusId.Should().Be(_ready.Id);
        version.CutDate.Should().Be(cutDate);
        version.ReleasedDate.Should().BeNull();
    }

    [Fact]
    public async Task CreateVersions_MakesARowWithBothDatesReleased()
    {
        // Arrange
        var product = SeedProduct();
        var cutDate = new LocalDate(2026, 3, 1);
        var releasedDate = new LocalDate(2026, 3, 15);

        // Act
        var result = await Run(Row(product.Id, cutDate: cutDate, releasedDate: releasedDate));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var version = _dbContext.Versions.Single();
        version.StatusId.Should().Be(_released.Id);
        version.CutDate.Should().Be(cutDate);
        version.ReleasedDate.Should().Be(releasedDate);
    }

    [Fact]
    public async Task CreateVersions_ReleasesARowThatHasNoCutDate()
    {
        // Arrange — cutting is not a prerequisite for shipping, which is what makes a historical backfill
        // possible: a version recorded after the fact rarely says when scope froze
        var product = SeedProduct();
        var releasedDate = new LocalDate(2026, 3, 15);

        // Act
        var result = await Run(Row(product.Id, releasedDate: releasedDate));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var version = _dbContext.Versions.Single();
        version.StatusId.Should().Be(_released.Id);
        version.CutDate.Should().BeNull();
        version.ReleasedDate.Should().Be(releasedDate);
    }

    [Fact]
    public async Task CreateVersions_RecordsEveryTransitionAReleasedRowPassedThrough()
    {
        // Arrange — replaying the real transitions rather than assigning a final status is what gives an
        // imported version the same history a hand-entered one would have
        var product = SeedProduct();

        // Act
        var result = await Run(
            Row(product.Id, cutDate: new LocalDate(2026, 3, 1), releasedDate: new LocalDate(2026, 3, 15)));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var transitions = _dbContext.Versions.Single().StatusTransitions.ToList();
        transitions.Should().HaveCount(3);
        transitions.Select(t => t.ToStatusId).Should().ContainInOrder(_planned.Id, _ready.Id, _released.Id);
    }

    [Fact]
    public async Task CreateVersions_AttributesEveryRowToTheImport()
    {
        // Arrange
        var product = SeedProduct();

        // Act
        await Run(Row(product.Id));

        // Assert
        var transition = _dbContext.Versions.Single().StatusTransitions.Single();
        transition.ActorKind.Should().Be(EventActorKind.Import);
        transition.ActorUserId.Should().Be(_userId);
    }

    [Fact]
    public async Task CreateVersions_AppliesTheNotesARowCarries()
    {
        // Arrange
        var product = SeedProduct();

        // Act
        var result = await Run(Row(product.Id, notes: "Bumped Npgsql to 9.0.2"));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _dbContext.Versions.Single().Notes.Should().Be("Bumped Npgsql to 9.0.2");
    }

    [Fact]
    public async Task CreateVersions_ImportsTwoProductsSharingAVersionNumber()
    {
        // Arrange — a version number is only unique within its product, which is why the key carries both
        var alpha = SeedProduct("Alpha");
        var beta = SeedProduct("Beta");

        // Act
        var result = await Run(Row(alpha.Id, "1.0.0"), Row(beta.Id, "1.0.0"));

        // Assert
        result.Value.Rows.Should().AllSatisfy(r => r.Failed.Should().BeFalse());
        _dbContext.Versions.Should().HaveCount(2);
        _dbContext.Versions.Select(v => v.ProductId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task CreateVersions_RejectsTheSecondRowRepeatingANumberForOneProduct()
    {
        // Arrange — the submission command rejects a repeat before the run starts, but the pass holds the
        // rule too: rows are applied before anything is saved
        var product = SeedProduct();

        // Act
        var result = await Run(Row(product.Id, "1.0.0"), Row(product.Id, "1.0.0"));

        // Assert
        result.Value.Rows.Single(r => r.ImportId == "r1").Failed.Should().BeFalse();
        result.Value.Rows.Single(r => r.ImportId == "r2").Failed.Should().BeTrue();
        _dbContext.Versions.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateVersions_RejectsARowWhoseVersionAlreadyExists()
    {
        // Arrange — a product holding two versions with the same number is a mistake, so re-running a file
        // is refused rather than silently duplicating shipments
        var product = SeedProduct();
        _dbContext.AddVersion(Version.Create(
            product.Id, "1.0.0", null, null, null, true, StatusRef.From(_planned), product.Name,
            EventActor.System, Now).Value);

        // Act
        var result = await Run(Row(product.Id, "1.0.0"));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("1.0.0");
        _dbContext.Versions.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateVersions_RejectsARowNamingAProductThatDoesNotExist()
    {
        // Arrange
        SeedProduct();
        var missing = Guid.CreateVersion7();

        // Act
        var result = await Run(Row(missing));

        // Assert — named per row, so the person knows which line to fix
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain(missing.ToString());
        _dbContext.Versions.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateVersions_RejectsARowNamingAProductWhoseTypeIsNotReleasable()
    {
        // Arrange
        var product = SeedProduct("Wayd", isReleasable: false);

        // Act
        var result = await Run(Row(product.Id));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("not releasable");
        _dbContext.Versions.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateVersions_TrimsAVersionNumberSurroundedByWhitespace()
    {
        // Arrange
        var product = SeedProduct();

        // Act
        var result = await Run(Row(product.Id, " 1.0.0 "));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _dbContext.Versions.Single().Number.Should().Be("1.0.0");
    }
}
