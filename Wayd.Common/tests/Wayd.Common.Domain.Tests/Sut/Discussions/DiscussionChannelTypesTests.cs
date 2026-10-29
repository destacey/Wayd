using Wayd.Common.Domain.Discussions;

namespace Wayd.Common.Domain.Tests.Sut.Discussions;

public sealed class DiscussionChannelTypesTests
{
    [Fact]
    public void Resolve_ShouldFindARegisteredTypeWhateverTheCasingOrPadding()
    {
        // Arrange
        var widget = new DiscussionChannelTypeDescriptor("test.discussions.resolve", "Widget", "Widgets", supportsInternalNotes: true);
        DiscussionChannelTypes.Register(widget);

        // Act
        var result = DiscussionChannelTypes.Resolve(" TEST.Discussions.Resolve ");

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().BeSameAs(widget);
    }

    [Fact]
    public void Resolve_ShouldFail_ForAnUnregisteredType()
    {
        // Act
        var result = DiscussionChannelTypes.Resolve("test.discussions.unregistered");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionErrors.UnknownChannelType("test.discussions.unregistered"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Resolve_ShouldFail_WhenNoTypeIsGiven(string? key)
    {
        // Act
        var result = DiscussionChannelTypes.Resolve(key);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionErrors.ChannelTypeRequired);
    }

    [Fact]
    public void Register_ShouldAllowTheSameDescriptorTwice()
    {
        // Arrange
        var widget = new DiscussionChannelTypeDescriptor("test.discussions.twice", "Widget", "Widgets", supportsInternalNotes: false);
        DiscussionChannelTypes.Register(widget);

        // Act
        Action act = () => DiscussionChannelTypes.Register(widget);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Register_ShouldThrow_WhenADifferentDescriptorClaimsAKey()
    {
        // Arrange
        DiscussionChannelTypes.Register(new DiscussionChannelTypeDescriptor("test.discussions.conflict", "Widget", "Widgets", supportsInternalNotes: false));
        var rival = new DiscussionChannelTypeDescriptor("TEST.discussions.conflict", "Rival", "Rivals", supportsInternalNotes: true);

        // Act
        Action act = () => DiscussionChannelTypes.Register(rival);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*already registered as 'TEST.discussions.conflict'*");
        DiscussionChannelTypes.Resolve("test.discussions.conflict").Value.DisplayName.Should().Be("Widget");
    }

    [Fact]
    public void Descriptor_ShouldThrow_WhenTheKeyIsLongerThanTheColumn()
    {
        // Act
        Action act = () => _ = new DiscussionChannelTypeDescriptor(new string('k', DiscussionChannelTypeDescriptor.KeyMaxLength + 1), "Widget", "Widgets", supportsInternalNotes: false);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("key");
    }
}
