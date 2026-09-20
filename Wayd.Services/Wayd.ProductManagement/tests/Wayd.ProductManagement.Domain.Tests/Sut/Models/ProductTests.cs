using FluentAssertions;
using NodaTime;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Common.Models;
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

    [Fact]
    public void Create_ThenChangedBeforeTheFirstSave_RecordsTheProductAsCreated()
    {
        // Arrange — the product import mutates a new product in the same pass, so the added event must
        // record the product as created rather than as it stood at the save
        var productTypeId = Guid.CreateVersion7();
        var initialStatus = StatusRefFactory.For(StatusCategory.Proposed);
        var sut = Product.Create("Checkout", "The checkout product.", productTypeId, null, null, initialStatus, EventActor.System, _dateTimeProvider.Now);

        // Act
        sut.UpdateDetails("Payments", "The payments product.", EventActor.System, _dateTimeProvider.Now);
        sut.ChangeStatus(StatusRefFactory.For(StatusCategory.Active), EventActor.System, _dateTimeProvider.Now);
        sut.ExecutePostPersistenceActions();

        // Assert
        var added = sut.DomainEvents.OfType<ProductAddedEvent>().Should().ContainSingle().Subject;
        added.Name.Should().Be("Checkout", "each later change is its own event");
        added.Description.Should().Be("The checkout product.");
        added.StatusId.Should().Be(initialStatus.StatusId);
        added.StatusCategory.Should().Be(StatusCategory.Proposed);
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
        sut.DomainEvents.Should().ContainSingle(e => e is ProductLinkedExternallyEventV2);
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
        var linked = sut.DomainEvents.OfType<ProductLinkedExternallyEventV2>().Should().ContainSingle().Subject;
        linked.PreviousExternalId.Should().Be("acme/checkout-web");
        linked.ExternalId.Should().BeNull();
    }

    [Fact]
    public void LinkExternally_ToADifferentValue_ShouldCarryBothEnds()
    {
        // Arrange
        var sut = _faker.WithExternalId("acme/checkout-web").Generate();

        // Act
        var result = sut.LinkExternally("acme/checkout-app", EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var linked = sut.DomainEvents.OfType<ProductLinkedExternallyEventV2>().Should().ContainSingle().Subject;
        linked.PreviousExternalId.Should().Be("acme/checkout-web");
        linked.ExternalId.Should().Be("acme/checkout-app");
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
        var result = sut.Reparent(newParentId, [], false, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.ParentId.Should().Be(newParentId);

        var reparented = sut.DomainEvents.OfType<ProductReparentedEventV2>().Single();
        reparented.FromParentId.Should().Be(oldParentId);
        reparented.ToParentId.Should().Be(newParentId);
    }

    [Fact]
    public void Reparent_ShouldRelateTheEventToBothParents()
    {
        // Arrange
        var oldParentId = Guid.CreateVersion7();
        var newParentId = Guid.CreateVersion7();
        var sut = _faker.WithParentId(oldParentId).Generate();

        // Act
        sut.Reparent(newParentId, [], false, EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.DomainEvents.OfType<ProductReparentedEventV2>().Single().RelatedAggregates
            .Should().BeEquivalentTo([new AggregateReference("Product", oldParentId), new AggregateReference("Product", newParentId)]);
    }

    [Fact]
    public void Reparent_FromRoot_ShouldRelateTheEventToTheNewParentOnly()
    {
        // Arrange
        var newParentId = Guid.CreateVersion7();
        var sut = _faker.Generate();

        // Act
        sut.Reparent(newParentId, [], false, EventActor.System, _dateTimeProvider.Now);

        // Assert
        sut.DomainEvents.OfType<ProductReparentedEventV2>().Single().RelatedAggregates
            .Should().ContainSingle().Which.Should().Be(new AggregateReference("Product", newParentId));
    }

    [Fact]
    public void Reparent_ToRoot_ShouldSucceed()
    {
        // Arrange
        var sut = _faker.WithParentId(Guid.CreateVersion7()).Generate();

        // Act
        var result = sut.Reparent(null, [], false, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.ParentId.Should().BeNull();
        sut.DomainEvents.OfType<ProductReparentedEventV2>().Single().ToParentId.Should().BeNull();
    }

    [Fact]
    public void Reparent_ToItself_ShouldFail()
    {
        // Arrange
        var sut = _faker.Generate();

        // Act
        var result = sut.Reparent(sut.Id, [], false, EventActor.System, _dateTimeProvider.Now);

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
        var result = sut.Reparent(descendantId, ancestorsOfTarget, false, EventActor.System, _dateTimeProvider.Now);

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
        var result = sut.Reparent(parentId, [], false, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Reparent_AcrossAnOpenDependency_ShouldFail()
    {
        // Arrange
        var sut = _faker.Generate();
        var fromParentId = sut.ParentId;

        // Act
        var result = sut.Reparent(Guid.CreateVersion7(), [], true, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(
            "This move would place a product above or below a product it has an open dependency with. End that dependency first.");
        sut.ParentId.Should().Be(fromParentId);
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
        sut.DomainEvents.Should().ContainSingle(e => e is ProductRetypedEventV2);
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
        var changed = sut.DomainEvents.OfType<ProductLifecycleChangedEventV2>().Single();
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
        var changed = sut.DomainEvents.OfType<ProductLifecycleChangedEventV2>().Single();
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
        var result = sut.Remove(hasChildren: false, hasVersions: false, isInAManifest: false, hasDependencies: false, isDependedOn: false, EventActor.System, _dateTimeProvider.Now);

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
        var result = sut.Remove(hasChildren: true, hasVersions: false, isInAManifest: false, hasDependencies: false, isDependedOn: false, EventActor.System, _dateTimeProvider.Now);

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
        var result = sut.Remove(hasChildren: false, hasVersions: false, isInAManifest: true, hasDependencies: false, isDependedOn: false, EventActor.System, _dateTimeProvider.Now);

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
        var result = sut.Remove(hasChildren: false, hasVersions: true, isInAManifest: false, hasDependencies: false, isDependedOn: false, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This product has versions and cannot be removed.");
    }

    [Fact]
    public void Remove_ShouldFail_WhenTheNodeHasDependencies()
    {
        // Arrange
        var sut = _faker.Generate();

        // Act
        var result = sut.Remove(hasChildren: false, hasVersions: false, isInAManifest: false, hasDependencies: true, isDependedOn: false, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This product has dependencies on other products recorded and cannot be removed.");
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Remove_ShouldFail_WhenOtherNodesDependOnIt()
    {
        // Arrange
        var sut = _faker.Generate();

        // Act
        var result = sut.Remove(hasChildren: false, hasVersions: false, isInAManifest: false, hasDependencies: false, isDependedOn: true, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Other products have dependencies on this product recorded, so it cannot be removed.");
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion Remove

    #region Dependencies

    private static readonly LocalDate Today = new(2026, 6, 15);

    private const InteractionStyle Both = InteractionStyle.Synchronous | InteractionStyle.Asynchronous;

    [Fact]
    public void AddDependency_ShouldOpenALinkAndRaiseAnEventRelatedToTheProductDependedOn()
    {
        // Arrange
        var sut = _faker.Generate();
        var identityId = Guid.CreateVersion7();
        var startsOn = Today.PlusDays(-30);

        // Act
        var result = sut.AddDependency(identityId, DependencyStrength.Hard, null, " Validates SSO tokens ", startsOn, [], [], Today, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var dependency = sut.Dependencies.Should().ContainSingle().Subject;
        dependency.Should().BeSameAs(result.Value);
        dependency.ProductId.Should().Be(sut.Id);
        dependency.DependsOnProductId.Should().Be(identityId);
        dependency.Strength.Should().Be(DependencyStrength.Hard);
        dependency.Description.Should().Be("Validates SSO tokens");
        dependency.Period.Should().Be(new FlexibleDateRange(startsOn));
        dependency.IsOpen.Should().BeTrue();

        var added = sut.DomainEvents.OfType<ProductDependencyAddedEvent>().Single();
        added.Should().BeEquivalentTo(new
        {
            sut.Id,
            DependencyId = dependency.Id,
            DependsOnProductId = identityId,
            Strength = DependencyStrength.Hard,
            Description = "Validates SSO tokens",
        });
        added.Period.Should().Be(new FlexibleDateRange(startsOn));
        added.RelatedAggregates.Should().BeEquivalentTo([new AggregateReference("Product", identityId)]);
    }

    [Fact]
    public void AddDependency_OnItself_ShouldFail()
    {
        // Arrange
        var sut = _faker.Generate();

        // Act
        var result = sut.AddDependency(sut.Id, DependencyStrength.Hard, null, null, Today, [], [], Today, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A product cannot depend on itself.");
        sut.Dependencies.Should().BeEmpty();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AddDependency_OnAnAncestor_ShouldFail()
    {
        // Arrange
        var parentId = Guid.CreateVersion7();
        var sut = _faker.WithParentId(parentId).Generate();

        // Act
        var result = sut.AddDependency(parentId, DependencyStrength.Hard, null, null, Today, [sut.Id, parentId], [parentId], Today, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().StartWith("A product cannot depend on a product above or below it in the product tree.");
        sut.Dependencies.Should().BeEmpty();
    }

    [Fact]
    public void AddDependency_OnADescendant_ShouldFail()
    {
        // Arrange
        var sut = _faker.Generate();
        var childId = Guid.CreateVersion7();

        // Act
        var result = sut.AddDependency(childId, DependencyStrength.Soft, null, null, Today, [sut.Id], [childId, sut.Id], Today, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().StartWith("A product cannot depend on a product above or below it in the product tree.");
    }

    [Fact]
    public void AddDependency_StartingInTheFuture_ShouldFail()
    {
        // Arrange
        var sut = _faker.Generate();

        // Act
        var result = sut.AddDependency(Guid.CreateVersion7(), DependencyStrength.Hard, null, null, Today.PlusDays(1), [], [], Today, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A dependency cannot start in the future.");
    }

    [Fact]
    public void AddDependency_WhileOneOnTheSameProductIsOpen_ShouldFail()
    {
        // Arrange
        var sut = _faker.Generate();
        var identityId = Guid.CreateVersion7();
        sut.AddDependency(identityId, DependencyStrength.Hard, null, null, Today.PlusDays(-10), [], [], Today, EventActor.System, _dateTimeProvider.Now);
        sut.ClearDomainEvents();

        // Act
        var result = sut.AddDependency(identityId, DependencyStrength.Soft, null, null, Today, [], [], Today, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().StartWith("This product already depends on that product.");
        sut.Dependencies.Should().ContainSingle();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AddDependency_StartingOnTheLastDayAnEndedOneHeld_ShouldFail()
    {
        // Arrange — a period includes its end day, so starting on it shares that day
        var sut = _faker.Generate();
        var identityId = Guid.CreateVersion7();
        var endedOn = Today.PlusDays(-10);
        var first = sut.AddDependency(identityId, DependencyStrength.Hard, null, null, Today.PlusDays(-30), [], [], Today, EventActor.System, _dateTimeProvider.Now).Value;
        sut.EndDependency(first.Id, endedOn, Today, EventActor.System, _dateTimeProvider.Now);

        // Act
        var result = sut.AddDependency(identityId, DependencyStrength.Hard, null, null, endedOn, [], [], Today, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().StartWith("An earlier dependency on that product already covers part of this period.");
    }

    [Fact]
    public void AddDependency_BeforeAnEndedOneOnTheSameProduct_ShouldFail()
    {
        // Arrange — an open link from before the earlier one began would cover all of it
        var sut = _faker.Generate();
        var identityId = Guid.CreateVersion7();
        var first = sut.AddDependency(identityId, DependencyStrength.Hard, null, null, Today.PlusDays(-30), [], [], Today, EventActor.System, _dateTimeProvider.Now).Value;
        sut.EndDependency(first.Id, Today.PlusDays(-10), Today, EventActor.System, _dateTimeProvider.Now);

        // Act
        var result = sut.AddDependency(identityId, DependencyStrength.Hard, null, null, Today.PlusDays(-60), [], [], Today, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().StartWith("An earlier dependency on that product already covers part of this period.");
    }

    [Fact]
    public void AddDependency_TheDayAfterAnEndedOneOnTheSameProduct_ShouldSucceed()
    {
        // Arrange
        var sut = _faker.Generate();
        var identityId = Guid.CreateVersion7();
        var endedOn = Today.PlusDays(-10);
        var first = sut.AddDependency(identityId, DependencyStrength.Hard, null, null, Today.PlusDays(-30), [], [], Today, EventActor.System, _dateTimeProvider.Now).Value;
        sut.EndDependency(first.Id, endedOn, Today, EventActor.System, _dateTimeProvider.Now);

        // Act
        var result = sut.AddDependency(identityId, DependencyStrength.Hard, null, null, endedOn.PlusDays(1), [], [], Today, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Dependencies.Should().HaveCount(2);
    }

    [Fact]
    public void EndDependency_ShouldKeepTheLinkAndRaiseEndedEvent()
    {
        // Arrange
        var sut = _faker.Generate();
        var startsOn = Today.PlusDays(-5);
        var dependency = sut.AddDependency(Guid.CreateVersion7(), DependencyStrength.Soft, null, null, startsOn, [], [], Today, EventActor.System, _dateTimeProvider.Now).Value;
        sut.ClearDomainEvents();
        var endsOn = Today.PlusDays(-1);

        // Act
        var result = sut.EndDependency(dependency.Id, endsOn, Today, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Dependencies.Should().ContainSingle().Which.Period.Should().Be(new FlexibleDateRange(startsOn, endsOn));
        dependency.IsOpen.Should().BeFalse();

        var ended = sut.DomainEvents.OfType<ProductDependencyEndedEvent>().Single();
        ended.Should().BeEquivalentTo(new { DependencyId = dependency.Id, dependency.DependsOnProductId, Strength = DependencyStrength.Soft });
        ended.Period.Should().Be(new FlexibleDateRange(startsOn, endsOn));
        ended.RelatedAggregates.Should().BeEquivalentTo([new AggregateReference("Product", dependency.DependsOnProductId)]);
    }

    [Fact]
    public void EndDependency_OnTheDayItStarted_ShouldSucceed()
    {
        // Arrange — a dependency that held for one day still held
        var sut = _faker.Generate();
        var dependency = sut.AddDependency(Guid.CreateVersion7(), DependencyStrength.Hard, null, null, Today, [], [], Today, EventActor.System, _dateTimeProvider.Now).Value;

        // Act
        var result = sut.EndDependency(dependency.Id, Today, Today, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        dependency.Period.Days.Should().Be(1);
    }

    [Fact]
    public void EndDependency_ThatHasAlreadyEnded_ShouldFail()
    {
        // Arrange
        var sut = _faker.Generate();
        var dependency = sut.AddDependency(Guid.CreateVersion7(), DependencyStrength.Hard, null, null, Today.PlusDays(-5), [], [], Today, EventActor.System, _dateTimeProvider.Now).Value;
        sut.EndDependency(dependency.Id, Today.PlusDays(-1), Today, EventActor.System, _dateTimeProvider.Now);
        sut.ClearDomainEvents();

        // Act
        var result = sut.EndDependency(dependency.Id, Today, Today, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This dependency has already ended.");
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void EndDependency_BeforeItStarted_ShouldFail()
    {
        // Arrange
        var sut = _faker.Generate();
        var startsOn = Today.PlusDays(-5);
        var dependency = sut.AddDependency(Guid.CreateVersion7(), DependencyStrength.Hard, null, null, startsOn, [], [], Today, EventActor.System, _dateTimeProvider.Now).Value;

        // Act
        var result = sut.EndDependency(dependency.Id, startsOn.PlusDays(-1), Today, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A dependency cannot end before it started.");
        dependency.IsOpen.Should().BeTrue();
    }

    [Fact]
    public void EndDependency_InTheFuture_ShouldFail()
    {
        // Arrange
        var sut = _faker.Generate();
        var dependency = sut.AddDependency(Guid.CreateVersion7(), DependencyStrength.Hard, null, null, Today.PlusDays(-5), [], [], Today, EventActor.System, _dateTimeProvider.Now).Value;

        // Act
        var result = sut.EndDependency(dependency.Id, Today.PlusDays(1), Today, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A dependency cannot end in the future.");
    }

    [Fact]
    public void EndDependency_ThatDoesNotExist_ShouldFail()
    {
        // Arrange
        var sut = _faker.Generate();

        // Act
        var result = sut.EndDependency(Guid.CreateVersion7(), Today, Today, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Dependency not found.");
    }

    [Fact]
    public void ChangeDependencyTerms_ShouldEndTheLinkTheDayBeforeAndOpenAnotherKeepingTheDescription()
    {
        // Arrange
        var sut = _faker.Generate();
        var identityId = Guid.CreateVersion7();
        var startsOn = Today.PlusDays(-30);
        var original = sut.AddDependency(identityId, DependencyStrength.Soft, null, "Validates SSO tokens", startsOn, [], [], Today, EventActor.System, _dateTimeProvider.Now).Value;
        sut.ClearDomainEvents();
        var changedOn = Today.PlusDays(-2);

        // Act
        var result = sut.ChangeDependencyTerms(original.Id, DependencyStrength.Hard, null, changedOn, Today, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        original.Period.Should().Be(new FlexibleDateRange(startsOn, changedOn.PlusDays(-1)));
        original.Strength.Should().Be(DependencyStrength.Soft, "a link's strength never changes");

        var replacement = result.Value;
        replacement.Id.Should().NotBe(original.Id);
        replacement.Should().BeEquivalentTo(new { DependsOnProductId = identityId, Strength = DependencyStrength.Hard, Description = "Validates SSO tokens" });
        replacement.Period.Should().Be(new FlexibleDateRange(changedOn));
        sut.Dependencies.Should().HaveCount(2);

        // Two facts, in the order they happened
        sut.DomainEvents.Select(e => e.GetType()).Should().Equal(typeof(ProductDependencyEndedEvent), typeof(ProductDependencyAddedEvent));
    }

    [Fact]
    public void ChangeDependencyTerms_ToTheSameStrength_ShouldRaiseNothing()
    {
        // Arrange
        var sut = _faker.Generate();
        var dependency = sut.AddDependency(Guid.CreateVersion7(), DependencyStrength.Hard, null, null, Today.PlusDays(-5), [], [], Today, EventActor.System, _dateTimeProvider.Now).Value;
        sut.ClearDomainEvents();

        // Act
        var result = sut.ChangeDependencyTerms(dependency.Id, DependencyStrength.Hard, null, Today, Today, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dependency);
        dependency.IsOpen.Should().BeTrue();
        sut.Dependencies.Should().ContainSingle();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeDependencyTerms_OnAnEndedLink_ShouldFail()
    {
        // Arrange
        var sut = _faker.Generate();
        var dependency = sut.AddDependency(Guid.CreateVersion7(), DependencyStrength.Hard, null, null, Today.PlusDays(-5), [], [], Today, EventActor.System, _dateTimeProvider.Now).Value;
        sut.EndDependency(dependency.Id, Today.PlusDays(-1), Today, EventActor.System, _dateTimeProvider.Now);
        sut.ClearDomainEvents();

        // Act
        var result = sut.ChangeDependencyTerms(dependency.Id, DependencyStrength.Soft, null, Today, Today, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("An ended dependency cannot change terms.");
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeDependencyTerms_OnTheDayTheLinkStarted_ShouldFailWithoutEndingIt()
    {
        // Arrange — there is no earlier day to end the current link on
        var sut = _faker.Generate();
        var dependency = sut.AddDependency(Guid.CreateVersion7(), DependencyStrength.Hard, null, null, Today, [], [], Today, EventActor.System, _dateTimeProvider.Now).Value;
        sut.ClearDomainEvents();

        // Act
        var result = sut.ChangeDependencyTerms(dependency.Id, DependencyStrength.Soft, null, Today, Today, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().StartWith("A dependency's terms can change from the day after it started.");
        dependency.IsOpen.Should().BeTrue();
        sut.Dependencies.Should().ContainSingle();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeDependencyTerms_InTheFuture_ShouldFail()
    {
        // Arrange
        var sut = _faker.Generate();
        var dependency = sut.AddDependency(Guid.CreateVersion7(), DependencyStrength.Hard, null, null, Today.PlusDays(-5), [], [], Today, EventActor.System, _dateTimeProvider.Now).Value;

        // Act
        var result = sut.ChangeDependencyTerms(dependency.Id, DependencyStrength.Soft, null, Today.PlusDays(1), Today, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A dependency's terms cannot change in the future.");
        dependency.IsOpen.Should().BeTrue();
    }

    [Fact]
    public void UpdateDependencyDetails_ShouldRaiseEventCarryingBothDescriptions()
    {
        // Arrange
        var sut = _faker.Generate();
        var dependency = sut.AddDependency(Guid.CreateVersion7(), DependencyStrength.Hard, null, "Validates tokens", Today, [], [], Today, EventActor.System, _dateTimeProvider.Now).Value;
        sut.ClearDomainEvents();

        // Act
        var result = sut.UpdateDependencyDetails(dependency.Id, "Validates SSO tokens", null, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        dependency.Description.Should().Be("Validates SSO tokens");

        var updated = sut.DomainEvents.OfType<ProductDependencyDetailsUpdatedEvent>().Single();
        updated.Should().BeEquivalentTo(new { DependencyId = dependency.Id, dependency.DependsOnProductId, Description = "Validates SSO tokens", PreviousDescription = "Validates tokens" });
        updated.RelatedAggregates.Should().BeEquivalentTo([new AggregateReference("Product", dependency.DependsOnProductId)]);
    }

    [Fact]
    public void UpdateDependencyDetails_WhenOnlyWhitespaceDiffers_ShouldRaiseNothing()
    {
        // Arrange
        var sut = _faker.Generate();
        var dependency = sut.AddDependency(Guid.CreateVersion7(), DependencyStrength.Hard, null, "Validates SSO tokens", Today, [], [], Today, EventActor.System, _dateTimeProvider.Now).Value;
        sut.ClearDomainEvents();

        // Act
        var result = sut.UpdateDependencyDetails(dependency.Id, "Validates SSO tokens ", null, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateDependencyDetails_OnAnEndedLink_ShouldSucceed()
    {
        // Arrange
        var sut = _faker.Generate();
        var dependency = sut.AddDependency(Guid.CreateVersion7(), DependencyStrength.Hard, null, null, Today.PlusDays(-5), [], [], Today, EventActor.System, _dateTimeProvider.Now).Value;
        sut.EndDependency(dependency.Id, Today.PlusDays(-1), Today, EventActor.System, _dateTimeProvider.Now);

        // Act
        var result = sut.UpdateDependencyDetails(dependency.Id, "Reads shift schedules", null, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        dependency.Description.Should().Be("Reads shift schedules");
    }

    [Fact]
    public void RemoveDependency_ShouldDeleteTheLinkAndRecordItWithTheReason()
    {
        // Arrange
        var sut = _faker.Generate();
        var startsOn = Today.PlusDays(-5);
        var endsOn = Today.PlusDays(-1);
        var dependency = sut.AddDependency(Guid.CreateVersion7(), DependencyStrength.Soft, null, null, startsOn, [], [], Today, EventActor.System, _dateTimeProvider.Now).Value;
        sut.EndDependency(dependency.Id, endsOn, Today, EventActor.System, _dateTimeProvider.Now);
        sut.ClearDomainEvents();

        // Act
        var result = sut.RemoveDependency(dependency.Id, " Recorded against the wrong product ", EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Dependencies.Should().BeEmpty();

        var removed = sut.DomainEvents.OfType<ProductDependencyRemovedEvent>().Single();
        removed.Should().BeEquivalentTo(new
        {
            DependencyId = dependency.Id,
            dependency.DependsOnProductId,
            Strength = DependencyStrength.Soft,
            Reason = "Recorded against the wrong product",
        });
        removed.Period.Should().Be(new FlexibleDateRange(startsOn, endsOn));
        removed.RelatedAggregates.Should().BeEquivalentTo([new AggregateReference("Product", dependency.DependsOnProductId)]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RemoveDependency_WithoutAReason_ShouldFail(string reason)
    {
        // Arrange
        var sut = _faker.Generate();
        var dependency = sut.AddDependency(Guid.CreateVersion7(), DependencyStrength.Hard, null, null, Today, [], [], Today, EventActor.System, _dateTimeProvider.Now).Value;
        sut.ClearDomainEvents();

        // Act
        var result = sut.RemoveDependency(dependency.Id, reason, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A reason is required to remove a dependency.");
        sut.Dependencies.Should().ContainSingle();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AddDependency_WithInteractionStyles_ShouldRecordThemAndCarryThemOnTheEvent()
    {
        // Arrange
        var sut = _faker.Generate();
        var identityId = Guid.CreateVersion7();

        // Act
        var result = sut.AddDependency(identityId, DependencyStrength.Hard, Both, null, Today, [], [], Today, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.InteractionStyle.Should().Be(Both);

        var added = sut.DomainEvents.OfType<ProductDependencyAddedEvent>().Single();
        added.InteractionStyles.Should().Equal(InteractionStyle.Synchronous, InteractionStyle.Asynchronous);
    }

    [Fact]
    public void AddDependency_WithoutInteractionStyles_ShouldCarryNullOnTheEvent()
    {
        // Arrange
        var sut = _faker.Generate();

        // Act
        var result = sut.AddDependency(Guid.CreateVersion7(), DependencyStrength.Hard, null, null, Today, [], [], Today, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // Null rather than an empty collection: nothing recorded is not the same as recorded as nothing.
        result.Value.InteractionStyle.Should().BeNull();
        sut.DomainEvents.OfType<ProductDependencyAddedEvent>().Single().InteractionStyles.Should().BeNull();
    }

    [Fact]
    public void AddDependency_WithAnInteractionStyleNamingNothing_ShouldThrow()
    {
        // Arrange
        var sut = _faker.Generate();

        // Act
        Action act = () => sut.AddDependency(Guid.CreateVersion7(), DependencyStrength.Hard, default(InteractionStyle), null, Today, [], [], Today, EventActor.System, _dateTimeProvider.Now);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ChangeDependencyTerms_ChangingOnlyTheStyles_ShouldEndTheLinkAndOpenAnother()
    {
        // Arrange
        var sut = _faker.Generate();
        var identityId = Guid.CreateVersion7();
        var startsOn = Today.PlusDays(-30);
        var original = sut.AddDependency(identityId, DependencyStrength.Hard, InteractionStyle.Synchronous, "Validates SSO tokens", startsOn, [], [], Today, EventActor.System, _dateTimeProvider.Now).Value;
        sut.ClearDomainEvents();
        var changedOn = Today.PlusDays(-2);

        // Act — the team moved off the call and onto events; downtime before that day is still synchronous
        var result = sut.ChangeDependencyTerms(original.Id, DependencyStrength.Hard, InteractionStyle.Asynchronous, changedOn, Today, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        original.Period.Should().Be(new FlexibleDateRange(startsOn, changedOn.PlusDays(-1)));
        original.InteractionStyle.Should().Be(InteractionStyle.Synchronous, "a link's styles never change");

        var replacement = result.Value;
        replacement.Id.Should().NotBe(original.Id);
        replacement.InteractionStyle.Should().Be(InteractionStyle.Asynchronous);
        replacement.Description.Should().Be("Validates SSO tokens");
        replacement.Period.Should().Be(new FlexibleDateRange(changedOn));

        sut.DomainEvents.Select(e => e.GetType()).Should().Equal(typeof(ProductDependencyEndedEvent), typeof(ProductDependencyAddedEvent));
    }

    [Fact]
    public void ChangeDependencyTerms_RecordingStylesOnALinkThatHadNone_ShouldFillThemInPlace()
    {
        // Arrange
        var sut = _faker.Generate();
        var startsOn = Today.PlusDays(-30);
        var dependency = sut.AddDependency(Guid.CreateVersion7(), DependencyStrength.Hard, null, null, startsOn, [], [], Today, EventActor.System, _dateTimeProvider.Now).Value;
        sut.ClearDomainEvents();

        // Act
        var result = sut.ChangeDependencyTerms(dependency.Id, DependencyStrength.Hard, Both, Today.PlusDays(-2), Today, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // Nothing about the dependency changed — somebody wrote down how it had always worked — so the
        // period is not split on a day nothing happened.
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dependency);
        dependency.InteractionStyle.Should().Be(Both);
        dependency.Period.Should().Be(new FlexibleDateRange(startsOn));
        sut.Dependencies.Should().ContainSingle();

        var updated = sut.DomainEvents.OfType<ProductDependencyDetailsUpdatedEvent>().Single();
        updated.InteractionStyles.Should().Equal(InteractionStyle.Synchronous, InteractionStyle.Asynchronous);
        updated.PreviousInteractionStyles.Should().BeNull();
    }

    [Fact]
    public void ChangeDependencyTerms_WithNoStylesGiven_ShouldCarryTheRecordedOnesOntoTheNewLink()
    {
        // Arrange
        var sut = _faker.Generate();
        var dependency = sut.AddDependency(Guid.CreateVersion7(), DependencyStrength.Soft, InteractionStyle.Synchronous, null, Today.PlusDays(-30), [], [], Today, EventActor.System, _dateTimeProvider.Now).Value;

        // Act — a strength change says nothing about the styles, so they carry over rather than being lost
        var result = sut.ChangeDependencyTerms(dependency.Id, DependencyStrength.Hard, null, Today.PlusDays(-2), Today, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Strength.Should().Be(DependencyStrength.Hard);
        result.Value.InteractionStyle.Should().Be(InteractionStyle.Synchronous);
    }

    [Fact]
    public void ChangeDependencyTerms_WithNothingDifferent_ShouldRaiseNothing()
    {
        // Arrange
        var sut = _faker.Generate();
        var dependency = sut.AddDependency(Guid.CreateVersion7(), DependencyStrength.Hard, Both, null, Today.PlusDays(-5), [], [], Today, EventActor.System, _dateTimeProvider.Now).Value;
        sut.ClearDomainEvents();

        // Act
        var result = sut.ChangeDependencyTerms(dependency.Id, DependencyStrength.Hard, Both, Today, Today, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dependency);
        sut.Dependencies.Should().ContainSingle();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateDependencyDetails_RecordingStylesOnAnEndedLink_ShouldSucceed()
    {
        // Arrange — a link that has ended can still have how it worked written down
        var sut = _faker.Generate();
        var dependency = sut.AddDependency(Guid.CreateVersion7(), DependencyStrength.Hard, null, "Validates tokens", Today.PlusDays(-5), [], [], Today, EventActor.System, _dateTimeProvider.Now).Value;
        sut.EndDependency(dependency.Id, Today.PlusDays(-1), Today, EventActor.System, _dateTimeProvider.Now);
        sut.ClearDomainEvents();

        // Act
        var result = sut.UpdateDependencyDetails(dependency.Id, "Validates tokens", InteractionStyle.Synchronous, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        dependency.InteractionStyle.Should().Be(InteractionStyle.Synchronous);

        var updated = sut.DomainEvents.OfType<ProductDependencyDetailsUpdatedEvent>().Single();
        updated.InteractionStyles.Should().Equal(InteractionStyle.Synchronous);
        updated.PreviousInteractionStyles.Should().BeNull();
        updated.Description.Should().Be("Validates tokens");
        updated.PreviousDescription.Should().Be("Validates tokens");
    }

    [Fact]
    public void UpdateDependencyDetails_ChangingStylesAlreadyRecorded_ShouldFail()
    {
        // Arrange
        var sut = _faker.Generate();
        var dependency = sut.AddDependency(Guid.CreateVersion7(), DependencyStrength.Hard, InteractionStyle.Synchronous, null, Today.PlusDays(-5), [], [], Today, EventActor.System, _dateTimeProvider.Now).Value;
        sut.ClearDomainEvents();

        // Act
        var result = sut.UpdateDependencyDetails(dependency.Id, null, InteractionStyle.Asynchronous, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // Moving off a call and onto events is a change of terms, which has to be dated.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().StartWith("This dependency's interaction styles have already been recorded.");
        dependency.InteractionStyle.Should().Be(InteractionStyle.Synchronous);
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateDependencyDetails_WithNoStylesGiven_ShouldLeaveRecordedOnesAlone()
    {
        // Arrange
        var sut = _faker.Generate();
        var dependency = sut.AddDependency(Guid.CreateVersion7(), DependencyStrength.Hard, Both, "Validates tokens", Today, [], [], Today, EventActor.System, _dateTimeProvider.Now).Value;
        sut.ClearDomainEvents();

        // Act
        var result = sut.UpdateDependencyDetails(dependency.Id, null, null, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // A null description clears one; null styles do not clear those. A caller that simply omitted the
        // field would otherwise erase what downtime attribution reads.
        result.IsSuccess.Should().BeTrue();
        dependency.Description.Should().BeNull();
        dependency.InteractionStyle.Should().Be(Both);
    }

    #endregion Dependencies
}
