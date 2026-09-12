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
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Deployments.Dtos;
using Wayd.ProductManagement.Application.Deployments.Imports;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

// The delivery artifact record, not System.Version.
using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.ProductManagement.Application.Tests.Sut.Deployments.Imports;

/// <summary>
/// Importing deployments. The definition's own work is resolving what a row names, deciding whether a
/// retired environment may be named, and walking each row to the outcome it describes.
/// </summary>
public sealed class DeploymentImportDefinitionTests
{
    private const int CreatePass = 0;

    private static readonly Instant Now = Instant.FromUtc(2026, 4, 1, 9, 0, 0);
    private static readonly Instant Started = Instant.FromUtc(2026, 3, 1, 14, 30, 0);
    private static readonly Instant Completed = Instant.FromUtc(2026, 3, 1, 14, 45, 0);
    private static readonly Instant RolledBack = Instant.FromUtc(2026, 3, 1, 16, 0, 0);

    private readonly FakeProductManagementDbContext _dbContext = new();
    private readonly Mock<IStatusResolver> _statusResolver = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();

    private readonly string _userId = Guid.CreateVersion7().ToString();
    private readonly WorkflowStatus _inProgress;
    private readonly WorkflowStatus _succeeded;
    private readonly WorkflowStatus _failed;
    private readonly WorkflowStatus _rolledBack;
    private readonly DeploymentImportDefinition _definition;

    public DeploymentImportDefinitionTests()
    {
        ProductWorkflowOwners.Register();

        _currentUser.Setup(u => u.GetUserId()).Returns(_userId);
        _dateTimeProvider.SetupGet(d => d.Now).Returns(Now);

        var workflow = StatusWorkflow
            .CreateSystem("Deployment Lifecycle", null, ProductWorkflowOwners.Deployment.Key).Value;
        _inProgress = workflow.AddSystemStatus("In Progress", null, StatusCategory.Active, (int)ProductStatusAlias.InProgress);
        _succeeded = workflow.AddSystemStatus("Succeeded", null, StatusCategory.Done, (int)ProductStatusAlias.Succeeded);
        _failed = workflow.AddSystemStatus("Failed", null, StatusCategory.Removed, (int)ProductStatusAlias.Failed);
        _rolledBack = workflow.AddSystemStatus("Rolled Back", null, StatusCategory.Removed, (int)ProductStatusAlias.RolledBack);
        workflow.PublishSystem();

        Resolve(ProductStatusAlias.InProgress, _inProgress);
        Resolve(ProductStatusAlias.Succeeded, _succeeded);
        Resolve(ProductStatusAlias.Failed, _failed);
        Resolve(ProductStatusAlias.RolledBack, _rolledBack);

        _definition = new DeploymentImportDefinition(
            _dbContext,
            _statusResolver.Object,
            _currentUser.Object,
            _dateTimeProvider.Object,
            Mock.Of<ILogger<DeploymentImportDefinition>>(),
            new ImportPayloadSerializer());
    }

    private void Resolve(ProductStatusAlias alias, WorkflowStatus status) =>
        _statusResolver
            .Setup(r => r.ForAlias(ProductWorkflowOwners.Deployment.Key, null, (int)alias, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(StatusRef.From(status)));

    private DeploymentEnvironment SeedEnvironment(
        string name = "Production", EnvironmentCategory category = EnvironmentCategory.Production, bool isActive = true)
    {
        var environment = DeploymentEnvironment.Create(name, category, 0, EventActor.System, Now);
        if (!isActive)
            environment.Deactivate(EventActor.System, Now);

        _dbContext.AddDeploymentEnvironment(environment);

        return environment;
    }

    private Version SeedVersion()
    {
        var productType = ProductType.Create("Service", null, true, 1);
        _dbContext.AddProductType(productType);

        var productStatus = new StatusRef(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Active", StatusCategory.Active, (int)ProductStatusAlias.Active);
        var product = Product.Create("Wayd API", null, productType.Id, null, null, productStatus, EventActor.System, Now);
        _dbContext.AddProduct(product);

        var versionStatus = new StatusRef(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Planned", StatusCategory.Proposed, StatusWorkflow.NoAlias);
        var version = Version.Create(
            product.Id, "4.10.0", null, null, null, true, versionStatus, product.Name, EventActor.System, Now).Value;
        _dbContext.AddVersion(version);

        return version;
    }

    private ReleasePackage SeedPackage()
    {
        var status = new StatusRef(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Assembled", StatusCategory.Active, StatusWorkflow.NoAlias);
        var package = ReleasePackage.Create(
            "WAYD-2026.09", null, null,
            [(Guid.CreateVersion7(), null, "4.10.0", ManifestEntryKind.Changed)],
            status, EventActor.System, Now).Value;
        _dbContext.AddReleasePackage(package);

        return package;
    }

    private ImportProcessRow[] Rows(params ImportDeploymentDto[] deployments) =>
        [.. deployments.Select((d, i) => ImportProcessRow.Create($"r{i + 1}", i + 1, _definition.SerializeRow(d)))];

    private Task<Result<ImportPassResult>> Run(params ImportDeploymentDto[] deployments) =>
        _definition.ExecutePass(
            Guid.CreateVersion7(), CreatePass, Rows(deployments), isFinalChunk: true, TestContext.Current.CancellationToken);

    private static ImportDeploymentDto Row(
        Guid? versionId = null,
        Guid? packageId = null,
        string environmentName = "Production",
        string? artifactId = null,
        ImportDeploymentOutcome? outcome = null,
        Instant? completedAt = null,
        Instant? rolledBackAt = null,
        string? reason = null) =>
        new(versionId, packageId, environmentName, artifactId, Started, outcome, completedAt, rolledBackAt, reason);

    [Fact]
    public void Definition_IsAtomic()
    {
        // Arrange & Act & Assert
        _definition.Atomicity.Should().Be(ImportAtomicity.Atomic);
        _definition.Passes.Single().Name.Should().Be("CreateDeployments");
    }

    [Fact]
    public async Task CreateDeployments_LeavesARowWithNoOutcomeInFlight()
    {
        // Arrange
        var environment = SeedEnvironment();
        var version = SeedVersion();

        // Act
        var result = await Run(Row(versionId: version.Id, artifactId: "4.10.0.008"));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeFalse();

        var deployment = _dbContext.Deployments.Single();
        deployment.VersionId.Should().Be(version.Id);
        deployment.PackageId.Should().BeNull();
        deployment.EnvironmentId.Should().Be(environment.Id);
        deployment.EnvironmentCategory.Should().Be(EnvironmentCategory.Production);
        deployment.ArtifactId.Should().Be("4.10.0.008");
        deployment.StartedAt.Should().Be(Started);
        deployment.CompletedAt.Should().BeNull();
        deployment.StatusId.Should().Be(_inProgress.Id);
        outcome.CreatedEntityId.Should().Be(deployment.Id);
    }

    [Fact]
    public async Task CreateDeployments_DeploysAPackage()
    {
        // Arrange
        SeedEnvironment();
        var package = SeedPackage();

        // Act
        var result = await Run(Row(packageId: package.Id));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var deployment = _dbContext.Deployments.Single();
        deployment.PackageId.Should().Be(package.Id);
        deployment.VersionId.Should().BeNull();
    }

    [Fact]
    public async Task CreateDeployments_SucceedsARowThatSucceeded()
    {
        // Arrange
        SeedEnvironment();
        var version = SeedVersion();

        // Act
        var result = await Run(Row(version.Id, outcome: ImportDeploymentOutcome.Succeeded, completedAt: Completed));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var deployment = _dbContext.Deployments.Single();
        deployment.StatusId.Should().Be(_succeeded.Id);
        deployment.CompletedAt.Should().Be(Completed);
    }

    [Fact]
    public async Task CreateDeployments_FailsARowThatFailedWithItsReason()
    {
        // Arrange
        SeedEnvironment();
        var version = SeedVersion();

        // Act
        var result = await Run(Row(
            version.Id, outcome: ImportDeploymentOutcome.Failed, completedAt: Completed, reason: "Migration timed out"));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var deployment = _dbContext.Deployments.Single();
        deployment.StatusId.Should().Be(_failed.Id);
        deployment.CompletedAt.Should().Be(Completed);
        deployment.Reason.Should().Be("Migration timed out");
    }

    [Fact]
    public async Task CreateDeployments_RollsBackARowThroughSuccessFirst()
    {
        // Arrange — the domain only permits rolling back a deployment that reached its environment, so a
        // rolled-back row is walked through success, and its history says so
        SeedEnvironment();
        var version = SeedVersion();

        // Act
        var result = await Run(Row(
            version.Id, outcome: ImportDeploymentOutcome.RolledBack, completedAt: Completed, rolledBackAt: RolledBack,
            reason: "Error rate spiked"));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();

        var deployment = _dbContext.Deployments.Single();
        deployment.StatusId.Should().Be(_rolledBack.Id);
        deployment.CompletedAt.Should().Be(RolledBack);
        deployment.Reason.Should().Be("Error rate spiked");

        var transitions = deployment.StatusTransitions.ToList();
        transitions.Select(t => t.ToStatusId).Should().ContainInOrder(_inProgress.Id, _succeeded.Id, _rolledBack.Id);
        transitions.Select(t => t.ChangedOn).Should().ContainInOrder(Started, Completed, RolledBack);
    }

    [Fact]
    public async Task CreateDeployments_DefersEventsInChronologicalOrderWithRowTimestamps()
    {
        // Arrange
        SeedEnvironment();
        var version = SeedVersion();

        // Act
        var result = await Run(Row(
            version.Id, outcome: ImportDeploymentOutcome.RolledBack, completedAt: Completed, rolledBackAt: RolledBack,
            reason: "Error rate spiked"));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        var deployment = _dbContext.Deployments.Single();
        deployment.DomainEvents.Should().BeEmpty();
        deployment.PostPersistenceActions.Should().HaveCount(3);

        deployment.ExecutePostPersistenceActions();

        var events = deployment.DomainEvents.ToList();
        events.Should().HaveCount(3);

        var started = events[0].Should().BeOfType<DeploymentStartedEvent>().Subject;
        started.StatusId.Should().Be(_inProgress.Id);
        started.StartedAt.Should().Be(Started);

        var succeeded = events[1].Should().BeOfType<DeploymentSucceededEvent>().Subject;
        succeeded.StatusId.Should().Be(_succeeded.Id);
        succeeded.CompletedAt.Should().Be(Completed);

        var rolledBack = events[2].Should().BeOfType<DeploymentRolledBackEvent>().Subject;
        rolledBack.StatusId.Should().Be(_rolledBack.Id);
        rolledBack.RolledBackAt.Should().Be(RolledBack);
        rolledBack.Reason.Should().Be("Error rate spiked");
    }

    [Fact]
    public async Task CreateDeployments_AttributesEveryRowToTheImport()
    {
        // Arrange
        SeedEnvironment();
        var version = SeedVersion();

        // Act
        await Run(Row(version.Id));

        // Assert
        var transition = _dbContext.Deployments.Single().StatusTransitions.Single();
        transition.ActorKind.Should().Be(EventActorKind.Import);
        transition.ActorUserId.Should().Be(_userId);
        transition.ChangedOn.Should().Be(Started);
    }

    [Fact]
    public async Task CreateDeployments_AcceptsARetiredEnvironmentForARowThatFinished()
    {
        // Arrange — a historical backfill routinely records deployments into environments that have since
        // been decommissioned
        SeedEnvironment(isActive: false);
        var version = SeedVersion();

        // Act
        var result = await Run(Row(version.Id, outcome: ImportDeploymentOutcome.Succeeded, completedAt: Completed));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        _dbContext.Deployments.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateDeployments_RejectsARetiredEnvironmentForARowStillInFlight()
    {
        // Arrange — the same refusal starting one by hand meets
        SeedEnvironment(isActive: false);
        var version = SeedVersion();

        // Act
        var result = await Run(Row(version.Id));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("retired");
        _dbContext.Deployments.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateDeployments_RejectsARowNamingAnEnvironmentThatDoesNotExist()
    {
        // Arrange
        SeedEnvironment();
        var version = SeedVersion();

        // Act
        var result = await Run(Row(version.Id, environmentName: "prod-eu"));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("prod-eu");
        _dbContext.Deployments.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateDeployments_RejectsARowNamingAVersionThatDoesNotExist()
    {
        // Arrange
        SeedEnvironment();
        var missing = Guid.CreateVersion7();

        // Act
        var result = await Run(Row(versionId: missing));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain(missing.ToString());
        _dbContext.Deployments.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateDeployments_RejectsARowNamingAPackageThatDoesNotExist()
    {
        // Arrange
        SeedEnvironment();
        var missing = Guid.CreateVersion7();

        // Act
        var result = await Run(Row(packageId: missing));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain(missing.ToString());
        _dbContext.Deployments.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateDeployments_RejectsARowWhoseCompletionPrecedesItsStart()
    {
        // Arrange — the domain's rule, surfaced per row rather than as a whole-run failure
        SeedEnvironment();
        var version = SeedVersion();

        // Act
        var result = await Run(Row(
            version.Id, outcome: ImportDeploymentOutcome.Succeeded, completedAt: Started - Duration.FromMinutes(1)));

        // Assert
        var outcome = result.Value.Rows.Single();
        outcome.Failed.Should().BeTrue();
        outcome.Error.Should().Contain("before the deployment started");
        _dbContext.Deployments.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateDeployments_AppliesTheRestOfTheFileWhenOneRowIsRejected()
    {
        // Arrange — the runner discards the run when any row fails an atomic import; the pass still
        // reports each row on its own so the run names every problem, not just the first
        SeedEnvironment();
        var version = SeedVersion();

        // Act
        var result = await Run(Row(version.Id), Row(versionId: Guid.CreateVersion7()), Row(version.Id));

        // Assert
        result.Value.Rows.Single(r => r.ImportId == "r1").Failed.Should().BeFalse();
        result.Value.Rows.Single(r => r.ImportId == "r2").Failed.Should().BeTrue();
        result.Value.Rows.Single(r => r.ImportId == "r3").Failed.Should().BeFalse();
        _dbContext.Deployments.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateDeployments_FailsTheRunWhenTheWorkflowLacksAnOutcome()
    {
        // Arrange — a workflow missing an alias is a configuration fault, not a row's fault
        SeedEnvironment();
        var version = SeedVersion();
        _statusResolver
            .Setup(r => r.ForAlias(
                ProductWorkflowOwners.Deployment.Key, null, (int)ProductStatusAlias.RolledBack, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<StatusRef>("No status carries the RolledBack alias."));

        // Act
        var result = await Run(Row(version.Id));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("RolledBack");
        _dbContext.Deployments.Should().BeEmpty();
    }
}
