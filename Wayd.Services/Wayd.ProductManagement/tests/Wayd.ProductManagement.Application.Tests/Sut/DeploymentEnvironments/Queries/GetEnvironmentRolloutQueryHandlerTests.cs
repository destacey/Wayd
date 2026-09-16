using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.DeploymentEnvironments.Queries;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Tests.Sut.DeploymentEnvironments.Queries;

/// <summary>
/// What each environment is running, derived from the deployment record.
/// </summary>
/// <remarks>
/// The distinction every test here turns on is that the running version is the latest deployment that
/// <em>succeeded and stayed succeeded</em>, not the latest deployment. A failure and a rollback both
/// leave the version before them in place, and reporting the latest attempt instead would tell a
/// reader something is live that is not.
/// </remarks>
public sealed class GetEnvironmentRolloutQueryHandlerTests : ProductCommandTestBase
{
    private static readonly Instant Day1 = Instant.FromUtc(2026, 4, 1, 9, 0, 0);
    private static readonly Instant Day2 = Instant.FromUtc(2026, 4, 2, 9, 0, 0);
    private static readonly Instant Day3 = Instant.FromUtc(2026, 4, 3, 9, 0, 0);

    private GetEnvironmentRolloutQueryHandler CreateSut() => new(DbContext);

    private Deployment Deploy(
        DeploymentEnvironment environment,
        Guid versionId,
        Instant startedAt,
        string? artifactId = null)
    {
        var deployment = Deployment.Create(
            versionId,
            null,
            environment.Id,
            environment.Category,
            artifactId,
            startedAt,
            Status("In Progress", StatusCategory.Active, ProductStatusAlias.InProgress),
            environment.Name,
            EventActor.System,
            startedAt).Value;

        deployment.ClearDomainEvents();
        DbContext.AddDeployment(deployment);

        return deployment;
    }

    private void Succeed(Deployment deployment, DeploymentEnvironment environment, Instant at) =>
        deployment.Succeed(
            at,
            Status("Succeeded", StatusCategory.Done, ProductStatusAlias.Succeeded),
            environment.Name,
            EventActor.System,
            at);

    /// <summary>
    /// A package with a manifest the caller controls, so component versions can differ between bundles.
    /// </summary>
    /// <remarks>
    /// Built here rather than through the base helper, whose manifest is fixed at one component at
    /// "1.0" — which cannot express two bundles carrying the same product at different versions, the
    /// case these tests exist for.
    /// </remarks>
    private ReleasePackage SeedPackage(
        string version,
        params (Guid ProductId, string Version)[] components)
    {
        var package = ReleasePackage.Create(
            version,
            null,
            null,
            [.. components.Select(c => (c.ProductId, (Guid?)null, c.Version, ManifestEntryKind.Changed))],
            Status("Released", StatusCategory.Done, ProductStatusAlias.None),
            EventActor.System,
            Now).Value;

        package.ClearDomainEvents();
        DbContext.AddReleasePackage(package);

        foreach (var component in package.Components)
        {
            DbContext.AddReleasePackageComponent(component);
        }

        return package;
    }

    private Deployment DeployPackage(
        DeploymentEnvironment environment,
        Guid packageId,
        Instant startedAt)
    {
        var deployment = Deployment.Create(
            null,
            packageId,
            environment.Id,
            environment.Category,
            null,
            startedAt,
            Status("In Progress", StatusCategory.Active, ProductStatusAlias.InProgress),
            environment.Name,
            EventActor.System,
            startedAt).Value;

        deployment.ClearDomainEvents();
        DbContext.AddDeployment(deployment);

        return deployment;
    }

    private void Fail(Deployment deployment, DeploymentEnvironment environment, Instant at) =>
        deployment.Fail(
            at,
            null,
            Status("Failed", StatusCategory.Removed, ProductStatusAlias.Failed),
            environment.Name,
            EventActor.System,
            at);

    [Fact]
    public async Task Handle_ShouldReportTheLatestSucceededVersion_WhenAProductWasDeployedTwice()
    {
        // Arrange
        var environment = SeedEnvironment("Prod", EnvironmentCategory.Production, 3);
        var product = SeedProduct("Trio VMS");
        var older = SeedVersion(product.Id, "2026.09");
        var newer = SeedVersion(product.Id, "2026.10");

        Succeed(Deploy(environment, older.Id, Day1), environment, Day1);
        Succeed(Deploy(environment, newer.Id, Day2), environment, Day2);

        // Act
        var result = await CreateSut().Handle(
            new GetEnvironmentRolloutQuery(), TestContext.Current.CancellationToken);

        // Assert
        var running = result.Single().Running.Single();
        running.Version!.Name.Should().Be("2026.10");
        running.Product!.Name.Should().Be("Trio VMS");
        running.HasFailedAttemptSince.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldLeaveThePreviousVersionRunning_WhenTheLatestAttemptFailed()
    {
        // Arrange — the failed deployment never reached the environment, so what is there is unchanged.
        var environment = SeedEnvironment("Dev", EnvironmentCategory.Development, 1);
        var product = SeedProduct("Data Extract");
        var running = SeedVersion(product.Id, "1.0.6");
        var attempted = SeedVersion(product.Id, "1.1.0");

        Succeed(Deploy(environment, running.Id, Day1), environment, Day1);
        Fail(Deploy(environment, attempted.Id, Day2), environment, Day2);

        // Act
        var result = await CreateSut().Handle(
            new GetEnvironmentRolloutQuery(), TestContext.Current.CancellationToken);

        // Assert
        var item = result.Single().Running.Single();
        item.Version!.Name.Should().Be("1.0.6");
        item.HasFailedAttemptSince.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldLeaveThePreviousVersionRunning_WhenTheLatestWasRolledBack()
    {
        // Arrange — a rollback replaces its own deployment's status, so the one before it wins again.
        var environment = SeedEnvironment("Prod", EnvironmentCategory.Production, 3);
        var product = SeedProduct("Argo Identity");
        var stable = SeedVersion(product.Id, "3.4.1");
        var reverted = SeedVersion(product.Id, "3.5.0");

        Succeed(Deploy(environment, stable.Id, Day1), environment, Day1);

        var rolledBack = Deploy(environment, reverted.Id, Day2);
        Succeed(rolledBack, environment, Day2);
        rolledBack.RollBack(
            Day3,
            "Auth regression",
            Status("Rolled Back", StatusCategory.Removed, ProductStatusAlias.RolledBack),
            environment.Name,
            EventActor.System,
            Day3);

        // Act
        var result = await CreateSut().Handle(
            new GetEnvironmentRolloutQuery(), TestContext.Current.CancellationToken);

        // Assert
        var item = result.Single().Running.Single();
        item.Version!.Name.Should().Be("3.4.1");
        item.HasFailedAttemptSince.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldReportNothingRunning_WhenTheOnlyDeploymentIsStillInFlight()
    {
        // Arrange — an in-flight deployment has not reached the environment yet.
        var environment = SeedEnvironment("Test", EnvironmentCategory.Testing, 2);
        var product = SeedProduct("Trio VMS");
        Deploy(environment, SeedVersion(product.Id, "2026.10").Id, Day1);

        // Act
        var result = await CreateSut().Handle(
            new GetEnvironmentRolloutQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Single().Running.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldIncludeAnEnvironmentWithNoDeployments()
    {
        // Arrange — "nothing has ever shipped here" is an answer, not missing data.
        SeedEnvironment("Prod", EnvironmentCategory.Production, 3);

        // Act
        var result = await CreateSut().Handle(
            new GetEnvironmentRolloutQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Single().Name.Should().Be("Prod");
        result.Single().Running.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReportEveryProductRunningInOneEnvironment()
    {
        // Arrange
        var environment = SeedEnvironment("Argo — Dev", EnvironmentCategory.Development, 1);
        var identity = SeedProduct("Argo Identity");
        var document = SeedProduct("Argo Document");

        Succeed(Deploy(environment, SeedVersion(identity.Id, "3.4.1").Id, Day1), environment, Day1);
        Succeed(Deploy(environment, SeedVersion(document.Id, "1.7.2").Id, Day1), environment, Day1);

        // Act
        var result = await CreateSut().Handle(
            new GetEnvironmentRolloutQuery(), TestContext.Current.CancellationToken);

        // Assert — one entry per product, named alphabetically so the list reads the same every load.
        result.Single().Running.Select(i => i.Product!.Name)
            .Should().Equal("Argo Document", "Argo Identity");
    }

    [Fact]
    public async Task Handle_ShouldExcludeRetiredEnvironments_ByDefault()
    {
        // Arrange — nothing is running in an environment that can no longer be deployed into.
        SeedEnvironment("Prod", EnvironmentCategory.Production, 3);
        var retired = SeedEnvironment("Old QA", EnvironmentCategory.Testing, 2);
        retired.Deactivate(EventActor.System, Now);

        // Act
        var result = await CreateSut().Handle(
            new GetEnvironmentRolloutQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Select(e => e.Name).Should().Equal("Prod");
    }

    [Fact]
    public async Task Handle_ShouldIncludeRetiredEnvironments_WhenAsked()
    {
        // Arrange
        SeedEnvironment("Prod", EnvironmentCategory.Production, 3);
        var retired = SeedEnvironment("Old QA", EnvironmentCategory.Testing, 2);
        retired.Deactivate(EventActor.System, Now);

        // Act
        var result = await CreateSut().Handle(
            new GetEnvironmentRolloutQuery(IncludeInactive: true), TestContext.Current.CancellationToken);

        // Assert
        result.Select(e => e.Name).Should().Equal("Old QA", "Prod");
        result.Single(e => e.Name == "Old QA").IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldOrderEnvironmentsByRingThenName()
    {
        // Arrange — the order a release travels in, which is what makes the page scannable.
        SeedEnvironment("Prod", EnvironmentCategory.Production, 3);
        SeedEnvironment("Dev", EnvironmentCategory.Development, 1);
        SeedEnvironment("UAT — Contoso", EnvironmentCategory.Staging, 2);
        SeedEnvironment("UAT — Fabrikam", EnvironmentCategory.Staging, 2);

        // Act
        var result = await CreateSut().Handle(
            new GetEnvironmentRolloutQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Select(e => e.Name)
            .Should().Equal("Dev", "UAT — Contoso", "UAT — Fabrikam", "Prod");
    }

    [Fact]
    public async Task Handle_ShouldKeepEnvironmentsSeparate_WhenOneProductRunsInBoth()
    {
        // Arrange — a newer version in one environment says nothing about the other.
        var dev = SeedEnvironment("Dev", EnvironmentCategory.Development, 1);
        var prod = SeedEnvironment("Prod", EnvironmentCategory.Production, 3);
        var product = SeedProduct("Trio VMS");
        var shipped = SeedVersion(product.Id, "2026.09");
        var next = SeedVersion(product.Id, "2026.10");

        Succeed(Deploy(prod, shipped.Id, Day1), prod, Day1);
        Succeed(Deploy(dev, next.Id, Day2), dev, Day2);

        // Act
        var result = await CreateSut().Handle(
            new GetEnvironmentRolloutQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Single(e => e.Name == "Dev").Running.Single().Version!.Name.Should().Be("2026.10");
        result.Single(e => e.Name == "Prod").Running.Single().Version!.Name.Should().Be("2026.09");
    }

    [Fact]
    public async Task Handle_ShouldExpandAPackageIntoItsComponents()
    {
        // Arrange — a package is how versions arrive, not a thing that runs beside them.
        var environment = SeedEnvironment("Dev", EnvironmentCategory.Development, 1);
        var catalog = SeedProduct("Catalog API");
        var billing = SeedProduct("Billing Portal");
        var package = SeedPackage("OFT-2026.07", (catalog.Id, "4.1.0"), (billing.Id, "3.11.0"));

        Succeed(DeployPackage(environment, package.Id, Day1), environment, Day1);

        // Act
        var result = await CreateSut().Handle(
            new GetEnvironmentRolloutQuery(), TestContext.Current.CancellationToken);

        // Assert — one entry per component product, each naming the package it arrived on.
        var running = result.Single().Running;
        running.Select(i => i.Product.Name).Should().Equal("Billing Portal", "Catalog API");
        running.Select(i => i.VersionLabel).Should().Equal("3.11.0", "4.1.0");
        running.Should().OnlyContain(i => i.Package!.Name == "OFT-2026.07");
    }

    [Fact]
    public async Task Handle_ShouldSupersedeAnEarlierPackage_WhenALaterOneCarriesTheSameProduct()
    {
        // Arrange — the defect this test exists for: keying on the package id left every bundle ever
        // deployed reported as running, because successive bundles carry the same components and no
        // bundle supersedes a differently-numbered one.
        var environment = SeedEnvironment("Dev", EnvironmentCategory.Development, 1);
        var catalog = SeedProduct("Catalog API");
        var older = SeedPackage("OFT-2026.06", (catalog.Id, "4.0.0"));
        var newer = SeedPackage("OFT-2026.07", (catalog.Id, "4.1.0"));

        Succeed(DeployPackage(environment, older.Id, Day1), environment, Day1);
        Succeed(DeployPackage(environment, newer.Id, Day2), environment, Day2);

        // Act
        var result = await CreateSut().Handle(
            new GetEnvironmentRolloutQuery(), TestContext.Current.CancellationToken);

        // Assert — one entry, from the later bundle.
        var running = result.Single().Running.Single();
        running.Product.Name.Should().Be("Catalog API");
        running.VersionLabel.Should().Be("4.1.0");
        running.Package!.Name.Should().Be("OFT-2026.07");
    }

    [Fact]
    public async Task Handle_ShouldPickTheLatest_WhenAProductShipsBothOnItsOwnAndInAPackage()
    {
        // Arrange — the two routes compete for the same product's slot, so the winner has to be
        // chosen across both rather than within each.
        var environment = SeedEnvironment("Dev", EnvironmentCategory.Development, 1);
        var catalog = SeedProduct("Catalog API");
        var package = SeedPackage("OFT-2026.06", (catalog.Id, "4.0.0"));

        Succeed(DeployPackage(environment, package.Id, Day1), environment, Day1);
        Succeed(Deploy(environment, SeedVersion(catalog.Id, "4.2.0").Id, Day2), environment, Day2);

        // Act
        var result = await CreateSut().Handle(
            new GetEnvironmentRolloutQuery(), TestContext.Current.CancellationToken);

        // Assert — the standalone version shipped later, so it wins and carries no package.
        var running = result.Single().Running.Single();
        running.VersionLabel.Should().Be("4.2.0");
        running.Package.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldCarryTheArtifactOfTheRunningDeployment()
    {
        // Arrange — two builds of one version are two deployments, so the artifact identifies which.
        var environment = SeedEnvironment("Dev", EnvironmentCategory.Development, 1);
        var product = SeedProduct("Trio VMS");
        var version = SeedVersion(product.Id, "2026.10");

        Succeed(Deploy(environment, version.Id, Day1, "2026.10.0914.1"), environment, Day1);
        Succeed(Deploy(environment, version.Id, Day2, "2026.10.0915.3"), environment, Day2);

        // Act
        var result = await CreateSut().Handle(
            new GetEnvironmentRolloutQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Single().Running.Single().ArtifactId.Should().Be("2026.10.0915.3");
    }
}
