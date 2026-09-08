using System.Text.Json;
using System.Text.Json.Nodes;

namespace Wayd.Tools.DataGeneration.Cli.Recipes;

/// <summary>
/// Checks a recipe's numbers against the ranges the schema states.
/// </summary>
/// <remarks>
/// Without this the generators quietly cope. <c>Math.Max(1, valueStreams)</c> and its neighbours turn a
/// zero into a one and carry on, so a recipe asking for no teams produces a company with some and says
/// nothing — the same silent-ignore failure that unmapped-member handling exists to prevent, one layer
/// down. A number outside its range is a mistake worth naming.
/// <para>
/// The ranges are read from the published schema rather than restated here. They are already written
/// down once, the form takes its <c>min</c> and <c>max</c> from the same place, and a bound that lived in
/// two files would drift the first time one was widened.
/// </para>
/// </remarks>
public static class RecipeBounds
{
    private static readonly Lazy<JsonNode> _schema = new(() =>
        JsonNode.Parse(RecipeLibrary.Schema())
            ?? throw new RecipeException("The recipe schema could not be read."));

    /// <summary>
    /// Throws when a number the recipe states falls outside the schema's range for it, whether it sits in
    /// an area or at the top of the recipe.
    /// </summary>
    /// <remarks>
    /// Compared through the recipe's own JSON rather than property by property, so a knob added to the
    /// model and the schema is covered without being listed here as well.
    /// </remarks>
    public static void Validate(Recipe recipe)
    {
        var stated = JsonSerializer.SerializeToNode(recipe, RecipeLibrary.SerializerOptions);
        if (stated is null || _schema.Value["properties"] is not JsonObject areas)
            return;

        foreach (var (name, propertySchema) in areas)
        {
            if (propertySchema is null)
                continue;

            // An area, whose own properties carry the bounds; or a value at the top of the recipe, which
            // carries them itself. Both are checked, so a bound is enforced wherever the schema states one
            // rather than only where the format happens to nest.
            if (propertySchema["properties"] is JsonObject fields)
            {
                if (stated[name] is not JsonObject area)
                    continue;

                foreach (var (fieldName, fieldSchema) in fields)
                    CheckStated(area, fieldName, fieldSchema, $"{name}.{fieldName}");
            }
            else
            {
                CheckStated(stated, name, propertySchema, name);
            }
        }
    }

    private static void CheckStated(JsonNode container, string field, JsonNode? schema, string path)
    {
        if (schema is null || container[field] is not JsonValue value || !value.TryGetValue<double>(out var number))
            return;

        Check(number, schema["minimum"], schema["maximum"], path);
    }

    private static void Check(double value, JsonNode? minimum, JsonNode? maximum, string field)
    {
        if (minimum?.GetValue<double>() is { } least && value < least)
        {
            throw new RecipeException(
                $"'{field}' is {Format(value)}, and the smallest it may be is {Format(least)}.");
        }

        if (maximum?.GetValue<double>() is { } most && value > most)
        {
            throw new RecipeException(
                $"'{field}' is {Format(value)}, and the largest it may be is {Format(most)}.");
        }
    }

    /// <summary>Whole numbers read as whole numbers, since most of these knobs are counts.</summary>
    private static string Format(double value) =>
        value == Math.Floor(value) ? ((long)value).ToString() : value.ToString("0.###");
}
