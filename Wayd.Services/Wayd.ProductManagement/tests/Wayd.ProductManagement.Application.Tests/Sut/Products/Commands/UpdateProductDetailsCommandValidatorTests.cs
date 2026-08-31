using FluentAssertions;
using Wayd.ProductManagement.Application.Products.Commands;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Commands;

/// <summary>
/// The rules the validator enforces before a handler ever runs.
/// </summary>
public sealed class UpdateProductDetailsCommandValidatorTests
{
    private readonly UpdateProductDetailsCommandValidator _sut = new();

    [Fact]
    public void Validate_ShouldRejectAnEmptyName()
    {
        // Arrange
        var command = new UpdateProductDetailsCommand(Guid.CreateVersion7(), "", null);

        // Act
        var result = _sut.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldRejectANameOverTheColumnLength()
    {
        // Arrange
        var command = new UpdateProductDetailsCommand(Guid.CreateVersion7(), new string('a', 129), null);

        // Act
        var result = _sut.Validate(command);

        // Assert
        // 128 is the mapped column width; letting a longer one through turns a validation message into
        // a truncation error at the database.
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldAcceptAValidCommand()
    {
        // Arrange
        var command = new UpdateProductDetailsCommand(Guid.CreateVersion7(), "Payments", null);

        // Act
        var result = _sut.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
