using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Domain.Tests.Sut.Models;

public sealed class ProductTagTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 28, 9, 0, 0);

    [Fact]
    public void CreateFree_RaisesCreatedWithTheNormalisedName()
    {
        // Act
        var result = ProductTag.CreateFree(" migration   wave 2 ", EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var sut = result.Value;
        sut.IsFree.Should().BeTrue();
        var created = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductFreeTagCreatedEvent>().Subject;
        created.Id.Should().Be(sut.Id);
        created.Name.Should().Be("migration wave 2");
        created.Timestamp.Should().Be(Now);
    }

    [Fact]
    public void CreateFree_WithABlankName_Fails()
    {
        // Act
        var result = ProductTag.CreateFree("  ", EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void UpdateFreeTagDetails_RaisesBothEnds()
    {
        // Arrange
        var sut = ProductTag.CreateFree("wave 2", EventActor.System, Now).Value;
        sut.ClearDomainEvents();

        // Act
        sut.UpdateFreeTagDetails("Wave 2", "Second migration wave", EventActor.System, Now);

        // Assert
        var updated = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductFreeTagDetailsUpdatedEvent>().Subject;
        updated.Name.Should().Be("Wave 2");
        updated.Description.Should().Be("Second migration wave");
        updated.PreviousName.Should().Be("wave 2");
        updated.PreviousDescription.Should().BeNull();
    }

    [Fact]
    public void DeleteFreeTag_RaisesWithItsNameAndTheTagItWasMergedInto()
    {
        // Arrange
        var sut = ProductTag.CreateFree("wave 2", EventActor.System, Now).Value;
        sut.ClearDomainEvents();
        var target = Guid.CreateVersion7();

        // Act
        sut.DeleteFreeTag(target, EventActor.System, Now);

        // Assert
        var deleted = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductFreeTagDeletedEvent>().Subject;
        deleted.Name.Should().Be("wave 2");
        deleted.MergedIntoTagId.Should().Be(target);
    }
}
