using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Domain.Tests.Sut.Models.PlanningPoker;

public class EstimationScaleTests
{
    private static readonly string[] FibonacciValues =
    [
        "0", "1", "2", "3", "5", "8", "13", "21", "?"
    ];

    [Fact]
    public void Create_ValidParameters_ShouldReturnSuccess()
    {
        // Arrange & Act
        var result = EstimationScale.Create("Fibonacci", "Standard Fibonacci", FibonacciValues);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Fibonacci");
        result.Value.Description.Should().Be("Standard Fibonacci");
        result.Value.IsActive.Should().BeTrue();
        result.Value.Values.Should().HaveCount(9);
    }

    [Fact]
    public void Create_LessThanTwoValues_ShouldReturnFailure()
    {
        // Arrange
        var values = new[] { "1" };

        // Act
        var result = EstimationScale.Create("Test", null, values);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("at least 2 values");
    }

    [Fact]
    public void Create_ShouldPreserveOrder()
    {
        // Arrange
        var values = new[] { "5", "1", "3" };

        // Act
        var result = EstimationScale.Create("Test", null, values);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var orderedValues = result.Value.Values.ToList();
        orderedValues[0].Should().Be("5");
        orderedValues[1].Should().Be("1");
        orderedValues[2].Should().Be("3");
    }

    [Fact]
    public void Update_ShouldReturnSuccess()
    {
        // Arrange
        var scale = EstimationScale.Create("Original", "Original desc", FibonacciValues).Value;

        // Act
        var result = scale.Update("Updated", "Updated desc");

        // Assert
        result.IsSuccess.Should().BeTrue();
        scale.Name.Should().Be("Updated");
        scale.Description.Should().Be("Updated desc");
    }

    [Fact]
    public void SetValues_ShouldReplaceValues()
    {
        // Arrange
        var scale = EstimationScale.Create("Test", null, FibonacciValues).Value;
        var newValues = new[] { "S", "M", "L" };

        // Act
        var result = scale.SetValues(newValues);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scale.Values.Should().HaveCount(3);
        scale.Values.First().Should().Be("S");
    }

    [Fact]
    public void SetValues_LessThanTwoValues_ShouldReturnFailure()
    {
        // Arrange
        var scale = EstimationScale.Create("Test", null, FibonacciValues).Value;

        // Act
        var result = scale.SetValues(["1"]);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("at least 2 values");
    }

    [Fact]
    public void Deactivate_ShouldReturnSuccess()
    {
        // Arrange
        var scale = EstimationScale.Create("Test", null, FibonacciValues).Value;

        // Act
        var result = scale.Deactivate();

        // Assert
        result.IsSuccess.Should().BeTrue();
        scale.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ShouldReturnFailure()
    {
        // Arrange
        var scale = EstimationScale.Create("Test", null, FibonacciValues).Value;
        scale.Deactivate();

        // Act
        var result = scale.Deactivate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already inactive");
    }

    [Fact]
    public void Activate_WhenInactive_ShouldReturnSuccess()
    {
        // Arrange
        var scale = EstimationScale.Create("Test", null, FibonacciValues).Value;
        scale.Deactivate();

        // Act
        var result = scale.Activate();

        // Assert
        result.IsSuccess.Should().BeTrue();
        scale.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ShouldReturnFailure()
    {
        // Arrange
        var scale = EstimationScale.Create("Test", null, FibonacciValues).Value;

        // Act
        var result = scale.Activate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already active");
    }
}
