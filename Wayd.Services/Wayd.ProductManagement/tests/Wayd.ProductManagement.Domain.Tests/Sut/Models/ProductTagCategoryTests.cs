using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.ProductManagement.Domain.Models;
using Wayd.ProductManagement.Domain.Tests.Data;

namespace Wayd.ProductManagement.Domain.Tests.Sut.Models;

public sealed class ProductTagCategoryTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 27, 9, 0, 0);

    #region Create

    [Fact]
    public void Create_WhenValid_IsActiveOrganizationAxisWithNoTags()
    {
        // Act
        var sut = ProductTagCategory.Create(" Platform ", "  ", allowsMany: true, 3, EventActor.System, Now);

        // Assert
        sut.Name.Should().Be("Platform");
        sut.Description.Should().BeNull();
        sut.AllowsMany.Should().BeTrue();
        sut.Order.Should().Be(3);
        sut.IsActive.Should().BeTrue();
        sut.IsSystem.Should().BeFalse();
        sut.Tags.Should().BeEmpty();
    }

    [Fact]
    public void Create_WhenValid_RaisesCreatedWithTheNormalisedValues()
    {
        // Act
        var sut = ProductTagCategory.Create(" Platform ", " What it runs on ", allowsMany: false, 2, EventActor.System, Now);

        // Assert
        var created = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductTagCategoryCreatedEvent>().Subject;
        created.Id.Should().Be(sut.Id);
        created.Name.Should().Be("Platform");
        created.Description.Should().Be("What it runs on");
        created.AllowsMany.Should().BeFalse();
        created.Order.Should().Be(2);
        created.Timestamp.Should().Be(Now);
    }

    #endregion Create

    #region UpdateDetails

    [Fact]
    public void UpdateDetails_WhenChanged_RaisesBothEnds()
    {
        // Arrange
        var sut = new ProductTagCategoryFaker().WithName("Platform").WithDescription("Old").Generate();

        // Act
        var result = sut.UpdateDetails(" Target platform ", null, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Name.Should().Be("Target platform");
        var updated = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductTagCategoryDetailsUpdatedEvent>().Subject;
        updated.Name.Should().Be("Target platform");
        updated.Description.Should().BeNull();
        updated.PreviousName.Should().Be("Platform");
        updated.PreviousDescription.Should().Be("Old");
    }

    [Fact]
    public void UpdateDetails_WhenOnlyWhitespaceDiffers_RaisesNothing()
    {
        // Arrange
        var sut = new ProductTagCategoryFaker().WithName("Platform").WithDescription(null).Generate();

        // Act
        var result = sut.UpdateDetails(" Platform ", " ", EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateDetails_OnASystemAxis_Fails()
    {
        // Arrange
        var sut = new ProductTagCategoryFaker().WithName("Platform").WithIsSystem(true).Generate();

        // Act
        var result = sut.UpdateDetails("Renamed", null, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductTagCategory.SystemAxisError);
        sut.Name.Should().Be("Platform");
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion UpdateDetails

    #region Reorder and activation

    [Fact]
    public void Reorder_WhenMoved_RaisesBothPositions()
    {
        // Arrange
        var sut = new ProductTagCategoryFaker().WithOrder(1).WithIsSystem(true).Generate();

        // Act
        var result = sut.Reorder(4, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Order.Should().Be(4);
        var reordered = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductTagCategoryReorderedEvent>().Subject;
        reordered.FromOrder.Should().Be(1);
        reordered.ToOrder.Should().Be(4);
    }

    [Fact]
    public void Reorder_ToTheSamePosition_RaisesNothing()
    {
        // Arrange
        var sut = new ProductTagCategoryFaker().WithOrder(2).Generate();

        // Act
        sut.Reorder(2, EventActor.System, Now);

        // Assert
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Deactivate_WhenActive_DeactivatesAndRaises()
    {
        // Arrange
        var sut = new ProductTagCategoryFaker().WithIsSystem(true).Generate();

        // Act
        var result = sut.Deactivate(EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.IsActive.Should().BeFalse();
        sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductTagCategoryDeactivatedEvent>();
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_RaisesNothing()
    {
        // Arrange
        var sut = new ProductTagCategoryFaker().WithIsActive(false).Generate();

        // Act
        var result = sut.Deactivate(EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Activate_WhenInactive_ActivatesAndRaises()
    {
        // Arrange
        var sut = new ProductTagCategoryFaker().WithIsActive(false).Generate();

        // Act
        var result = sut.Activate(EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.IsActive.Should().BeTrue();
        sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductTagCategoryActivatedEvent>();
    }

    #endregion Reorder and activation

    #region Delete

    [Fact]
    public void Delete_WhenNotInUse_RaisesDeleted()
    {
        // Arrange
        var sut = new ProductTagCategoryFaker().WithName("Platform").Generate();

        // Act
        var result = sut.Delete(isInUse: false, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var deleted = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductTagCategoryDeletedEvent>().Subject;
        deleted.Name.Should().Be("Platform");
    }

    [Fact]
    public void Delete_WhenInUse_Fails()
    {
        // Arrange
        var sut = new ProductTagCategoryFaker().Generate();

        // Act
        var result = sut.Delete(isInUse: true, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductTagCategory.AxisInUseError);
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Delete_OnASystemAxis_Fails()
    {
        // Arrange
        var sut = new ProductTagCategoryFaker().WithIsSystem(true).Generate();

        // Act
        var result = sut.Delete(isInUse: false, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductTagCategory.SystemAxisError);
    }

    #endregion Delete

    #region AddTag

    [Fact]
    public void AddTag_WhenValid_AddsAnActiveTagAndRaises()
    {
        // Arrange
        var sut = new ProductTagCategoryFaker().Generate();

        // Act
        var result = sut.AddTag(" ios ", " Apple mobile ", EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var tag = sut.Tags.Should().ContainSingle().Subject;
        tag.Should().BeSameAs(result.Value);
        tag.Name.Should().Be("ios");
        tag.Description.Should().Be("Apple mobile");
        tag.CategoryId.Should().Be(sut.Id);
        tag.IsActive.Should().BeTrue();
        var added = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductTagAddedEvent>().Subject;
        added.Id.Should().Be(sut.Id);
        added.TagId.Should().Be(tag.Id);
        added.Name.Should().Be("ios");
    }

    [Fact]
    public void AddTag_WithANameAlreadyOnTheAxisInAnotherCase_Fails()
    {
        // Arrange
        var sut = new ProductTagCategoryFaker().WithTags("ios").Generate();

        // Act
        var result = sut.AddTag("IOS", null, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductTagCategory.DuplicateTagNameError);
        sut.Tags.Should().ContainSingle();
    }

    [Fact]
    public void AddTag_OnASystemAxis_Fails()
    {
        // Arrange
        var sut = new ProductTagCategoryFaker().WithIsSystem(true).Generate();

        // Act
        var result = sut.AddTag("ios", null, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductTagCategory.SystemAxisError);
        sut.Tags.Should().BeEmpty();
    }

    #endregion AddTag

    #region UpdateTagDetails

    [Fact]
    public void UpdateTagDetails_WhenChanged_RaisesBothEnds()
    {
        // Arrange
        var sut = new ProductTagCategoryFaker().WithTags("ios").Generate();
        var tag = sut.Tags.Single();

        // Act
        var result = sut.UpdateTagDetails(tag.Id, " iOS and iPadOS ", "Apple mobile", EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        tag.Name.Should().Be("iOS and iPadOS");
        var updated = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductTagDetailsUpdatedEvent>().Subject;
        updated.TagId.Should().Be(tag.Id);
        updated.Name.Should().Be("iOS and iPadOS");
        updated.Description.Should().Be("Apple mobile");
        updated.PreviousName.Should().Be("ios");
        updated.PreviousDescription.Should().BeNull();
    }

    [Fact]
    public void UpdateTagDetails_ToItsOwnNameInAnotherCase_Succeeds()
    {
        // Arrange
        var sut = new ProductTagCategoryFaker().WithTags("ios", "android").Generate();
        var tag = sut.Tags.Single(t => t.Name == "ios");

        // Act
        var result = sut.UpdateTagDetails(tag.Id, "IOS", null, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        tag.Name.Should().Be("IOS");
    }

    [Fact]
    public void UpdateTagDetails_ToAnotherTagsName_Fails()
    {
        // Arrange
        var sut = new ProductTagCategoryFaker().WithTags("ios", "android").Generate();
        var tag = sut.Tags.Single(t => t.Name == "ios");

        // Act
        var result = sut.UpdateTagDetails(tag.Id, "ANDROID", null, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductTagCategory.DuplicateTagNameError);
        tag.Name.Should().Be("ios");
    }

    [Fact]
    public void UpdateTagDetails_ForATagOnAnotherAxis_Fails()
    {
        // Arrange
        var sut = new ProductTagCategoryFaker().WithTags("ios").Generate();

        // Act
        var result = sut.UpdateTagDetails(Guid.NewGuid(), "Anything", null, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductTagCategory.TagNotFoundError);
    }

    #endregion UpdateTagDetails

    #region Tag activation and removal

    [Fact]
    public void DeactivateTag_WhenActive_DeactivatesAndRaises()
    {
        // Arrange
        var sut = new ProductTagCategoryFaker().WithTags("ios").Generate();
        var tag = sut.Tags.Single();

        // Act
        var result = sut.DeactivateTag(tag.Id, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        tag.IsActive.Should().BeFalse();
        sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductTagDeactivatedEvent>()
            .Which.TagId.Should().Be(tag.Id);
    }

    [Fact]
    public void ActivateTag_WhenInactive_ActivatesAndRaises()
    {
        // Arrange
        var sut = new ProductTagCategoryFaker().WithTags("ios").Generate();
        var tag = sut.Tags.Single();
        sut.DeactivateTag(tag.Id, EventActor.System, Now);
        sut.ClearDomainEvents();

        // Act
        var result = sut.ActivateTag(tag.Id, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        tag.IsActive.Should().BeTrue();
        sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductTagActivatedEvent>();
    }

    [Fact]
    public void DeactivateTag_OnASystemAxis_Fails()
    {
        // Arrange
        var sut = new ProductTagCategoryFaker().WithTags("ios").WithIsSystem(true).Generate();
        var tag = sut.Tags.Single();

        // Act
        var result = sut.DeactivateTag(tag.Id, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        tag.IsActive.Should().BeTrue();
    }

    [Fact]
    public void RemoveTag_WhenNotInUse_RemovesAndRaisesWithItsName()
    {
        // Arrange
        var sut = new ProductTagCategoryFaker().WithTags("ios").Generate();
        var tag = sut.Tags.Single();

        // Act
        var result = sut.RemoveTag(tag.Id, isInUse: false, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Tags.Should().BeEmpty();
        var removed = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductTagRemovedEvent>().Subject;
        removed.TagId.Should().Be(tag.Id);
        removed.Name.Should().Be("ios");
    }

    [Fact]
    public void RemoveTag_WhenInUse_FailsAndKeepsIt()
    {
        // Arrange
        var sut = new ProductTagCategoryFaker().WithTags("ios").Generate();
        var tag = sut.Tags.Single();

        // Act
        var result = sut.RemoveTag(tag.Id, isInUse: true, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductTagCategory.TagInUseError);
        sut.Tags.Should().ContainSingle();
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion Tag activation and removal

    #region System axes

    [Fact]
    public void CreateSystem_WhenValid_IsASystemAxisAndRaisesCreated()
    {
        // Act
        var sut = ProductTagCategory.CreateSystem("Platform", null, allowsMany: true, 1, EventActor.System, Now);

        // Assert
        sut.IsSystem.Should().BeTrue();
        sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductTagCategoryCreatedEvent>();
    }

    [Fact]
    public void AddSystemTag_OnASystemAxis_AddsAndRaises()
    {
        // Arrange
        var sut = new ProductTagCategoryFaker().WithIsSystem(true).Generate();

        // Act
        var result = sut.AddSystemTag("ios", "Apple mobile.", EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Tags.Should().ContainSingle().Which.Name.Should().Be("ios");
        sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductTagAddedEvent>();
    }

    [Fact]
    public void UpdateTagDetails_OnASystemAxis_Fails()
    {
        // Arrange
        var sut = new ProductTagCategoryFaker().WithTags("ios").WithIsSystem(true).Generate();
        var tag = sut.Tags.Single();

        // Act
        var result = sut.UpdateTagDetails(tag.Id, "iOS", null, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductTagCategory.SystemAxisError);
        tag.Name.Should().Be("ios");
    }

    [Fact]
    public void RemoveTag_OnASystemAxis_FailsAndKeepsIt()
    {
        // Arrange
        var sut = new ProductTagCategoryFaker().WithTags("ios").WithIsSystem(true).Generate();
        var tag = sut.Tags.Single();

        // Act
        var result = sut.RemoveTag(tag.Id, isInUse: false, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductTagCategory.SystemAxisError);
        sut.Tags.Should().ContainSingle();
    }

    #endregion System axes
}
