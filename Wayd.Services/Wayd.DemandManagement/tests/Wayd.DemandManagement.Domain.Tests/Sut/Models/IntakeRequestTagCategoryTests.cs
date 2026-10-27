using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Domain.Tests.Sut.Models;

public sealed class IntakeRequestTagCategoryTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 27, 9, 0, 0);

    #region Create

    [Fact]
    public void Create_WhenValid_IsActiveCompanyAxisWithNoTags()
    {
        // Act
        var sut = IntakeRequestTagCategory.Create(" Area ", "  ", allowsMany: true, 3, EventActor.System, Now);

        // Assert
        sut.Name.Should().Be("Area");
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
        var sut = IntakeRequestTagCategory.Create(" Area ", " Where it lands ", allowsMany: false, 2, EventActor.System, Now);

        // Assert
        var created = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestTagCategoryCreatedEvent>().Subject;
        created.Id.Should().Be(sut.Id);
        created.Name.Should().Be("Area");
        created.Description.Should().Be("Where it lands");
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
        var sut = new IntakeRequestTagCategoryFaker().WithName("Area").WithDescription("Old").Generate();

        // Act
        var result = sut.UpdateDetails(" Business area ", null, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Name.Should().Be("Business area");
        var updated = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestTagCategoryDetailsUpdatedEvent>().Subject;
        updated.Name.Should().Be("Business area");
        updated.Description.Should().BeNull();
        updated.PreviousName.Should().Be("Area");
        updated.PreviousDescription.Should().Be("Old");
    }

    [Fact]
    public void UpdateDetails_WhenOnlyWhitespaceDiffers_RaisesNothing()
    {
        // Arrange
        var sut = new IntakeRequestTagCategoryFaker().WithName("Area").WithDescription(null).Generate();

        // Act
        var result = sut.UpdateDetails(" Area ", " ", EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateDetails_OnASystemAxis_Fails()
    {
        // Arrange
        var sut = new IntakeRequestTagCategoryFaker().WithName("Area").WithIsSystem(true).Generate();

        // Act
        var result = sut.UpdateDetails("Renamed", null, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestTagCategory.SystemAxisError);
        sut.Name.Should().Be("Area");
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion UpdateDetails

    #region Reorder and activation

    [Fact]
    public void Reorder_WhenMoved_RaisesBothPositions()
    {
        // Arrange
        var sut = new IntakeRequestTagCategoryFaker().WithOrder(1).WithIsSystem(true).Generate();

        // Act
        var result = sut.Reorder(4, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Order.Should().Be(4);
        var reordered = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestTagCategoryReorderedEvent>().Subject;
        reordered.FromOrder.Should().Be(1);
        reordered.ToOrder.Should().Be(4);
    }

    [Fact]
    public void Reorder_ToTheSamePosition_RaisesNothing()
    {
        // Arrange
        var sut = new IntakeRequestTagCategoryFaker().WithOrder(2).Generate();

        // Act
        sut.Reorder(2, EventActor.System, Now);

        // Assert
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Deactivate_WhenActive_DeactivatesAndRaises()
    {
        // Arrange
        var sut = new IntakeRequestTagCategoryFaker().WithIsSystem(true).Generate();

        // Act
        var result = sut.Deactivate(EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.IsActive.Should().BeFalse();
        sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestTagCategoryDeactivatedEvent>();
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_RaisesNothing()
    {
        // Arrange
        var sut = new IntakeRequestTagCategoryFaker().WithIsActive(false).Generate();

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
        var sut = new IntakeRequestTagCategoryFaker().WithIsActive(false).Generate();

        // Act
        var result = sut.Activate(EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.IsActive.Should().BeTrue();
        sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestTagCategoryActivatedEvent>();
    }

    #endregion Reorder and activation

    #region Delete

    [Fact]
    public void Delete_WhenNotInUse_RaisesDeleted()
    {
        // Arrange
        var sut = new IntakeRequestTagCategoryFaker().WithName("Area").Generate();

        // Act
        var result = sut.Delete(isInUse: false, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var deleted = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestTagCategoryDeletedEvent>().Subject;
        deleted.Name.Should().Be("Area");
    }

    [Fact]
    public void Delete_WhenInUse_Fails()
    {
        // Arrange
        var sut = new IntakeRequestTagCategoryFaker().Generate();

        // Act
        var result = sut.Delete(isInUse: true, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestTagCategory.AxisInUseError);
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Delete_OnASystemAxis_Fails()
    {
        // Arrange
        var sut = new IntakeRequestTagCategoryFaker().WithIsSystem(true).Generate();

        // Act
        var result = sut.Delete(isInUse: false, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestTagCategory.SystemAxisError);
    }

    #endregion Delete

    #region AddTag

    [Fact]
    public void AddTag_WhenValid_AddsAnActiveTagAndRaises()
    {
        // Arrange
        var sut = new IntakeRequestTagCategoryFaker().Generate();

        // Act
        var result = sut.AddTag(" Payments ", " Card and wallet flows ", EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var tag = sut.Tags.Should().ContainSingle().Subject;
        tag.Should().BeSameAs(result.Value);
        tag.Name.Should().Be("Payments");
        tag.Description.Should().Be("Card and wallet flows");
        tag.CategoryId.Should().Be(sut.Id);
        tag.IsActive.Should().BeTrue();
        var added = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestTagAddedEvent>().Subject;
        added.Id.Should().Be(sut.Id);
        added.TagId.Should().Be(tag.Id);
        added.Name.Should().Be("Payments");
    }

    [Fact]
    public void AddTag_WithANameAlreadyOnTheAxisInAnotherCase_Fails()
    {
        // Arrange
        var sut = new IntakeRequestTagCategoryFaker().WithTags("Payments").Generate();

        // Act
        var result = sut.AddTag("PAYMENTS", null, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestTagCategory.DuplicateTagNameError);
        sut.Tags.Should().ContainSingle();
    }

    [Fact]
    public void AddTag_OnASystemAxis_Fails()
    {
        // Arrange
        var sut = new IntakeRequestTagCategoryFaker().WithIsSystem(true).Generate();

        // Act
        var result = sut.AddTag("Payments", null, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestTagCategory.SystemAxisError);
        sut.Tags.Should().BeEmpty();
    }

    #endregion AddTag

    #region UpdateTagDetails

    [Fact]
    public void UpdateTagDetails_WhenChanged_RaisesBothEnds()
    {
        // Arrange
        var sut = new IntakeRequestTagCategoryFaker().WithTags("Payments").Generate();
        var tag = sut.Tags.Single();

        // Act
        var result = sut.UpdateTagDetails(tag.Id, " Payments and billing ", "Money in", EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        tag.Name.Should().Be("Payments and billing");
        var updated = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestTagDetailsUpdatedEvent>().Subject;
        updated.TagId.Should().Be(tag.Id);
        updated.Name.Should().Be("Payments and billing");
        updated.Description.Should().Be("Money in");
        updated.PreviousName.Should().Be("Payments");
        updated.PreviousDescription.Should().BeNull();
    }

    [Fact]
    public void UpdateTagDetails_ToItsOwnNameInAnotherCase_Succeeds()
    {
        // Arrange
        var sut = new IntakeRequestTagCategoryFaker().WithTags("Payments", "Billing").Generate();
        var tag = sut.Tags.Single(t => t.Name == "Payments");

        // Act
        var result = sut.UpdateTagDetails(tag.Id, "payments", null, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        tag.Name.Should().Be("payments");
    }

    [Fact]
    public void UpdateTagDetails_ToAnotherTagsName_Fails()
    {
        // Arrange
        var sut = new IntakeRequestTagCategoryFaker().WithTags("Payments", "Billing").Generate();
        var tag = sut.Tags.Single(t => t.Name == "Payments");

        // Act
        var result = sut.UpdateTagDetails(tag.Id, "billing", null, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestTagCategory.DuplicateTagNameError);
        tag.Name.Should().Be("Payments");
    }

    [Fact]
    public void UpdateTagDetails_ForATagOnAnotherAxis_Fails()
    {
        // Arrange
        var sut = new IntakeRequestTagCategoryFaker().WithTags("Payments").Generate();

        // Act
        var result = sut.UpdateTagDetails(Guid.NewGuid(), "Anything", null, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestTagCategory.TagNotFoundError);
    }

    #endregion UpdateTagDetails

    #region Tag activation and removal

    [Fact]
    public void DeactivateTag_WhenActive_DeactivatesAndRaises()
    {
        // Arrange
        var sut = new IntakeRequestTagCategoryFaker().WithTags("Payments").Generate();
        var tag = sut.Tags.Single();

        // Act
        var result = sut.DeactivateTag(tag.Id, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        tag.IsActive.Should().BeFalse();
        sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestTagDeactivatedEvent>()
            .Which.TagId.Should().Be(tag.Id);
    }

    [Fact]
    public void ActivateTag_WhenInactive_ActivatesAndRaises()
    {
        // Arrange
        var sut = new IntakeRequestTagCategoryFaker().WithTags("Payments").Generate();
        var tag = sut.Tags.Single();
        sut.DeactivateTag(tag.Id, EventActor.System, Now);
        sut.ClearDomainEvents();

        // Act
        var result = sut.ActivateTag(tag.Id, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        tag.IsActive.Should().BeTrue();
        sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestTagActivatedEvent>();
    }

    [Fact]
    public void DeactivateTag_OnASystemAxis_Fails()
    {
        // Arrange
        var sut = new IntakeRequestTagCategoryFaker().WithTags("Payments").WithIsSystem(true).Generate();
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
        var sut = new IntakeRequestTagCategoryFaker().WithTags("Payments").Generate();
        var tag = sut.Tags.Single();

        // Act
        var result = sut.RemoveTag(tag.Id, isInUse: false, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Tags.Should().BeEmpty();
        var removed = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestTagRemovedEvent>().Subject;
        removed.TagId.Should().Be(tag.Id);
        removed.Name.Should().Be("Payments");
    }

    [Fact]
    public void RemoveTag_WhenInUse_FailsAndKeepsIt()
    {
        // Arrange
        var sut = new IntakeRequestTagCategoryFaker().WithTags("Payments").Generate();
        var tag = sut.Tags.Single();

        // Act
        var result = sut.RemoveTag(tag.Id, isInUse: true, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestTagCategory.TagInUseError);
        sut.Tags.Should().ContainSingle();
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion Tag activation and removal
}
