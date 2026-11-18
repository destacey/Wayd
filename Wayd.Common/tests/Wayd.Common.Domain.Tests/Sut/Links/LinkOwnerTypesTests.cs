using Wayd.Common.Domain.Links;

namespace Wayd.Common.Domain.Tests.Sut.Links;

public sealed class LinkOwnerTypesTests
{
    [Fact]
    public void Resolve_ShouldFindARegisteredTypeWhateverTheCasingOrPadding()
    {
        // Arrange
        var widget = new LinkOwnerTypeDescriptor("test.links.resolve", "Widget", "Widget");
        LinkOwnerTypes.Register(widget);

        // Act
        var result = LinkOwnerTypes.Resolve(" TEST.Links.Resolve ");

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().BeSameAs(widget);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Resolve_ShouldFail_WhenNoTypeIsGiven(string? key)
    {
        // Arrange — the key is the theory data

        // Act
        var result = LinkOwnerTypes.Resolve(key);

        // Assert
        result.Error.Should().Be(LinkErrors.OwnerTypeRequired);
    }

    [Fact]
    public void Resolve_ShouldFail_ForAnUnregisteredType()
    {
        // Arrange
        const string key = "test.links.unregistered";

        // Act
        var result = LinkOwnerTypes.Resolve(key);

        // Assert
        result.Error.Should().Be(LinkErrors.UnknownOwnerType(key));
    }

    [Fact]
    public void Register_ShouldIgnoreTheSameDescriptorTwice_ButRefuseADifferentOneUnderItsKey()
    {
        // Arrange
        var first = new LinkOwnerTypeDescriptor("test.links.conflict", "First", "First");
        var second = new LinkOwnerTypeDescriptor("test.links.conflict", "Second", "Second");
        LinkOwnerTypes.Register(first);

        // Act
        var again = () => LinkOwnerTypes.Register(first);
        var conflict = () => LinkOwnerTypes.Register(second);

        // Assert
        again.Should().NotThrow();
        conflict.Should().Throw<InvalidOperationException>();
        LinkOwnerTypes.Resolve("test.links.conflict").Value.Should().BeSameAs(first);
    }

    [Fact]
    public void Descriptor_ShouldRefuseAKeyLongerThanItsColumn()
    {
        // Arrange
        var key = new string('k', LinkOwnerTypeDescriptor.KeyMaxLength + 1);

        // Act
        var act = () => new LinkOwnerTypeDescriptor(key, "Too long", "TooLong");

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
