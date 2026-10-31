using FluentAssertions;
using FluentValidation.TestHelper;
using Wayd.Web.Api.Models.ProductManagement.Products;

namespace Wayd.Web.Api.Tests.Sut.Models.ProductManagement.Products;

/// <summary>
/// A product CSV row's Teams column, and what it becomes: a list of team codes and ids, each refused at the row
/// when it could be neither.
/// </summary>
public sealed class ImportProductRequestTests
{
    private readonly ImportProductRequestValidator _validator = new();

    private static ImportProductRequest Row(string? teams = null) =>
        new()
        {
            ImportId = "1",
            Name = "Storefront Web",
            ProductTypeName = "Application",
            Teams = teams,
        };

    [Fact]
    public void ToImportProductDto_SplitsTheTeamsOnSemicolons_TrimmingAndDroppingRepeats()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        var request = Row($" PAY ; {id};;pay ;MOB");

        // Act
        var dto = request.ToImportProductDto();

        // Assert
        dto.Teams.Should().Equal("PAY", id.ToString(), "MOB");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ToImportProductDto_ReadsABlankCellAsNoTeams(string? teams)
    {
        // Act
        var dto = Row(teams).ToImportProductDto();

        // Assert
        dto.Teams.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("PAY")]
    [InlineData("pay;MOB2;0196b5c4-1f1e-7a52-9d2e-3c4b5a697887")]
    public void Validate_AcceptsTeamCodesAndIds(string? teams)
    {
        // Act
        var result = _validator.TestValidate(Row(teams));

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("Payments Team")]
    [InlineData("PAY;TOOLONGCODE1")]
    [InlineData("PAY|MOB")]
    public void Validate_RefusesAnEntryThatIsNeitherACodeNorAnId(string teams)
    {
        // Act
        var result = _validator.TestValidate(Row(teams));

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Teams)
            .WithErrorMessage("Each team must be a team code or a team ID, separated by semicolons.");
    }

    [Fact]
    public void Validate_RefusesTheEmptyId_SayingSo()
    {
        // Act
        var result = _validator.TestValidate(Row("PAY;00000000-0000-0000-0000-000000000000"));

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Teams)
            .WithErrorMessage("A team ID cannot be the empty ID, which names no team.");
    }
}
