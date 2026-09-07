using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wayd.Tools.DataGeneration.Cli.Recipes;

/// <summary>Raised when a recipe cannot be found, parsed, or resolved.</summary>
public sealed class RecipeException(string message) : Exception(message);

/// <summary>
/// Finds recipes — the built-in ones shipped with the tool, and files on disk — and resolves what a run
/// should actually use.
/// </summary>
/// <remarks>
/// Built-ins are embedded JSON of exactly the same shape a hand-written file has, and are read through the
/// same parser. There is no privileged path: a custom recipe can express anything a built-in can, by
/// construction rather than by promise.
/// </remarks>
public static class RecipeLibrary
{
    /// <summary>The recipe every run starts from when none is named.</summary>
    public const string DefaultRecipeName = "default";

    private const string ResourcePrefix = "Wayd.Tools.DataGeneration.Cli.Recipes.BuiltIn.";

    /// <summary>
    /// How recipe JSON is read, and the reason a typo is an error rather than a silent no-op.
    /// </summary>
    /// <remarks>
    /// <see cref="JsonUnmappedMemberHandling.Disallow"/> is the important one. Without it
    /// <c>"teemz": 40</c> parses cleanly and does nothing, which is the worst failure this format can
    /// have: the run succeeds and quietly ignores what you asked for. Comments are allowed because a
    /// recipe is a file a person edits and annotates.
    /// </remarks>
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>The names of every built-in recipe, in alphabetical order.</summary>
    public static IReadOnlyList<string> BuiltInNames =>
        [.. typeof(RecipeLibrary).Assembly
            .GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal))
            .Select(n => n[ResourcePrefix.Length..^".json".Length])
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)];

    /// <summary>Whether a built-in of this name exists.</summary>
    public static bool IsBuiltIn(string name) =>
        BuiltInNames.Contains(name, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves a recipe by built-in name or file path, with its <c>extends</c> chain already layered in.
    /// </summary>
    /// <remarks>
    /// A built-in name is tried first, so shipping a new built-in cannot be shadowed by a stray file of
    /// that name in the working directory.
    /// </remarks>
    public static Recipe Resolve(string nameOrPath) => Resolve(nameOrPath, []);

    /// <summary>The shipped defaults every other layer sits on top of.</summary>
    public static Recipe Defaults() => Resolve(DefaultRecipeName);

    private static Recipe Resolve(string nameOrPath, List<string> chain)
    {
        // Named rather than positional, because the cycle is what the message has to show.
        if (chain.Any(step => string.Equals(step, nameOrPath, StringComparison.OrdinalIgnoreCase)))
            throw new RecipeException($"Recipe '{nameOrPath}' extends itself: {string.Join(" -> ", chain)} -> {nameOrPath}.");

        chain.Add(nameOrPath);

        var recipe = Read(nameOrPath);
        if (recipe.Extends is not { } parent)
            return recipe;

        if (!IsBuiltIn(parent) && !File.Exists(parent))
            throw new RecipeException($"Recipe '{nameOrPath}' extends '{parent}', which is not a built-in recipe or an existing file.");

        return recipe.LayerOver(Resolve(parent, chain));
    }

    private static Recipe Read(string nameOrPath)
    {
        var json = ReadBuiltIn(nameOrPath) ?? ReadFile(nameOrPath);

        try
        {
            return JsonSerializer.Deserialize<Recipe>(json, SerializerOptions)
                ?? throw new RecipeException($"Recipe '{nameOrPath}' is empty.");
        }
        catch (JsonException ex)
        {
            throw new RecipeException($"Recipe '{nameOrPath}' could not be read: {ex.Message}");
        }
    }

    /// <summary>The JSON schema recipe files declare, shipped alongside the built-ins.</summary>
    public static string Schema()
    {
        using var stream = typeof(RecipeLibrary).Assembly
            .GetManifestResourceStream("Wayd.Tools.DataGeneration.Cli.Recipes.recipe.schema.json")!;
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    private static string? ReadBuiltIn(string name)
    {
        var resource = typeof(RecipeLibrary).Assembly
            .GetManifestResourceNames()
            .FirstOrDefault(n => string.Equals(n, $"{ResourcePrefix}{name}.json", StringComparison.OrdinalIgnoreCase));

        if (resource is null)
            return null;

        using var stream = typeof(RecipeLibrary).Assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    private static string ReadFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new RecipeException(
                $"No recipe named '{path}', and no file at that path. Built-in recipes: {string.Join(", ", BuiltInNames)}.");
        }

        return File.ReadAllText(path);
    }
}
