using FluentAssertions;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Domain.Tests.Sut.Models;

public sealed class ProductTypeTests
{
    #region Create

    [Fact]
    public void Create_WhenValid_Success()
    {
        // Arrange & Act
        var sut = ProductType.Create("Service", "A deployable service.", isReleasable: true, order: 3);

        // Assert
        sut.Name.Should().Be("Service");
        sut.Description.Should().Be("A deployable service.");
        sut.IsReleasable.Should().BeTrue();
        sut.Order.Should().Be(3);
        sut.IsSystem.Should().BeFalse();
    }

    [Fact]
    public void CreateSystem_ShouldMarkTheTypeAsSystemOwned()
    {
        // Arrange & Act
        var sut = ProductType.CreateSystem("Product Line", "A logical grouping.", isReleasable: false, order: 1);

        // Assert
        sut.IsSystem.Should().BeTrue();
        sut.IsReleasable.Should().BeFalse();
    }

    [Fact]
    public void Create_WithBlankName_Throws()
    {
        // Act
        Action act = () => ProductType.Create("   ", null, isReleasable: true, order: 1);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("Required input Name was empty. (Parameter 'Name')");
    }

    #endregion Create

    #region Update

    [Fact]
    public void Update_ShouldRenameAndReorder()
    {
        // Arrange
        var sut = ProductType.Create("Service", null, isReleasable: true, order: 3);

        // Act
        var result = sut.Update("Microservice", "Renamed.", 4);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Name.Should().Be("Microservice");
        sut.Order.Should().Be(4);
    }

    [Fact]
    public void Update_ShouldFail_OnASystemType()
    {
        // Arrange
        var sut = ProductType.CreateSystem("Service", null, isReleasable: true, order: 3);

        // Act
        var result = sut.Update("Microservice", null, 4);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("System product types cannot be modified.");
    }

    #endregion Update

    #region SetReleasable

    [Fact]
    public void SetReleasable_ShouldToggleTheFlag()
    {
        // Arrange
        var sut = ProductType.Create("Module", null, isReleasable: true, order: 5);

        // Act
        var result = sut.SetReleasable(false);

        // Assert
        // An embedded node — a connector compiled into the API — ships inside its host's release rather
        // than carrying one of its own.
        result.IsSuccess.Should().BeTrue();
        sut.IsReleasable.Should().BeFalse();
    }

    [Fact]
    public void SetReleasable_ShouldFail_OnASystemType()
    {
        // Arrange
        var sut = ProductType.CreateSystem("Module", null, isReleasable: false, order: 5);

        // Act
        var result = sut.SetReleasable(true);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("System product types cannot be modified.");
    }

    #endregion SetReleasable
}
