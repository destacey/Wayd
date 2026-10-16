using System.Text.Json;
using Wayd.Infrastructure.OpenApi;

namespace Wayd.Infrastructure.Tests.Sut.OpenApi;

public sealed class SchemaReferenceIdsTests
{
    [Theory]
    [InlineData(typeof(Pair<Guid, int>), "PairOfGuidAndInteger")]
    [InlineData(typeof(Pair<Guid, string>), "PairOfGuidAndString")]
    [InlineData(typeof(Box<Sample>), "BoxOfSample")]
    [InlineData(typeof(Box<Pair<Guid, long>>), "BoxOfPairOfGuidAndLong")]
    [InlineData(typeof(Sample), "Sample")]
    public void For_NamesAGenericTypeForItsArguments(Type type, string expected)
    {
        // Arrange
        var subject = type;

        // Act
        var id = SchemaReferenceIds.For(subject);

        // Assert
        id.Should().Be(expected);
    }

    [Fact]
    public void Create_NamesAGenericObjectForItsTypeArguments()
    {
        // Arrange
        var typeInfo = JsonSerializerOptions.Web.GetTypeInfo(typeof(Pair<Guid, int>));

        // Act
        var id = SchemaReferenceIds.Create(typeInfo);

        // Assert
        id.Should().Be("PairOfGuidAndInteger");
    }

    [Fact]
    public void Create_GivesANullableEnumTheEnumsOwnComponent()
    {
        // Arrange
        var typeInfo = JsonSerializerOptions.Web.GetTypeInfo(typeof(SampleKind?));

        // Act
        var id = SchemaReferenceIds.Create(typeInfo);

        // Assert
        id.Should().Be(nameof(SampleKind));
    }

    [Theory]
    [InlineData(typeof(Instant))]
    [InlineData(typeof(LocalDate?))]
    [InlineData(typeof(TimeSpan))]
    public void Create_InlinesAValueDescribedAsAString(Type type)
    {
        // Arrange
        var typeInfo = JsonSerializerOptions.Web.GetTypeInfo(type);

        // Act
        var id = SchemaReferenceIds.Create(typeInfo);

        // Assert
        id.Should().BeNull();
    }

    private sealed record Box<T>(T Value);

    private sealed record Pair<TFirst, TSecond>(TFirst First, TSecond Second);

    private sealed record Sample(string Name);

    private enum SampleKind
    {
        First,
        Second,
    }
}
