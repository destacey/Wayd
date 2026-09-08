using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli;
using Wayd.Tools.DataGeneration.Cli.Recipes;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

/// <summary>
/// Every knob the recipe format states, reachable from the command line.
/// </summary>
/// <remarks>
/// The two front ends are only equals while this holds. A field with no flag can be set in the page and in
/// a recipe file but not on a command line, so the command the page prints silently stops reproducing what
/// is on screen — which is the one thing that command exists to do. Seven flags were being dropped that
/// way before this pairing was asserted rather than maintained.
/// </remarks>
public class GenerationOptionsTests
{
    private static readonly JsonNode _schema = JsonNode.Parse(RecipeLibrary.Schema())!;

    /// <summary>Every <c>area.field</c> the schema states, with the field's own schema.</summary>
    private static IEnumerable<(string Area, string Field, JsonObject Spec)> Fields()
    {
        foreach (var (area, areaSpec) in _schema["properties"]!.AsObject())
        {
            if (areaSpec?["properties"] is not JsonObject fields)
                continue;

            foreach (var (field, spec) in fields)
                yield return (area, field, spec!.AsObject());
        }
    }

    private static IReadOnlyList<string> Names(Option option) => [option.Name, .. option.Aliases];

    [Fact]
    public void All_ReachesEveryFieldTheSchemaStates()
    {
        // Arrange
        var registered = GenerationOptions.All.SelectMany(Names).ToHashSet(StringComparer.Ordinal);

        // Act — a field either names the flag that sets it, or says why it has none
        var unreachable = Fields()
            .Where(f => f.Spec["x-cli-no-flag"] is null)
            .Where(f => f.Spec["x-cli-flag"]?.GetValue<string>() is not { } flag || !registered.Contains(flag))
            .Select(f => $"{f.Area}.{f.Field}")
            .ToList();

        // Assert
        unreachable.Should().BeEmpty(
            "a field with no flag can be set in the page and in a file but not on a command line");
    }

    [Fact]
    public void All_ExemptsOnlyFieldsThatSayWhy()
    {
        // Arrange & Act — the exemption is a claim about the field, so it carries its reason
        var exempt = Fields()
            .Where(f => f.Spec["x-cli-no-flag"] is not null)
            .Select(f => ($"{f.Area}.{f.Field}", f.Spec["x-cli-no-flag"]!.GetValue<string>()))
            .ToList();

        // Assert — organization.enabled is the only one: it cannot be false, so a flag would express
        // nothing. A second exemption appearing here is a field that quietly left the command line.
        exempt.Should().ContainSingle().Which.Item1.Should().Be("organization.enabled");
        exempt.Should().OnlyContain(e => e.Item2.Length > 0);
    }

    [Fact]
    public void All_StatesEveryGenerationFlagInTheSchema()
    {
        // Arrange — the run's own inputs are not recipe fields: a recipe cannot name itself, and pinning
        // a seed inside one would make every run of it produce the same company
        string[] runInputs = ["--recipe", "--random-seed", "-r"];

        var declared = Fields()
            .Select(f => f.Spec["x-cli-flag"]?.GetValue<string>())
            .Where(flag => flag is not null)
            .ToHashSet(StringComparer.Ordinal);

        // Act
        var undeclared = GenerationOptions.All
            .Where(option => !runInputs.Contains(option.Name))
            .Where(option => !declared.Contains(option.Name))
            .Select(option => option.Name)
            .ToList();

        // Assert — an undeclared flag is invisible to the page, which builds its command from the schema
        undeclared.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(FlaggedFields))]
    public void Compose_CarriesTheFlagIntoTheRecipe(string area, string field)
    {
        // Arrange — a flag can be registered, documented, and still never read; this settles that by
        // passing each one and reading the composed recipe back
        var spec = Fields().Single(f => f.Area == area && f.Field == field).Spec;
        var flag = spec["x-cli-flag"]!.GetValue<string>();
        var negates = spec["x-cli-flag-negates"]?.GetValue<bool>() == true;
        var (argument, expected) = Sample(spec);

        var command = new Command("generate");
        GenerationOptions.AddTo(command);

        // Act
        var parse = command.Parse(negates ? [flag] : [flag, argument]);
        parse.Errors.Should().BeEmpty();

        var composed = JsonSerializer.SerializeToNode(
            GenerationOptions.Compose(parse), RecipeLibrary.SerializerOptions)!;

        // Assert
        var stated = composed[area]?[field];
        stated.Should().NotBeNull($"{flag} should have set {area}.{field}");
        stated!.ToJsonString().TrimStart('"').Should().StartWith(expected);
    }

    public static TheoryData<string, string> FlaggedFields()
    {
        var data = new TheoryData<string, string>();
        foreach (var (area, field, spec) in Fields().Where(f => f.Spec["x-cli-flag"] is not null))
            data.Add(area, field);

        return data;
    }

    /// <summary>
    /// A value a field will accept, and how it reads once composed.
    /// </summary>
    /// <remarks>
    /// Drawn from the field's own schema rather than listed per field, so a knob added to the format is
    /// covered by this the moment it declares a flag. Counts sit one above their minimum so the value is
    /// distinguishable from the bound itself.
    /// </remarks>
    private static (string Argument, string Expected) Sample(JsonObject spec)
    {
        if (spec["x-cli-flag-negates"]?.GetValue<bool>() == true)
            return (string.Empty, "false");

        if (spec["enum"] is JsonArray choices)
            return (choices[^1]!.GetValue<string>(), choices[^1]!.GetValue<string>());

        if (spec["format"]?.GetValue<string>() == "date")
            return ("2026-06-15", "2026-06-15");

        // A nullable field states its type as a list, so this is not always a single value.
        var type = spec["type"] is JsonValue single ? single.GetValue<string>() : null;
        if (type == "integer")
        {
            var value = (spec["minimum"]?.GetValue<int>() ?? 0) + 1;
            return (value.ToString(), value.ToString());
        }

        if (type == "number")
            return ("0.25", "0.25");

        // The only remaining string is the account password, which has to satisfy the password rules.
        return ("Sample123", "Sample123");
    }

    [Fact]
    public void Compose_LeavesAFieldAloneWhenItsFlagIsAbsent()
    {
        // Arrange — the layering rests on absent meaning absent, which is why no option carries a default
        var command = new Command("generate");
        GenerationOptions.AddTo(command);

        // Act
        var withFlag = GenerationOptions.Compose(command.Parse(["--recipe", "large-tech", "--teams", "7"]));
        var without = GenerationOptions.Compose(command.Parse(["--recipe", "large-tech"]));

        // Assert
        withFlag.Organization!.Teams.Should().Be(7);
        without.Organization!.Teams.Should().NotBe(7, "the recipe's own value should show through");
        without.Organization!.ValueStreams.Should().Be(withFlag.Organization!.ValueStreams);
    }
}
