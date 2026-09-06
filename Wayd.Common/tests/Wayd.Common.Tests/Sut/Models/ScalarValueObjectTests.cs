using System.Text.Json;
using FluentAssertions;
using Wayd.Common.Models;
using Xunit;

namespace Wayd.Common.Tests.Sut.Models;

public sealed class ScalarValueObjectTests
{
    [System.Text.Json.Serialization.JsonConverter(typeof(Wayd.Common.Serialization.ScalarValueObjectJsonConverterFactory))]
    private sealed class TestStringVo : ScalarValueObject<string>
    {
        public TestStringVo(string value) : base(value) { }
    }

    [System.Text.Json.Serialization.JsonConverter(typeof(Wayd.Common.Serialization.ScalarValueObjectJsonConverterFactory))]
    private sealed class TestDecimalVo : ScalarValueObject<decimal>
    {
        public TestDecimalVo(decimal value) : base(value) { }
    }

    private sealed record TestContainer(TestStringVo StringVo, TestDecimalVo DecimalVo);

    [Fact]
    public void Serializes_As_Primitive_Scalar_Not_Object()
    {
        // Arrange
        var stringVo = new TestStringVo("TEST_VALUE");
        var decimalVo = new TestDecimalVo(42.5m);

        // Act
        var stringJson = JsonSerializer.Serialize(stringVo);
        var decimalJson = JsonSerializer.Serialize(decimalVo);

        // Assert - verify it is serialized directly as the primitive scalar, not as {"value": ...}
        stringJson.Should().Be("\"TEST_VALUE\"");
        decimalJson.Should().Be("42.5");
    }

    [Fact]
    public void Container_Serializes_Properties_As_Scalars()
    {
        // Arrange
        var container = new TestContainer(new TestStringVo("APOLLO"), new TestDecimalVo(99.9m));

        // Act
        var json = JsonSerializer.Serialize(container);

        // Assert
        json.Should().Be("{\"StringVo\":\"APOLLO\",\"DecimalVo\":99.9}");
    }

    [Fact]
    public void Deserializes_From_Primitive_Scalar()
    {
        // Arrange
        var stringJson = "\"TEST_VALUE\"";
        var decimalJson = "42.5";

        // Act
        var stringVo = JsonSerializer.Deserialize<TestStringVo>(stringJson);
        var decimalVo = JsonSerializer.Deserialize<TestDecimalVo>(decimalJson);

        // Assert
        stringVo.Should().NotBeNull();
        stringVo!.Value.Should().Be("TEST_VALUE");

        decimalVo.Should().NotBeNull();
        decimalVo!.Value.Should().Be(42.5m);
    }

    [Fact]
    public void Deserializes_From_Legacy_Object_Payload_For_Backward_Compatibility()
    {
        // Arrange - test both lowercase "value" and uppercase "Value"
        var legacyJson1 = "{\"value\":\"LEGACY_VAL\"}";
        var legacyJson2 = "{\"Value\":12.34}";

        // Act
        var stringVo = JsonSerializer.Deserialize<TestStringVo>(legacyJson1);
        var decimalVo = JsonSerializer.Deserialize<TestDecimalVo>(legacyJson2);

        // Assert
        stringVo.Should().NotBeNull();
        stringVo!.Value.Should().Be("LEGACY_VAL");

        decimalVo.Should().NotBeNull();
        decimalVo!.Value.Should().Be(12.34m);
    }

    [Fact]
    public void Implicit_Operator_Returns_Underlying_Value()
    {
        // Arrange
        var stringVo = new TestStringVo("HELLO");
        var decimalVo = new TestDecimalVo(10.5m);

        // Act
        string stringVal = stringVo;
        decimal decimalVal = decimalVo;

        // Assert
        stringVal.Should().Be("HELLO");
        decimalVal.Should().Be(10.5m);
    }

    [Fact]
    public void Value_Equality_Works_Correctly()
    {
        // Arrange
        var vo1 = new TestStringVo("ABC");
        var vo2 = new TestStringVo("ABC");
        var vo3 = new TestStringVo("XYZ");

        // Assert
        vo1.Should().Be(vo2);
        vo1.Should().NotBe(vo3);
        (vo1 == vo2).Should().BeTrue();
        (vo1 != vo3).Should().BeTrue();
    }

    [Fact]
    public void Serializes_Null_ValueObject_As_Null()
    {
        // Arrange
        TestStringVo? nullVo = null;

        // Act
        var json = JsonSerializer.Serialize(nullVo);

        // Assert
        json.Should().Be("null");
    }
}
