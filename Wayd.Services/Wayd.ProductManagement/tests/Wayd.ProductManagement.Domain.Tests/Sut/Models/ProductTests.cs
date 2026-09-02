using FluentAssertions;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Domain.Models;
using Wayd.ProductManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.ProductManagement.Domain.Tests.Sut.Models;

public sealed class ProductTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly ProductFaker _faker;

    public ProductTests()
    {
        _dateTimeProvider = new(new FakeClock(DateTime.UtcNow.ToInstant()));
        _faker = new ProductFaker();
    }

    #region Create

    [Fact]
    public void Create_WhenValid_Success()
    {
        // Arrange
        var productTypeId = Guid.CreateVersion7();
        var initialStatus = StatusRefFactory.For(StatusCategory.Proposed);

        // Act
        var sut = Product.Create("Checkout", "The checkout product.", productTypeId, null, "checkout-web", initialStatus, EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.Name.Should().Be("Checkout");
        sut.Description.Should().Be("The checkout product.");
        sut.ProductTypeId.Should().Be(productTypeId);
        sut.ParentId.Should().BeNull();
        sut.ExternalId.Should().Be("checkout-web");
        sut.StatusId.Should().Be(initialStatus.StatusId);
        sut.StatusCategory.Should().Be(StatusCategory.Proposed);
    }

    [Fact]
    public void Create_ShouldRaiseProductAddedEvent_AfterPersistence()
    {
        // Arrange
        var productTypeId = Guid.CreateVersion7();
        var initialStatus = StatusRefFactory.For(StatusCategory.Proposed);

        // Act
        var sut = Product.Create("Checkout", null, productTypeId, null, null, initialStatus, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // The event is deferred because Key is database-generated; raising it eagerly would carry Key 0.
        sut.DomainEvents.Should().BeEmpty();
        sut.PostPersistenceActions.Should().ContainSingle();

        sut.PostPersistenceActions.First()();

        sut.DomainEvents.Should().ContainSingle(e => e is ProductAddedEvent);
        var added = sut.DomainEvents.OfType<ProductAddedEvent>().Single();
        added.Id.Should().Be(sut.Id);
        added.Name.Should().Be("Checkout");
        added.ProductTypeId.Should().Be(productTypeId);
        added.StatusId.Should().Be(initialStatus.StatusId);
        added.Actor.Should().Be(EventActor.System);
        added.Timestamp.Should().Be(_dateTimeProvider.Now);
    }

    [Fact]
    public void Create_WithNullName_Throws()
    {
        // Arrange
        string? name = null;

        // Act
        Action act = () => Product.Create(name!, null, Guid.CreateVersion7(), null, null, StatusRefFactory.For(StatusCategory.Proposed), EventActor.System, _dateTimeProvider.Now);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("Value cannot be null. (Parameter 'Name')");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidName_Throws(string name)
    {
        // Act
        Action act = () => Product.Create(name, null, Guid.CreateVersion7(), null, null, StatusRefFactory.For(StatusCategory.Proposed), EventActor.System, _dateTimeProvider.Now);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("Required input Name was empty. (Parameter 'Name')");
    }

    [Fact]
    public void Create_WithDefaultProductTypeId_Throws()
    {
        // Act
        Action act = () => Product.Create("Checkout", null, Guid.Empty, null, null, StatusRefFactory.For(StatusCategory.Proposed), EventActor.System, _dateTimeProvider.Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldTrimNameAndNormalizeBlankDescriptionToNull()
    {
        // Arrange & Act
        var sut = Product.Create("  Checkout  ", "   ", Guid.CreateVersion7(), null, "  ", StatusRefFactory.For(StatusCategory.Proposed), EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.Name.Should().Be("Checkout");
        sut.Description.Should().BeNull();
        sut.ExternalId.Should().BeNull();
    }

    #endregion Create

    #region UpdateDetails

    [Fact]
    public void UpdateDetails_ShouldUpdateDetailsAndRaiseEvent()
    {
        // Arrange
        var sut = _faker.Generate();

        // Act
        var result = sut.UpdateDetails("Checkout Web", "Rebranded.", EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Name.Should().Be("Checkout Web");
        sut.Description.Should().Be("Rebranded.");
        sut.DomainEvents.Should().ContainSingle(e => e is ProductDetailsUpdatedEvent);
    }

    [Fact]
    public void UpdateDetails_WithUnchangedValues_ShouldSucceedWithoutRaisingAnEvent()
    {
        // Arrange
        var sut = _faker.WithName("Checkout").WithDescription("The checkout product.").WithExternalId("checkout-web").Generate();

        // Act
        var result = sut.UpdateDetails("Checkout", "The checkout product.", EventActor.System, _dateTimeProvider.Now);

        // Assert
        // An event asserts something happened. Saving a form without editing it must not put a change
        // in the history, notify watchers, or cost a durable message.
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateDetails_WithOnlyWhitespaceDifferences_ShouldNotRaiseAnEvent()
    {
        // Arrange
        var sut = _faker.WithName("Checkout").WithDescription("The checkout product.").WithExternalId("checkout-web").Generate();

        // Act
        var result = sut.UpdateDetails("  Checkout  ", " The checkout product. ", EventActor.System, _dateTimeProvider.Now);

        // Assert
        // The setters trim, so the stored state would be identical — reporting a change the record does
        // not show would make the event lie.
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateDetails_ShouldRaiseAnEvent_WhenOnlyOneFieldChanges()
    {
        // Arrange
        var sut = _faker.WithName("Checkout").WithDescription("The checkout product.").WithExternalId("checkout-web").Generate();

        // Act
        var result = sut.UpdateDetails("Checkout", "Reworded.", EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().ContainSingle(e => e is ProductDetailsUpdatedEvent);
    }

    [Fact]
    public void UpdateDetails_ShouldRaiseAnEvent_WhenAValueIsCleared()
    {
        // Arrange
        var sut = _faker.WithName("Checkout").WithDescription("The checkout product.").WithExternalId("checkout-web").Generate();

        // Act
        var result = sut.UpdateDetails("Checkout", null, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Description.Should().BeNull();
        sut.DomainEvents.Should().ContainSingle(e => e is ProductDetailsUpdatedEvent);
    }

    [Fact]
    public void UpdateDetails_ShouldLeaveTheExternalLinkAlone()
    {
        // The facets were split so a rename cannot silently clear the link.
        // Arrange
        var sut = _faker.WithName("Checkout").WithExternalId("checkout-web").Generate();

        // Act
        var result = sut.UpdateDetails("Checkout Web", "Rebranded.", EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.ExternalId.Should().Be("checkout-web");
    }

    #endregion UpdateDetails

    #region LinkExternally

    [Fact]
    public void LinkExternally_ShouldSetTheLinkAndRaiseEvent()
    {
        // Arrange
        var sut = _faker.WithExternalId(null).Generate();

        // Act
        var result = sut.LinkExternally("acme/checkout-web", EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.ExternalId.Should().Be("acme/checkout-web");
        sut.DomainEvents.Should().ContainSingle(e => e is ProductLinkedExternallyEvent);
    }

    [Fact]
    public void LinkExternally_WithNull_ShouldClearTheLinkAndRaiseEvent()
    {
        // Unlinking is as much a change as linking: an integration that correlated on the old value
        // stops being able to.
        // Arrange
        var sut = _faker.WithExternalId("acme/checkout-web").Generate();

        // Act
        var result = sut.LinkExternally(null, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.ExternalId.Should().BeNull();
        sut.DomainEvents.Should().ContainSingle(e => e is ProductLinkedExternallyEvent);
    }

    [Fact]
    public void LinkExternally_WithTheSameValue_ShouldSucceedWithoutRaisingAnEvent()
    {
        // Arrange
        var sut = _faker.WithExternalId("acme/checkout-web").Generate();

        // Act
        var result = sut.LinkExternally("  acme/checkout-web  ", EventActor.System, _dateTimeProvider.Now);

        // Assert
        // The setter trims, so the stored state is identical and nothing happened.
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void LinkExternally_ShouldLeaveTheNameAndDescriptionAlone()
    {
        // Arrange
        var sut = _faker.WithName("Checkout").WithDescription("The checkout product.").Generate();

        // Act
        var result = sut.LinkExternally("acme/checkout-web", EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Name.Should().Be("Checkout");
        sut.Description.Should().Be("The checkout product.");
    }

    #endregion LinkExternally

    #region Reparent

    [Fact]
    public void Reparent_ShouldMoveNodeAndRaiseEventCarryingBothEnds()
    {
        // Arrange
        var oldParentId = Guid.CreateVersion7();
        var newParentId = Guid.CreateVersion7();
        var sut = _faker.WithParentId(oldParentId).Generate();

        // Act
        var result = sut.Reparent(newParentId, [], EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.ParentId.Should().Be(newParentId);

        var reparented = sut.DomainEvents.OfType<ProductReparentedEvent>().Single();
        reparented.FromParentId.Should().Be(oldParentId);
        reparented.ToParentId.Should().Be(newParentId);
    }

    [Fact]
    public void Reparent_ToRoot_ShouldSucceed()
    {
        // Arrange
        var sut = _faker.WithParentId(Guid.CreateVersion7()).Generate();

        // Act
        var result = sut.Reparent(null, [], EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.ParentId.Should().BeNull();
        sut.DomainEvents.OfType<ProductReparentedEvent>().Single().ToParentId.Should().BeNull();
    }

    [Fact]
    public void Reparent_ToItself_ShouldFail()
    {
        // Arrange
        var sut = _faker.Generate();

        // Act
        var result = sut.Reparent(sut.Id, [], EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A product cannot be its own parent.");
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Reparent_BeneathItsOwnDescendant_ShouldFail()
    {
        // Arrange
        var sut = _faker.Generate();
        var descendantId = Guid.CreateVersion7();

        // The target's ancestry runs back through this node, which is what makes the move a cycle.
        var ancestorsOfTarget = new[] { Guid.CreateVersion7(), sut.Id };

        // Act
        var result = sut.Reparent(descendantId, ancestorsOfTarget, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A product cannot be moved beneath one of its own descendants.");
    }

    [Fact]
    public void Reparent_ToTheSameParent_ShouldSucceedWithoutRaisingAnEvent()
    {
        // Arrange
        var parentId = Guid.CreateVersion7();
        var sut = _faker.WithParentId(parentId).Generate();

        // Act
        var result = sut.Reparent(parentId, [], EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion Reparent

    #region Retype

    [Fact]
    public void Retype_ShouldChangeTypeAndRaiseEvent()
    {
        // Arrange
        var sut = _faker.Generate();
        var toTypeId = Guid.CreateVersion7();

        // Act
        var result = sut.Retype(toTypeId, isTargetReleasable: true, hasVersions: false, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.ProductTypeId.Should().Be(toTypeId);
        sut.DomainEvents.Should().ContainSingle(e => e is ProductRetypedEvent);
    }

    [Fact]
    public void Retype_ToANonReleasableType_ShouldFail_WhenTheProductHasReleases()
    {
        // Arrange
        var sut = _faker.Generate();

        // Act
        var result = sut.Retype(Guid.CreateVersion7(), isTargetReleasable: false, hasVersions: true, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This product has versions and cannot be changed to a type that is not releasable.");
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Retype_ToANonReleasableType_ShouldSucceed_WhenTheProductHasNoReleases()
    {
        // Arrange
        var sut = _faker.Generate();

        // Act
        var result = sut.Retype(Guid.CreateVersion7(), isTargetReleasable: false, hasVersions: false, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion Retype

    #region ChangeStatus

    [Fact]
    public void ChangeStatus_ShouldMoveStatusAndKeepCategoryInStep()
    {
        // Arrange
        var sut = _faker.WithStatusCategory(StatusCategory.Active).Generate();
        var retired = StatusRefFactory.Retired();

        // Act
        var result = sut.ChangeStatus(retired, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.StatusId.Should().Be(retired.StatusId);
        sut.StatusCategory.Should().Be(StatusCategory.Done);
    }

    [Fact]
    public void ChangeStatus_ShouldCarryTheTargetAliasOnTheEvent()
    {
        // Arrange
        var sut = _faker.WithStatusCategory(StatusCategory.Active).Generate();
        var retired = StatusRefFactory.Retired();

        // Act
        sut.ChangeStatus(retired, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // A consumer branches on the alias, never on a status name an administrator can rename.
        var changed = sut.DomainEvents.OfType<ProductLifecycleChangedEvent>().Single();
        changed.ToAlias.Should().Be(ProductStatusAlias.Retired);
        changed.ToCategory.Should().Be(StatusCategory.Done);
        changed.FromCategory.Should().Be(StatusCategory.Active);
    }

    [Fact]
    public void ChangeStatus_ShouldReportTheAliasItMovedAwayFrom()
    {
        // Arrange
        var sut = _faker.WithStatusCategory(StatusCategory.Active).WithStatusAlias(ProductStatusAlias.Active).Generate();

        // Act
        sut.ChangeStatus(StatusRefFactory.Retired(), EventActor.System, _dateTimeProvider.Now);

        // Assert
        // FromAlias is a real payload field, so a consumer asking "did this leave Active?" gets an
        // answer rather than a constant None.
        var changed = sut.DomainEvents.OfType<ProductLifecycleChangedEvent>().Single();
        changed.FromAlias.Should().Be(ProductStatusAlias.Active);
        sut.StatusAlias.Should().Be(ProductStatusAlias.Retired);
    }

    [Fact]
    public void ChangeStatus_ToTheSameStatus_ShouldSucceedWithoutRaisingAnEvent()
    {
        // Arrange
        var current = StatusRefFactory.For(StatusCategory.Active, ProductStatusAlias.Active);
        var sut = _faker.WithStatusId(current.StatusId).WithStatusCategory(StatusCategory.Active).Generate();

        // Act
        var result = sut.ChangeStatus(current, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion ChangeStatus

    #region Remove

    [Fact]
    public void Remove_ShouldRaiseRemovedEvent()
    {
        // Arrange
        var parentId = Guid.CreateVersion7();
        var sut = _faker.WithParentId(parentId).Generate();

        // Act
        var result = sut.Remove(hasChildren: false, hasVersions: false, isInAManifest: false, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var removed = sut.DomainEvents.OfType<ProductRemovedEvent>().Single();
        removed.ParentId.Should().Be(parentId);
    }

    [Fact]
    public void Remove_ShouldFail_WhenTheNodeHasChildren()
    {
        // Arrange
        var sut = _faker.Generate();

        // Act
        var result = sut.Remove(hasChildren: true, hasVersions: false, isInAManifest: false, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This product has child products and cannot be removed. Move or remove them first.");
    }

    [Fact]
    public void Remove_ShouldFail_WhenTheNodeAppearsInAPackageManifest()
    {
        // Checked separately from releases: a carried-forward component often has no release row at
        // all, so the release guard misses it and the restricting foreign key rejects the delete with
        // an unreadable error.
        // Arrange
        var sut = _faker.Generate();

        // Act
        var result = sut.Remove(hasChildren: false, hasVersions: false, isInAManifest: true, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This product appears in a release package manifest and cannot be removed.");
    }

    [Fact]
    public void Remove_ShouldFail_WhenTheNodeHasReleases()
    {
        // Arrange
        var sut = _faker.Generate();

        // Act
        var result = sut.Remove(hasChildren: false, hasVersions: true, isInAManifest: false, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This product has versions and cannot be removed.");
    }

    #endregion Remove
}
