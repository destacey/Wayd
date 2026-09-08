using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli.Recipes;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

/// <summary>
/// Numbers outside the range the schema states.
/// </summary>
/// <remarks>
/// The generators cope silently — <c>Math.Max(1, valueStreams)</c> turns a zero into a one and carries on
/// — so without this a recipe asking for no teams produces a company with some and reports nothing. That
/// is the same silent-ignore failure the format rejects unmapped members to avoid.
/// </remarks>
public class RecipeBoundsTests
{
    [Fact]
    public void Validate_RejectsACountBelowItsMinimum()
    {
        // Arrange — the case that prompted this: zero teams read as valid and generated a company anyway
        var recipe = new Recipe { Organization = new OrganizationRecipe { Teams = 0 } };

        // Act
        var act = () => RecipeBounds.Validate(recipe);

        // Assert — naming the field and both numbers, so the message says what to change
        act.Should().Throw<RecipeException>()
            .WithMessage("*organization.teams*")
            .WithMessage("*0*")
            .WithMessage("*1*");
    }

    [Fact]
    public void Validate_RejectsAFractionAboveItsMaximum()
    {
        // Arrange — a share of the company cannot exceed all of it
        var recipe = new Recipe { Organization = new OrganizationRecipe { DeliveryRatio = 1.5 } };

        // Act
        var act = () => RecipeBounds.Validate(recipe);

        // Assert
        act.Should().Throw<RecipeException>().WithMessage("*organization.deliveryRatio*");
    }

    [Fact]
    public void Validate_AcceptsAValueSittingExactlyOnTheBound()
    {
        // Arrange — the bounds are inclusive, and one team is a legitimate company
        var recipe = new Recipe
        {
            Organization = new OrganizationRecipe { Teams = 1, ValueStreams = 1, DeliveryRatio = 1 },
            Ppm = new PpmRecipe { FunctionPortfolios = 0 },
        };

        // Act
        var act = () => RecipeBounds.Validate(recipe);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_RejectsAValueAtTheTopOfTheRecipe()
    {
        // Arrange — version sits beside the areas rather than inside one, and its bound applies the same
        var recipe = new Recipe { Version = 0 };

        // Act
        var act = () => RecipeBounds.Validate(recipe);

        // Assert
        act.Should().Throw<RecipeException>().WithMessage("*version*");
    }

    [Fact]
    public void Validate_IgnoresAKnobTheRecipeDoesNotState()
    {
        // Arrange — an empty recipe states nothing, and nothing is not out of range
        var act = () => RecipeBounds.Validate(new Recipe());

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_AcceptsEveryBuiltIn()
    {
        // Arrange & Act — a built-in that violates its own schema would fail for everyone who names it
        var offending = RecipeLibrary.BuiltInNames
            .Where(name =>
            {
                try
                {
                    RecipeBounds.Validate(RecipeLibrary.Resolve(name).LayerOver(RecipeLibrary.Defaults()));
                    return false;
                }
                catch (RecipeException)
                {
                    return true;
                }
            })
            .ToList();

        // Assert
        offending.Should().BeEmpty();
    }

    [Fact]
    public void Validate_CoversEveryBoundTheSchemaStates()
    {
        // Arrange — the ranges are read from the schema rather than restated, so this checks the reading
        // works for all of them: a bound the walker cannot see is a bound that silently stops applying.
        var schema = JsonNode.Parse(RecipeLibrary.Schema())!;
        var bounded = new List<string>();

        foreach (var (areaName, areaSchema) in schema["properties"]!.AsObject())
        {
            if (areaSchema?["properties"] is not JsonObject fields)
            {
                if (areaSchema?["minimum"] is not null || areaSchema?["maximum"] is not null)
                    bounded.Add(areaName);

                continue;
            }

            foreach (var (fieldName, fieldSchema) in fields)
            {
                if (fieldSchema?["minimum"] is not null || fieldSchema?["maximum"] is not null)
                    bounded.Add($"{areaName}.{fieldName}");
            }
        }

        // Act — every bounded field, pushed a long way outside its range at once
        var offending = new List<string>();
        var wild = JsonNode.Parse("""
            {
              "version": -9,
              "timeline": { "companyAgeYears": -9, "teamStructureAgeYears": -9, "historyYears": -9, "runwayYears": -9 },
              "organization": { "deliveryRatio": 99, "valueStreams": -9, "teams": -9, "formerEmployeeFraction": 99 },
              "ppm": { "functionPortfolios": -9, "concurrentProjectsPerArt": -9, "concurrentProgramsPerPortfolio": -9 }
            }
            """)!;

        // Each bound is reached only once the ones before it pass, so they are peeled off one at a time:
        // whatever the message names is fixed and the walk repeats, until nothing is left to reject.
        var remaining = wild.Deserialize<Recipe>(RecipeLibrary.SerializerOptions)!;
        for (var attempt = 0; attempt < bounded.Count + 1; attempt++)
        {
            try
            {
                RecipeBounds.Validate(remaining);
                break;
            }
            catch (RecipeException failure)
            {
                var named = bounded.FirstOrDefault(field => failure.Message.Contains($"'{field}'"));
                named.Should().NotBeNull("every rejection has to name the field it is about");
                offending.Add(named!);
                Clear(wild, named!);
                remaining = wild.Deserialize<Recipe>(RecipeLibrary.SerializerOptions)!;
            }
        }

        // Assert — a bound the walker cannot see never rejects, and so never appears here
        bounded.Should().NotBeEmpty("the schema is expected to constrain the counts");
        offending.Should().BeEquivalentTo(bounded);
    }

    /// <summary>Removes one <c>area.field</c> or top-level value from the recipe's JSON.</summary>
    private static void Clear(JsonNode recipe, string path)
    {
        var parts = path.Split('.');
        if (parts.Length == 1)
            recipe.AsObject().Remove(parts[0]);
        else
            recipe[parts[0]]!.AsObject().Remove(parts[1]);
    }
}
