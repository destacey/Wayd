using FluentAssertions;
using NodaTime;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Domain.Models;
using Wayd.ProductManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.ProductManagement.Domain.Tests.Sut.Models;

public sealed class DeploymentTests
{
    private const string EnvironmentName = "Production";

    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly DeploymentFaker _faker;

    public DeploymentTests()
    {
        _dateTimeProvider = new(new FakeClock(DateTime.UtcNow.ToInstant()));
        _faker = new DeploymentFaker();
    }

    #region Create

    [Fact]
    public void Create_ForARelease_Success()
    {
        // Arrange
        var releaseId = Guid.CreateVersion7();
        var environmentId = Guid.CreateVersion7();

        // Act
        var result = Deployment.Create(releaseId, null, environmentId, EnvironmentCategory.Production, "4.8.2.008", _dateTimeProvider.Now, StatusRefFactory.InProgress(), EnvironmentName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ReleaseId.Should().Be(releaseId);
        result.Value.PackageId.Should().BeNull();
        result.Value.ArtifactId.Should().Be("4.8.2.008");
        result.Value.Outcome.Should().Be(ProductStatusAlias.InProgress);
        result.Value.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void Create_ShouldFail_WhenNeitherReleaseNorPackageIsSupplied()
    {
        // Act
        var result = Deployment.Create(null, null, Guid.CreateVersion7(), EnvironmentCategory.Production, null, _dateTimeProvider.Now, StatusRefFactory.InProgress(), EnvironmentName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A deployment must be for either a release or a package.");
    }

    [Fact]
    public void Create_ShouldFail_WhenBothAReleaseAndAPackageAreSupplied()
    {
        // Act
        var result = Deployment.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), EnvironmentCategory.Production, null, _dateTimeProvider.Now, StatusRefFactory.InProgress(), EnvironmentName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // One pipeline run shipping fifteen services must count once, not fifteen times.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A deployment is for either a release or a package, not both. Where a package exists it is the unit, so that one pipeline run counts once.");
    }

    [Fact]
    public void Create_ShouldKeepTheArtifactIdSeparateFromTheReleaseVersion()
    {
        // Arrange & Act
        var result = Deployment.Create(Guid.CreateVersion7(), null, Guid.CreateVersion7(), EnvironmentCategory.Production, "4.8.2.008", _dateTimeProvider.Now, StatusRefFactory.InProgress(), EnvironmentName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // 4.8.2.005 and 4.8.2.008 are two deployments of release 4.8.2; conflating the fields would
        // cost a migration and a re-import later.
        result.Value.ArtifactId.Should().Be("4.8.2.008");
    }

    #endregion Create

    #region Succeed

    [Fact]
    public void Succeed_ShouldCompleteAndRaiseEventCarryingTheEnvironmentCategory()
    {
        // Arrange
        var sut = _faker.Generate();
        var completedAt = sut.StartedAt.Plus(Duration.FromMinutes(12));

        // Act
        var result = sut.Succeed(completedAt, StatusRefFactory.Succeeded(), EnvironmentName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.CompletedAt.Should().Be(completedAt);
        sut.Outcome.Should().Be(ProductStatusAlias.Succeeded);
        sut.IsComplete.Should().BeTrue();

        var succeeded = sut.DomainEvents.OfType<DeploymentSucceededEvent>().Single();
        succeeded.EnvironmentCategory.Should().Be(EnvironmentCategory.Production);
    }

    [Fact]
    public void Succeed_ShouldFail_WhenCompletedBeforeItStarted()
    {
        // Arrange
        var sut = _faker.Generate();

        // Act
        var result = sut.Succeed(sut.StartedAt.Minus(Duration.FromMinutes(1)), StatusRefFactory.Succeeded(), EnvironmentName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The completion cannot be before the deployment started.");
    }

    [Fact]
    public void Succeed_ShouldFail_WhenAlreadyComplete()
    {
        // Arrange
        var sut = _faker.AsSucceeded(Instant.FromUtc(2026, 5, 1, 10, 0)).Generate();

        // Act
        var result = sut.Succeed(Instant.FromUtc(2026, 5, 1, 11, 0), StatusRefFactory.Succeeded(), EnvironmentName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This deployment has already completed.");
    }

    #endregion Succeed

    #region Fail

    [Fact]
    public void Fail_ShouldCompleteWithAReasonAndRaiseItsOwnEvent()
    {
        // Arrange
        var sut = _faker.Generate();
        var completedAt = sut.StartedAt.Plus(Duration.FromMinutes(3));

        // Act
        var result = sut.Fail(completedAt, "Migration timed out.", StatusRefFactory.Failed(), EnvironmentName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Outcome.Should().Be(ProductStatusAlias.Failed);
        sut.Reason.Should().Be("Migration timed out.");

        // A distinct type from a rollback: this one never reached users, a rollback did.
        sut.DomainEvents.Should().ContainSingle(e => e is DeploymentFailedEvent);
    }

    #endregion Fail

    #region RollBack

    [Fact]
    public void RollBack_ShouldBeAllowedFromASucceededDeployment()
    {
        // Arrange
        var completedAt = Instant.FromUtc(2026, 5, 1, 10, 0);
        var sut = _faker.AsSucceeded(completedAt).Generate();

        // Act
        var result = sut.RollBack(completedAt.Plus(Duration.FromHours(2)), "Checkout errors spiked.", StatusRefFactory.RolledBack(), EnvironmentName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // A rollback normally follows a deployment that appeared to work — which is exactly what makes
        // it the strongest change-failure signal available without an incident feed.
        result.IsSuccess.Should().BeTrue();
        sut.Outcome.Should().Be(ProductStatusAlias.RolledBack);
        sut.DomainEvents.Should().ContainSingle(e => e is DeploymentRolledBackEvent);
    }

    [Fact]
    public void RollBack_ShouldFail_WhenTheDeploymentIsStillInProgress()
    {
        // Arrange
        var sut = _faker.Generate();

        // Act
        var result = sut.RollBack(sut.StartedAt.Plus(Duration.FromMinutes(5)), null, StatusRefFactory.RolledBack(), EnvironmentName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // A deployment still in flight has not reached its environment, so counting it as a rollback
        // would inflate change failure rate with something that never reached users.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A deployment that is still in progress cannot be rolled back. Record its outcome first.");
        sut.IsChangeFailure.Should().BeFalse();
    }

    [Fact]
    public void RollBack_ShouldFail_WhenItPredatesCompletion()
    {
        // Arrange
        var completedAt = Instant.FromUtc(2026, 5, 1, 10, 0);
        var sut = _faker.AsSucceeded(completedAt).Generate();

        // Act
        var result = sut.RollBack(completedAt.Minus(Duration.FromMinutes(30)), null, StatusRefFactory.RolledBack(), EnvironmentName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The rollback cannot be before the deployment completed.");
    }

    [Fact]
    public void RollBack_ShouldFail_WhenTheDeploymentFailed()
    {
        // Arrange
        var sut = _faker.AsFailed(Instant.FromUtc(2026, 5, 1, 10, 0)).Generate();

        // Act
        var result = sut.RollBack(Instant.FromUtc(2026, 5, 1, 11, 0), null, StatusRefFactory.RolledBack(), EnvironmentName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A failed deployment never reached its environment and cannot be rolled back.");
    }

    [Fact]
    public void RollBack_ShouldFail_WhenAlreadyRolledBack()
    {
        // Arrange
        var sut = _faker.AsRolledBack(Instant.FromUtc(2026, 5, 1, 10, 0)).Generate();

        // Act
        var result = sut.RollBack(Instant.FromUtc(2026, 5, 1, 11, 0), null, StatusRefFactory.RolledBack(), EnvironmentName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This deployment has already been rolled back.");
    }

    #endregion RollBack

    #region IsChangeFailure

    [Fact]
    public void IsChangeFailure_ShouldBeTrue_ForAFailedProductionDeployment()
    {
        // Arrange
        var sut = _faker.WithEnvironmentCategory(EnvironmentCategory.Production).AsFailed(Instant.FromUtc(2026, 5, 1, 10, 0)).Generate();

        // Act & Assert
        sut.IsChangeFailure.Should().BeTrue();
    }

    [Fact]
    public void IsChangeFailure_ShouldBeTrue_ForARolledBackProductionDeployment()
    {
        // Arrange
        var sut = _faker.WithEnvironmentCategory(EnvironmentCategory.Production).AsRolledBack(Instant.FromUtc(2026, 5, 1, 10, 0)).Generate();

        // Act & Assert
        sut.IsChangeFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData(EnvironmentCategory.Development)]
    [InlineData(EnvironmentCategory.Testing)]
    [InlineData(EnvironmentCategory.Staging)]
    public void IsChangeFailure_ShouldBeFalse_ForAFailureOutsideProduction(EnvironmentCategory category)
    {
        // Arrange
        var sut = _faker.WithEnvironmentCategory(category).AsFailed(Instant.FromUtc(2026, 5, 1, 10, 0)).Generate();

        // Act & Assert
        // A run that failed before reaching production is a failure that was PREVENTED. Counting it
        // inflates change failure rate while describing the opposite of what happened.
        sut.IsChangeFailure.Should().BeFalse();
    }

    [Fact]
    public void IsChangeFailure_ShouldBeFalse_ForASucceededProductionDeployment()
    {
        // Arrange
        var sut = _faker.WithEnvironmentCategory(EnvironmentCategory.Production).AsSucceeded(Instant.FromUtc(2026, 5, 1, 10, 0)).Generate();

        // Act & Assert
        sut.IsChangeFailure.Should().BeFalse();
    }

    #endregion IsChangeFailure

    #region Environment category freezing

    [Fact]
    public void EnvironmentCategory_ShouldBeFrozenOnTheRecord()
    {
        // Arrange
        var sut = _faker.WithEnvironmentCategory(EnvironmentCategory.Staging).AsSucceeded(Instant.FromUtc(2026, 5, 1, 10, 0)).Generate();

        // Act
        var succeeded = sut.EnvironmentCategory;

        // Assert
        // Reclassifying the environment later must not retroactively rewrite what this deployment
        // counted as, so the category is stored rather than resolved through EnvironmentId on read.
        succeeded.Should().Be(EnvironmentCategory.Staging);
        sut.IsChangeFailure.Should().BeFalse();
    }

    #endregion Environment category freezing
}
