using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Domain.Tests.Sut.Kpis;

public sealed class KpiSubjectTypesTests
{
    [Fact]
    public void Resolve_ShouldFindARegisteredTypeWhateverTheCasingOrPadding()
    {
        // Arrange
        var widget = new KpiSubjectTypeDescriptor("test.kpis.resolve", "Widget", "Widget");
        KpiSubjectTypes.Register(widget);

        // Act
        var result = KpiSubjectTypes.Resolve(" TEST.Kpis.Resolve ");

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().BeSameAs(widget);
    }

    [Theory]
    [InlineData(null, KpiErrors.SubjectTypeRequired)]
    [InlineData("  ", KpiErrors.SubjectTypeRequired)]
    public void Resolve_ShouldFail_WhenNoTypeIsGiven(string? key, string expected)
    {
        // Arrange — the key is the theory data

        // Act
        var result = KpiSubjectTypes.Resolve(key);

        // Assert
        result.Error.Should().Be(expected);
    }

    [Fact]
    public void Resolve_ShouldFail_ForAnUnregisteredType()
    {
        // Arrange
        const string key = "test.kpis.unregistered";

        // Act
        var result = KpiSubjectTypes.Resolve(key);

        // Assert
        result.Error.Should().Be(KpiErrors.UnknownSubjectType(key));
    }

    [Fact]
    public void Register_ShouldIgnoreTheSameDescriptorTwice_ButRefuseADifferentOneUnderItsKey()
    {
        // Arrange
        var first = new KpiSubjectTypeDescriptor("test.kpis.conflict", "First", "First");
        var second = new KpiSubjectTypeDescriptor("test.kpis.conflict", "Second", "Second");
        KpiSubjectTypes.Register(first);

        // Act
        var again = () => KpiSubjectTypes.Register(first);
        var conflict = () => KpiSubjectTypes.Register(second);

        // Assert
        again.Should().NotThrow();
        conflict.Should().Throw<InvalidOperationException>();
        KpiSubjectTypes.Resolve("test.kpis.conflict").Value.Should().BeSameAs(first);
    }

    [Fact]
    public void Descriptor_ShouldRefuseAKeyLongerThanTheColumn()
    {
        // Arrange
        var key = new string('k', KpiSubjectTypeDescriptor.KeyMaxLength + 1);

        // Act
        var create = () => new KpiSubjectTypeDescriptor(key, "Long", "Long");

        // Assert
        create.Should().Throw<ArgumentException>();
    }
}
