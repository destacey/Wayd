using FluentAssertions;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.ProductManagement.Domain.Models;
using Wayd.ProductManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.ProductManagement.Domain.Tests.Sut.Models;

public sealed class DeploymentEnvironmentTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly DeploymentEnvironmentFaker _faker;

    public DeploymentEnvironmentTests()
    {
        _dateTimeProvider = new(new FakeClock(DateTime.UtcNow.ToInstant()));
        _faker = new DeploymentEnvironmentFaker();
    }

    #region Create

    [Fact]
    public void Create_WhenValid_Success()
    {
        // Arrange & Act
        var sut = DeploymentEnvironment.Create("Production", EnvironmentCategory.Production, 4, EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.Name.Should().Be("Production");
        sut.Category.Should().Be(EnvironmentCategory.Production);
        sut.RingOrder.Should().Be(4);
        sut.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldRaiseEnvironmentAddedEvent_AfterPersistence()
    {
        // Arrange & Act
        var sut = DeploymentEnvironment.Create("Production", EnvironmentCategory.Production, 4, EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.DomainEvents.Should().BeEmpty();
        sut.PostPersistenceActions.First()();

        sut.DomainEvents.Should().ContainSingle(e => e is EnvironmentAddedEvent);
    }

    [Fact]
    public void Create_WithBlankName_Throws()
    {
        // Act
        Action act = () => DeploymentEnvironment.Create("  ", EnvironmentCategory.Production, 1, EventActor.System, _dateTimeProvider.Now);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("Required input Name was empty. (Parameter 'Name')");
    }

    #endregion Create

    #region Reclassify

    [Fact]
    public void Reclassify_ShouldRaiseItsOwnEventCarryingBothCategories()
    {
        // Arrange
        var sut = _faker.WithCategory(EnvironmentCategory.Staging).Generate();

        // Act
        var result = sut.Reclassify(EnvironmentCategory.Production, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // Promoting an environment to production retroactively changes deployment frequency and every
        // production-scoped measure, so it is a fact worth a name rather than an ordinary edit.
        result.IsSuccess.Should().BeTrue();
        sut.Category.Should().Be(EnvironmentCategory.Production);

        var reclassified = sut.DomainEvents.OfType<EnvironmentReclassifiedEvent>().Single();
        reclassified.FromCategory.Should().Be(EnvironmentCategory.Staging);
        reclassified.ToCategory.Should().Be(EnvironmentCategory.Production);
    }

    [Fact]
    public void Reclassify_ToTheSameCategory_ShouldSucceedWithoutRaisingAnEvent()
    {
        // Arrange
        var sut = _faker.WithCategory(EnvironmentCategory.Production).Generate();

        // Act
        var result = sut.Reclassify(EnvironmentCategory.Production, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Reclassify_ShouldFail_WhenTheEnvironmentIsRetired()
    {
        // Arrange
        var sut = _faker.AsRetired().Generate();

        // Act
        var result = sut.Reclassify(EnvironmentCategory.Production, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A retired environment cannot be reclassified.");
    }

    #endregion Reclassify

    #region Deactivate

    [Fact]
    public void Deactivate_ShouldDeactivateAndRaiseEvent()
    {
        // Arrange
        var sut = _faker.Generate();

        // Act
        var result = sut.Deactivate(EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.IsActive.Should().BeFalse();
        sut.DomainEvents.Should().ContainSingle(e => e is EnvironmentRetiredEvent);
    }

    [Fact]
    public void Deactivate_ShouldFail_WhenAlreadyInactive()
    {
        // Arrange
        var sut = _faker.AsRetired().Generate();

        // Act
        var result = sut.Deactivate(EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This environment is already inactive.");
    }

    #endregion Deactivate

    #region Update

    [Fact]
    public void Update_ShouldRenameAndRepositionInTheRollout()
    {
        // Arrange
        var sut = _faker.WithRingOrder(2).Generate();

        // Act
        var result = sut.Update("Production EU", 5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Name.Should().Be("Production EU");
        sut.RingOrder.Should().Be(5);
    }

    [Fact]
    public void Update_ShouldFail_WhenTheEnvironmentIsRetired()
    {
        // Arrange
        var sut = _faker.AsRetired().Generate();

        // Act
        var result = sut.Update("Production EU", 5);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A retired environment cannot be updated.");
    }

    #endregion Update
}
