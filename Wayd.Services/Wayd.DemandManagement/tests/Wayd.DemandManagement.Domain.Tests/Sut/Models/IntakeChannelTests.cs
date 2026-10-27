using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Domain.Tests.Sut.Models;

public sealed class IntakeChannelTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 27, 9, 0, 0);

    #region Create

    [Fact]
    public void Create_WhenValid_IsActiveChannel()
    {
        // Act
        var sut = IntakeChannel.Create(" Customer feedback ", "  ", 2, EventActor.System, Now);

        // Assert
        sut.Name.Should().Be("Customer feedback");
        sut.Description.Should().BeNull();
        sut.Order.Should().Be(2);
        sut.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_WhenValid_RaisesCreatedWithTheNormalisedValues()
    {
        // Act
        var sut = IntakeChannel.Create(" Customer feedback ", " What customers ask for ", 2, EventActor.System, Now);

        // Assert
        var created = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeChannelCreatedEvent>().Subject;
        created.Id.Should().Be(sut.Id);
        created.Name.Should().Be("Customer feedback");
        created.Description.Should().Be("What customers ask for");
        created.Order.Should().Be(2);
        created.Timestamp.Should().Be(Now);
    }

    [Fact]
    public void Create_WithABlankName_Throws()
    {
        // Act
        Action act = () => IntakeChannel.Create("  ", null, 1, EventActor.System, Now);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("Required input Name was empty. (Parameter 'Name')");
    }

    #endregion Create

    #region UpdateDetails

    [Fact]
    public void UpdateDetails_WhenChanged_RaisesBothEnds()
    {
        // Arrange
        var sut = new IntakeChannelFaker().WithName("Support").WithDescription("From support cases.").Generate();

        // Act
        var result = sut.UpdateDetails(" Customer support ", null, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Name.Should().Be("Customer support");
        sut.Description.Should().BeNull();
        var updated = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeChannelDetailsUpdatedEvent>().Subject;
        updated.Name.Should().Be("Customer support");
        updated.Description.Should().BeNull();
        updated.PreviousName.Should().Be("Support");
        updated.PreviousDescription.Should().Be("From support cases.");
    }

    [Fact]
    public void UpdateDetails_WhenOnlyWhitespaceDiffers_RaisesNothing()
    {
        // Arrange
        var sut = new IntakeChannelFaker().WithName("Support").WithDescription("From support cases.").Generate();

        // Act
        var result = sut.UpdateDetails(" Support ", " From support cases. ", EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion UpdateDetails

    #region Reorder

    [Fact]
    public void Reorder_ToANewPosition_RaisesBothEnds()
    {
        // Arrange
        var sut = new IntakeChannelFaker().WithOrder(3).Generate();

        // Act
        var result = sut.Reorder(1, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Order.Should().Be(1);
        var reordered = sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeChannelReorderedEvent>().Subject;
        reordered.FromOrder.Should().Be(3);
        reordered.ToOrder.Should().Be(1);
    }

    [Fact]
    public void Reorder_ToItsCurrentPosition_RaisesNothing()
    {
        // Arrange
        var sut = new IntakeChannelFaker().WithOrder(3).Generate();

        // Act
        sut.Reorder(3, EventActor.System, Now);

        // Assert
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion Reorder

    #region Archive and Restore

    [Fact]
    public void Archive_WhenActive_TakesItOutOfUse()
    {
        // Arrange
        var sut = new IntakeChannelFaker().Generate();

        // Act
        var result = sut.Archive(EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.IsActive.Should().BeFalse();
        sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeChannelArchivedEvent>();
    }

    [Fact]
    public void Archive_WhenAlreadyArchived_RaisesNothing()
    {
        // Arrange
        var sut = new IntakeChannelFaker().AsArchived().Generate();

        // Act
        var result = sut.Archive(EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Restore_WhenArchived_BringsItBackIntoUse()
    {
        // Arrange
        var sut = new IntakeChannelFaker().AsArchived().Generate();

        // Act
        var result = sut.Restore(EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.IsActive.Should().BeTrue();
        sut.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeChannelRestoredEvent>();
    }

    [Fact]
    public void Restore_WhenActive_RaisesNothing()
    {
        // Arrange
        var sut = new IntakeChannelFaker().Generate();

        // Act
        sut.Restore(EventActor.System, Now);

        // Assert
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion Archive and Restore
}
