using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli.Generation;
using Wayd.Tools.DataGeneration.Cli.Recipes;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

/// <summary>
/// Laying one recipe over another. The rule is one line — stated wins, unstated shows through — and every
/// knob has to obey it, because a knob that quietly does not is a value someone set and never received.
/// </summary>
public class RecipeLayeringTests
{
    [Fact]
    public void LayerOver_TakesTheUpperValueWhereItIsStated()
    {
        // Arrange
        var under = new Recipe { Organization = new OrganizationRecipe { Teams = 10 } };
        var over = new Recipe { Organization = new OrganizationRecipe { Teams = 40 } };

        // Act
        var result = over.LayerOver(under);

        // Assert
        result.Organization!.Teams.Should().Be(40);
    }

    [Fact]
    public void LayerOver_LeavesTheLowerValueShowingWhereTheUpperSaysNothing()
    {
        // Arrange — the reason every field is nullable: null is "not stated", not "zero"
        var under = new Recipe { Organization = new OrganizationRecipe { Teams = 10, ValueStreams = 3 } };
        var over = new Recipe { Organization = new OrganizationRecipe { Teams = 40 } };

        // Act
        var result = over.LayerOver(under);

        // Assert
        result.Organization!.ValueStreams.Should().Be(3);
    }

    [Fact]
    public void LayerOver_KeepsTheLowerAreaWhenTheUpperOmitsItEntirely()
    {
        // Arrange
        var under = new Recipe { Ppm = new PpmRecipe { FunctionPortfolios = 4 } };
        var over = new Recipe { Organization = new OrganizationRecipe { Teams = 40 } };

        // Act
        var result = over.LayerOver(under);

        // Assert
        result.Ppm!.FunctionPortfolios.Should().Be(4);
    }

    [Fact]
    public void LayerOver_KeepsTheUpperAreaWhenTheLowerOmitsItEntirely()
    {
        // Arrange
        var under = new Recipe();
        var over = new Recipe { Ppm = new PpmRecipe { FunctionPortfolios = 4 } };

        // Act
        var result = over.LayerOver(under);

        // Assert
        result.Ppm!.FunctionPortfolios.Should().Be(4);
    }

    [Fact]
    public void LayerOver_LetsAnUpperLayerTurnAnAreaOff()
    {
        // Arrange — what replaces a --skip-x flag per area
        var under = new Recipe { Ppm = new PpmRecipe { Enabled = true, FunctionPortfolios = 4 } };
        var over = new Recipe { Ppm = new PpmRecipe { Enabled = false } };

        // Act
        var result = ResolvedRecipe.From(over.LayerOver(under).LayerOver(RecipeLibrary.Defaults()), seed: 1);

        // Assert
        result.GeneratePpm.Should().BeFalse();
    }

    [Fact]
    public void LayerOver_DoesNotLetAnUnstatedEnabledTurnAnAreaBackOn()
    {
        // Arrange — a flags layer states nothing about Enabled unless --skip-ppm was passed, so it must
        // not overwrite a recipe that switched the area off
        var recipe = new Recipe { Ppm = new PpmRecipe { Enabled = false } };
        var flags = new Recipe { Ppm = new PpmRecipe { ConcurrentProjectsPerArt = 3 } };

        // Act
        var result = ResolvedRecipe.From(flags.LayerOver(recipe).LayerOver(RecipeLibrary.Defaults()), seed: 1);

        // Assert
        result.GeneratePpm.Should().BeFalse();
    }

    [Fact]
    public void LayerOver_CarriesEveryKnobOfEveryArea()
    {
        // Arrange — the failure this guards is a knob added to the model and forgotten in the layering,
        // which reads as "my recipe set that and nothing happened". Rather than one test per field, this
        // states every knob in the upper layer over a lower layer that states different values, and
        // requires the upper one to win throughout.
        var under = new Recipe
        {
            Timeline = new TimelineRecipe
            {
                AsOf = new DateTime(2000, 1, 1),
                CompanyAgeYears = 1,
                TeamStructureAgeYears = 1,
                HistoryYears = 1,
                RunwayYears = 1,
            },
            Organization = new OrganizationRecipe
            {
                CompanyType = CompanyType.Enterprise,
                DeliveryRatio = 0.1,
                ValueStreams = 1,
                Teams = 1,
                FormerEmployeeFraction = 0.01,
            },
            Ppm = new PpmRecipe
            {
                FunctionPortfolios = 1,
                ConcurrentProjectsPerArt = 1,
                ConcurrentProgramsPerPortfolio = 1,
            },
        };

        var over = new Recipe
        {
            Timeline = new TimelineRecipe
            {
                AsOf = new DateTime(2026, 6, 15),
                CompanyAgeYears = 9,
                TeamStructureAgeYears = 8,
                HistoryYears = 7,
                RunwayYears = 6,
            },
            Organization = new OrganizationRecipe
            {
                CompanyType = CompanyType.Balanced,
                DeliveryRatio = 0.9,
                ValueStreams = 5,
                Teams = 55,
                FormerEmployeeFraction = 0.5,
            },
            Ppm = new PpmRecipe
            {
                FunctionPortfolios = 6,
                ConcurrentProjectsPerArt = 11,
                ConcurrentProgramsPerPortfolio = 4,
            },
        };

        // Act
        var result = ResolvedRecipe.From(over.LayerOver(under), seed: 1);

        // Assert
        result.Context.AsOf.Should().Be(new DateTime(2026, 6, 15));
        result.Context.CompanyAgeYears.Should().Be(9);
        result.Context.TeamStructureAgeYears.Should().Be(8);
        result.Context.HistoryYears.Should().Be(7);
        result.Context.RunwayYears.Should().Be(6);

        result.Organization.CompanyType.Should().Be(CompanyType.Balanced);
        result.Organization.DeliveryRatio.Should().Be(0.9);
        result.Organization.ValueStreams.Should().Be(5);
        result.Organization.Teams.Should().Be(55);
        result.Organization.FormerEmployeeFraction.Should().Be(0.5);

        result.Ppm.FunctionPortfolios.Should().Be(6);
        result.Ppm.ConcurrentProjectsPerArt.Should().Be(11);
        result.Ppm.ConcurrentProgramsPerPortfolio.Should().Be(4);
    }

    [Fact]
    public void LayerOver_LetsEveryKnobOfTheLowerLayerShowThrough()
    {
        // Arrange — the mirror of the above, and the half that catches a knob wired as "always take the
        // upper value" rather than "take it when stated"
        var under = new Recipe
        {
            Timeline = new TimelineRecipe
            {
                AsOf = new DateTime(2026, 6, 15),
                CompanyAgeYears = 9,
                TeamStructureAgeYears = 8,
                HistoryYears = 7,
                RunwayYears = 6,
            },
            Organization = new OrganizationRecipe
            {
                CompanyType = CompanyType.Balanced,
                DeliveryRatio = 0.9,
                ValueStreams = 5,
                Teams = 55,
                FormerEmployeeFraction = 0.5,
            },
            Ppm = new PpmRecipe
            {
                FunctionPortfolios = 6,
                ConcurrentProjectsPerArt = 11,
                ConcurrentProgramsPerPortfolio = 4,
            },
        };

        // Act — an upper layer that states nothing at all, which is what an unflagged run looks like
        var result = ResolvedRecipe.From(new Recipe().LayerOver(under), seed: 1);

        // Assert
        result.Context.AsOf.Should().Be(new DateTime(2026, 6, 15));
        result.Context.CompanyAgeYears.Should().Be(9);
        result.Context.TeamStructureAgeYears.Should().Be(8);
        result.Context.HistoryYears.Should().Be(7);
        result.Context.RunwayYears.Should().Be(6);

        result.Organization.CompanyType.Should().Be(CompanyType.Balanced);
        result.Organization.DeliveryRatio.Should().Be(0.9);
        result.Organization.ValueStreams.Should().Be(5);
        result.Organization.Teams.Should().Be(55);
        result.Organization.FormerEmployeeFraction.Should().Be(0.5);

        result.Ppm.FunctionPortfolios.Should().Be(6);
        result.Ppm.ConcurrentProjectsPerArt.Should().Be(11);
        result.Ppm.ConcurrentProgramsPerPortfolio.Should().Be(4);
    }

    [Fact]
    public void From_AnchorsOnTodayWhenNoLayerPinsAsOf()
    {
        // Arrange — the one knob a recipe may leave open, so an unpinned run still straddles now
        var recipe = RecipeLibrary.Defaults();

        // Act
        var result = ResolvedRecipe.From(recipe, seed: 1);

        // Assert
        result.Context.AsOf.Should().Be(DateTime.UtcNow.Date);
    }
}
