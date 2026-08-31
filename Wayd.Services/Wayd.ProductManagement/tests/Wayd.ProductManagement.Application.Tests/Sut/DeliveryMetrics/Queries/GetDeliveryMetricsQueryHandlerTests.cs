using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.DeliveryMetrics.Queries;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Tests.Sut.DeliveryMetrics.Queries;

/// <summary>
/// The two delivery measures this module can compute.
/// </summary>
/// <remarks>
/// Frequency and change failure rate read the same set but count it differently: a rolled-back
/// deployment reached production, so it is delivery that happened <em>and</em> a failure.
/// </remarks>
public sealed class GetDeliveryMetricsQueryHandlerTests : ProductCommandTestBase
{
    private static readonly Instant WindowStart = Instant.FromUtc(2026, 4, 1, 0, 0, 0);
    private static readonly Instant WindowEnd = Instant.FromUtc(2026, 4, 11, 0, 0, 0);

    private GetDeliveryMetricsQueryHandler CreateSut() => new(DbContext);

    /// <summary>
    /// A completed deployment with a chosen outcome, so the counting rules can be exercised directly.
    /// </summary>
    private Deployment SeedCompleted(
        ProductStatusAlias outcome,
        EnvironmentCategory category = EnvironmentCategory.Production,
        Instant? completedAt = null,
        Guid? productId = null)
    {
        var environment = SeedEnvironment($"env-{Guid.CreateVersion7()}"[..12], category, 1);
        var product = productId is null
            ? SeedProduct($"product-{Guid.CreateVersion7()}"[..16])
            : DbContext.Products.First(p => p.Id == productId);
        var release = SeedRelease(product.Id);

        var deployment = Deployment.Create(
            release.Id,
            null,
            environment.Id,
            category,
            null,
            WindowStart,
            Status("In Progress", StatusCategory.Active, ProductStatusAlias.InProgress),
            environment.Name,
            EventActor.System,
            WindowStart).Value;

        var at = completedAt ?? WindowStart.Plus(Duration.FromDays(1));

        switch (outcome)
        {
            case ProductStatusAlias.Succeeded:
                deployment.Succeed(
                    at, Status("Succeeded", StatusCategory.Done, ProductStatusAlias.Succeeded),
                    environment.Name, EventActor.System, at);
                break;

            case ProductStatusAlias.Failed:
                deployment.Fail(
                    at, null, Status("Failed", StatusCategory.Removed, ProductStatusAlias.Failed),
                    environment.Name, EventActor.System, at);
                break;

            case ProductStatusAlias.RolledBack:
                deployment.Succeed(
                    at, Status("Succeeded", StatusCategory.Done, ProductStatusAlias.Succeeded),
                    environment.Name, EventActor.System, at);
                deployment.RollBack(
                    at, null, Status("Rolled Back", StatusCategory.Removed, ProductStatusAlias.RolledBack),
                    environment.Name, EventActor.System, at);
                break;

            // An outcome the organization added itself: a completed deployment whose status carries no
            // alias. Completed matters — the query only considers completed deployments, so a fixture
            // that left this one in progress would report zero for the wrong reason.
            case ProductStatusAlias.None:
                deployment.Succeed(
                    at, Status("Partially Rolled Out", StatusCategory.Done, ProductStatusAlias.None),
                    environment.Name, EventActor.System, at);
                break;
        }

        deployment.ClearDomainEvents();
        DbContext.AddDeployment(deployment);

        return deployment;
    }

    private Task<Application.DeliveryMetrics.Dtos.DeliveryMetricsDto> Run(Guid? productId = null) =>
        CreateSut().Handle(
            new GetDeliveryMetricsQuery(WindowStart, WindowEnd, productId), TestContext.Current.CancellationToken);

    #region Deployment frequency

    [Fact]
    public async Task Handle_ShouldCountSucceededDeployments()
    {
        // Arrange
        SeedCompleted(ProductStatusAlias.Succeeded);
        SeedCompleted(ProductStatusAlias.Succeeded);

        // Act
        var metrics = await Run();

        // Assert
        metrics.DeploymentFrequency.Count.Should().Be(2);
        metrics.DeploymentFrequency.PerDay.Should().Be(0.2);
    }

    [Fact]
    public async Task Handle_ShouldCountARolledBackDeploymentAsDelivered()
    {
        // Arrange
        SeedCompleted(ProductStatusAlias.RolledBack);

        // Act
        var metrics = await Run();

        // Assert
        // It reached production, which is what frequency measures. Whether that was a good idea is
        // change failure rate's question.
        metrics.DeploymentFrequency.Count.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldNotCountAnUnaliasedOutcomeAsDelivered()
    {
        // Statuses are user-extensible, so an organization can add a deployment outcome of its own —
        // it carries no alias. Counting "everything that is not Failed" would silently inflate
        // deployment frequency with statuses this module knows nothing about.
        // Arrange
        SeedCompleted(ProductStatusAlias.None);

        // Act
        var metrics = await Run();

        // Assert
        metrics.DeploymentFrequency.Count.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldNotCountAFailedDeploymentAsDelivered()
    {
        // Arrange
        SeedCompleted(ProductStatusAlias.Failed);

        // Act
        var metrics = await Run();

        // Assert
        // It never reached production, so nothing was delivered.
        metrics.DeploymentFrequency.Count.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldNotCountAnInFlightDeployment()
    {
        // Arrange
        SeedDeployment();

        // Act
        var metrics = await Run();

        // Assert
        metrics.DeploymentFrequency.Count.Should().Be(0);
        metrics.ChangeFailureRate.TotalDeployments.Should().Be(0);
    }

    #endregion Deployment frequency

    #region Change failure rate

    [Fact]
    public async Task Handle_ShouldRateFailuresAgainstAllCompletedDeployments()
    {
        // Arrange
        SeedCompleted(ProductStatusAlias.Succeeded);
        SeedCompleted(ProductStatusAlias.Succeeded);
        SeedCompleted(ProductStatusAlias.Succeeded);
        SeedCompleted(ProductStatusAlias.Failed);

        // Act
        var metrics = await Run();

        // Assert
        metrics.ChangeFailureRate.TotalDeployments.Should().Be(4);
        metrics.ChangeFailureRate.FailedDeployments.Should().Be(1);
        metrics.ChangeFailureRate.Rate.Should().Be(0.25);
    }

    [Fact]
    public async Task Handle_ShouldCountARollbackAsAFailure()
    {
        // Arrange
        SeedCompleted(ProductStatusAlias.Succeeded);
        SeedCompleted(ProductStatusAlias.RolledBack);

        // Act
        var metrics = await Run();

        // Assert
        // The same deployment counts as delivered and as a failure — the two measures ask different
        // questions of it.
        metrics.DeploymentFrequency.Count.Should().Be(2);
        metrics.ChangeFailureRate.FailedDeployments.Should().Be(1);
        metrics.ChangeFailureRate.Rate.Should().Be(0.5);
    }

    [Fact]
    public async Task Handle_ShouldReturnANullRate_WhenNothingDeployed()
    {
        // Act
        var metrics = await Run();

        // Assert
        // Null is "no deployments to judge", which a reader must be able to tell from a rate of zero.
        metrics.ChangeFailureRate.Rate.Should().BeNull();
        metrics.ChangeFailureRate.TotalDeployments.Should().Be(0);
    }

    #endregion Change failure rate

    #region Scope

    [Fact]
    public async Task Handle_ShouldIgnoreDeploymentsOutsideProduction()
    {
        // Arrange
        SeedCompleted(ProductStatusAlias.Succeeded, EnvironmentCategory.Staging);
        SeedCompleted(ProductStatusAlias.Failed, EnvironmentCategory.Staging);

        // Act
        var metrics = await Run();

        // Assert
        // A failure caught before production is a failure that was prevented; counting it would invert
        // the measure.
        metrics.DeploymentFrequency.Count.Should().Be(0);
        metrics.ChangeFailureRate.TotalDeployments.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldIgnoreDeploymentsOutsideTheWindow()
    {
        // Arrange
        SeedCompleted(ProductStatusAlias.Succeeded, completedAt: WindowStart.Minus(Duration.FromDays(1)));
        SeedCompleted(ProductStatusAlias.Succeeded, completedAt: WindowEnd.Plus(Duration.FromDays(1)));
        SeedCompleted(ProductStatusAlias.Succeeded);

        // Act
        var metrics = await Run();

        // Assert
        metrics.DeploymentFrequency.Count.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldNarrowToOneProduct()
    {
        // Arrange
        var wanted = SeedProduct("Wanted");
        SeedCompleted(ProductStatusAlias.Succeeded, productId: wanted.Id);
        SeedCompleted(ProductStatusAlias.Succeeded);

        // Act
        var metrics = await Run(wanted.Id);

        // Assert
        metrics.DeploymentFrequency.Count.Should().Be(1);
    }

    #endregion Scope

    #region Unavailable measures

    [Fact]
    public async Task Handle_ShouldReportTheMeasuresItCannotCompute()
    {
        // Act
        var metrics = await Run();

        // Assert
        // Reported rather than omitted, so a reader can tell "not measured yet" from "no deployments".
        metrics.Unavailable.Should().HaveCount(2);
        metrics.Unavailable.Select(u => u.Metric)
            .Should().BeEquivalentTo(["Lead time for changes", "Time to restore service"]);
        metrics.Unavailable.Should().AllSatisfy(u => u.Reason.Should().NotBeNullOrWhiteSpace());
    }

    #endregion Unavailable measures
}
