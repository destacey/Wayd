using FluentAssertions;
using Wayd.ProductManagement.Application.Products.Commands;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Commands;

/// <summary>
/// The rules the validator enforces before a handler ever runs.
/// </summary>
public sealed class LinkProductExternallyCommandValidatorTests
{
    private readonly LinkProductExternallyCommandValidator _sut = new();

    [Fact]
    public void Validate_ShouldRejectAnEmptyId()
    {
        // Arrange
        var command = new LinkProductExternallyCommand(Guid.Empty, "acme/checkout");

        // Act
        var result = _sut.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldRejectALinkOverTheColumnLength()
    {
        // Arrange
        var command = new LinkProductExternallyCommand(Guid.CreateVersion7(), new string('a', 257));

        // Act
        var result = _sut.Validate(command);

        // Assert
        // 256 is the mapped column width; letting a longer one through turns a validation message into
        // a truncation error at the database.
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldAcceptANullLink()
    {
        // Arrange
        var command = new LinkProductExternallyCommand(Guid.CreateVersion7(), null);

        // Act
        var result = _sut.Validate(command);

        // Assert
        // Null is how a product is unlinked, not a missing value.
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldAcceptAValidCommand()
    {
        // Arrange
        var command = new LinkProductExternallyCommand(Guid.CreateVersion7(), "acme/checkout");

        // Act
        var result = _sut.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
