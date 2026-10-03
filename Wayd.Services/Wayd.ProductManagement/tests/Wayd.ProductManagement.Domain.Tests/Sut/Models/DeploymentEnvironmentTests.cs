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

    [Fact]
    public void Create_ThenRetiredBeforeTheFirstSave_RecordsTheEnvironmentAsAdded()
    {
        // Arrange — what the environment import does for an already-retired row: define the environment,
        // then retire it through the real transition, all before one save
        var sut = DeploymentEnvironment.Create("Production", EnvironmentCategory.Production, 4, EventActor.System, _dateTimeProvider.Now);

        // Act
        sut.Deactivate(EventActor.System, _dateTimeProvider.Now);
        sut.ExecutePostPersistenceActions();

        // Assert
        var added = sut.DomainEvents.OfType<EnvironmentAddedEvent>().Should().ContainSingle().Subject;
        added.Name.Should().Be("Production");
        added.Category.Should().Be(EnvironmentCategory.Production);
        added.RingOrder.Should().Be(4);
        sut.DomainEvents.Should().ContainSingle(e => e is EnvironmentRetiredEventV2);
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

        var reclassified = sut.DomainEvents.OfType<EnvironmentReclassifiedEventV2>().Single();
        reclassified.FromCategory.Should().Be(EnvironmentCategory.Staging);
        reclassified.ToCategory.Should().Be(EnvironmentCategory.Production);
    }

    [Fact]
    public void Reclassify_BeforePersistence_DefersEventUntilPostPersistence()
    {
        // Arrange
        var sut = DeploymentEnvironment.Create("QA", EnvironmentCategory.Testing, 1, EventActor.System, _dateTimeProvider.Now);

        // Act
        var result = sut.Reclassify(EnvironmentCategory.Staging, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();

        sut.ExecutePostPersistenceActions();

        sut.DomainEvents.Select(e => e.GetType()).Should().Equal(
            typeof(EnvironmentAddedEvent), typeof(EnvironmentReclassifiedEventV2));
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
        sut.DomainEvents.Should().ContainSingle(e => e is EnvironmentRetiredEventV2);
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

    [Fact]
    public void Deactivate_BeforePersistence_DefersEventUntilPostPersistence()
    {
        // Arrange
        var sut = DeploymentEnvironment.Create("QA2", EnvironmentCategory.Testing, 1, EventActor.System, _dateTimeProvider.Now);

        // Act
        var result = sut.Deactivate(EventActor.System, _dateTimeProvider.Now);

        // Assert — before persistence Key is 0, so the retirement event must be deferred to post-persistence
        result.IsSuccess.Should().BeTrue();
        sut.IsActive.Should().BeFalse();
        sut.DomainEvents.Should().BeEmpty();
        sut.PostPersistenceActions.Should().HaveCount(2);

        // When EF assigns the key on save and drains post-persistence actions:
        sut.ExecutePostPersistenceActions();

        var events = sut.DomainEvents.ToList();
        events.Should().HaveCount(2);
        events[0].Should().BeOfType<EnvironmentAddedEvent>();
        events[1].Should().BeOfType<EnvironmentRetiredEventV2>();
    }

    #endregion Deactivate

    #region Activate

    [Fact]
    public void Activate_ShouldReactivateAndRaiseEvent()
    {
        // Arrange
        var sut = _faker.AsRetired().Generate();

        // Act
        var result = sut.Activate(EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.IsActive.Should().BeTrue();

        var reinstated = sut.DomainEvents.OfType<EnvironmentReinstatedEvent>().Should().ContainSingle().Subject;
        reinstated.Id.Should().Be(sut.Id);
        reinstated.Key.Should().Be(sut.Key);
        reinstated.Timestamp.Should().Be(_dateTimeProvider.Now);
    }

    [Fact]
    public void Activate_ShouldFailWithoutRaising_WhenAlreadyActive()
    {
        // Arrange
        var sut = _faker.Generate();

        // Act
        var result = sut.Activate(EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This environment is already active.");
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Activate_BeforePersistence_DefersEventUntilPostPersistence()
    {
        // Arrange
        var sut = DeploymentEnvironment.Create("QA2", EnvironmentCategory.Testing, 1, EventActor.System, _dateTimeProvider.Now);
        sut.Deactivate(EventActor.System, _dateTimeProvider.Now);

        // Act
        var result = sut.Activate(EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();

        sut.ExecutePostPersistenceActions();

        sut.DomainEvents.Select(e => e.GetType()).Should().Equal(
            typeof(EnvironmentAddedEvent), typeof(EnvironmentRetiredEventV2), typeof(EnvironmentReinstatedEvent));
    }

    #endregion Activate

    #region Update

    [Fact]
    public void Update_ShouldRenameAndRepositionInTheRollout()
    {
        // Arrange
        var sut = _faker.WithRingOrder(2).Generate();

        // Act
        var result = sut.Update("Production EU", 5, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Name.Should().Be("Production EU");
        sut.RingOrder.Should().Be(5);
    }

    [Fact]
    public void Update_ShouldRaiseADetailsEventCarryingBothEnds()
    {
        // Arrange
        var sut = _faker.WithName("prod-eu").WithRingOrder(2).Generate();

        // Act
        sut.Update("Production EU", 5, EventActor.System, _dateTimeProvider.Now);

        // Assert
        var updated = sut.DomainEvents.OfType<EnvironmentDetailsUpdatedEvent>().Should().ContainSingle().Subject;
        updated.Id.Should().Be(sut.Id);
        updated.Key.Should().Be(sut.Key);
        updated.Name.Should().Be("Production EU");
        updated.RingOrder.Should().Be(5);
        updated.Previous.Should().Be(new EnvironmentDetails("prod-eu", 2));
    }

    [Fact]
    public void Update_WithOnlyARingOrderChange_ShouldRaise()
    {
        // Arrange
        var sut = _faker.WithName("Staging").WithRingOrder(2).Generate();

        // Act
        sut.Update("Staging", 3, EventActor.System, _dateTimeProvider.Now);

        // Assert
        var updated = sut.DomainEvents.OfType<EnvironmentDetailsUpdatedEvent>().Should().ContainSingle().Subject;
        updated.RingOrder.Should().Be(3);
        updated.Previous!.RingOrder.Should().Be(2);
    }

    [Fact]
    public void Update_WithNoChange_ShouldSucceedWithoutRaisingAnEvent()
    {
        // Arrange — the name differs only by whitespace the setter trims, so nothing actually changes
        var sut = _faker.WithName("Staging").WithRingOrder(2).Generate();

        // Act
        var result = sut.Update(" Staging ", 2, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Update_BeforePersistence_DefersEventUntilPostPersistence()
    {
        // Arrange
        var sut = DeploymentEnvironment.Create("QA", EnvironmentCategory.Testing, 1, EventActor.System, _dateTimeProvider.Now);

        // Act
        var result = sut.Update("QA2", 2, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();

        sut.ExecutePostPersistenceActions();

        var events = sut.DomainEvents.ToList();
        events.Should().HaveCount(2);
        events[0].Should().BeOfType<EnvironmentAddedEvent>();
        var updated = events[1].Should().BeOfType<EnvironmentDetailsUpdatedEvent>().Subject;
        updated.Name.Should().Be("QA2");
        updated.Previous.Should().Be(new EnvironmentDetails("QA", 1));
    }

    [Fact]
    public void Update_ShouldFail_WhenTheEnvironmentIsRetired()
    {
        // Arrange
        var sut = _faker.AsRetired().Generate();

        // Act
        var result = sut.Update("Production EU", 5, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A retired environment cannot be updated.");
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion Update

    #region Delete

    [Fact]
    public void Delete_ShouldRaiseEnvironmentDeletedEvent_EvenWhenRetired()
    {
        // Arrange
        var sut = _faker.AsRetired().Generate();
        sut.ClearDomainEvents();

        // Act
        sut.Delete(EventActor.System, _dateTimeProvider.Now);

        // Assert
        var deleted = sut.DomainEvents.OfType<EnvironmentDeletedEvent>().Should().ContainSingle().Subject;
        deleted.Id.Should().Be(sut.Id);
        deleted.Key.Should().Be(sut.Key);
        deleted.Name.Should().Be(sut.Name);
        deleted.Category.Should().Be(sut.Category);
    }

    #endregion Delete
}
