using FluentAssertions;
using Wayd.ProductManagement.Application.Products.Dtos;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Dtos;

/// <summary>
/// The rules a product row's teams are held to before the import runs: each entry a team code or a team id.
/// </summary>
public sealed class ImportProductDtoValidatorTests
{
    private readonly ImportProductDtoValidator _sut = new();

    private static ImportProductDto Row(IReadOnlyList<string>? teams) =>
        new("Storefront Web", null, "Application", null, null, null, [], teams);

    [Fact]
    public async Task Validate_AcceptsARowStoredWithoutTeams()
    {
        // Act
        var result = await _sut.ValidateAsync(Row(null), TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_AcceptsTeamCodesInAnyCaseAndTeamIds()
    {
        // Act
        var result = await _sut.ValidateAsync(Row(["pay", "MOB2", Guid.CreateVersion7().ToString()]), TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Payments Team")]
    [InlineData("TOOLONGCODE1")]
    [InlineData("")]
    public async Task Validate_RefusesAnEntryThatIsNeitherACodeNorAnId(string entry)
    {
        // Act
        var result = await _sut.ValidateAsync(Row(["PAY", entry]), TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be($"'{entry}' is not a team ID or team code.");
    }

    [Fact]
    public async Task Validate_RefusesTheEmptyId_SayingSo()
    {
        // Act
        var result = await _sut.ValidateAsync(Row([Guid.Empty.ToString()]), TestContext.Current.CancellationToken);

        // Assert
        result.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be($"'{Guid.Empty}' is the empty ID, which names no team.");
    }
}
