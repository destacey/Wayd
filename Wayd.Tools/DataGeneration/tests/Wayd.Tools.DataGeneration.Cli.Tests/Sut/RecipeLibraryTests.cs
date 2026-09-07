using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli.Generation;
using Wayd.Tools.DataGeneration.Cli.Recipes;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

/// <summary>
/// Finding and resolving recipes: the built-ins, files on disk, and the <c>extends</c> chain between them.
/// </summary>
public class RecipeLibraryTests : IDisposable
{
    private readonly List<string> _files = [];

    public void Dispose()
    {
        foreach (var file in _files)
            File.Delete(file);

        GC.SuppressFinalize(this);
    }

    /// <summary>Writes a recipe to a temp file, so a test can exercise the file path as well as the names.</summary>
    private string File_(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"recipe-{Guid.CreateVersion7():N}.json");
        File.WriteAllText(path, json);
        _files.Add(path);

        return path;
    }

    [Fact]
    public void BuiltInNames_IncludesTheDefaultEveryRunStartsFrom()
    {
        // Arrange & Act & Assert — every other layer sits on this one, so its absence is not a missing
        // convenience but a run with no values at all
        RecipeLibrary.BuiltInNames.Should().Contain(RecipeLibrary.DefaultRecipeName);
    }

    [Fact]
    public void Defaults_SettleEveryKnob()
    {
        // Arrange & Act — the guarantee the whole nullable design leans on: a recipe may decline to state
        // anything, so the bottom layer has to state everything. Adding a knob to the model and not to
        // default.json would otherwise surface as a run-time error on somebody else's machine.
        var act = () => ResolvedRecipe.From(RecipeLibrary.Defaults(), seed: 1);

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("default")]
    [InlineData("startup")]
    [InlineData("large-tech")]
    [InlineData("enterprise")]
    [InlineData("org-only")]
    [InlineData("demo")]
    public void EveryBuiltIn_ResolvesIntoACompleteRun(string name)
    {
        // Arrange & Act — a built-in that cannot be resolved is broken for everyone who names it, and it
        // ships inside the assembly, so nothing but a test catches it
        var act = () => ResolvedRecipe.From(RecipeLibrary.Resolve(name).LayerOver(RecipeLibrary.Defaults()), seed: 1);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void EveryBuiltIn_DescribesItself()
    {
        // Arrange & Act — the description is what `wayd-data recipes` prints, so a missing one shows as a
        // blank line next to a name
        var undescribed = RecipeLibrary.BuiltInNames
            .Where(n => string.IsNullOrWhiteSpace(RecipeLibrary.Resolve(n).Description))
            .ToList();

        // Assert
        undescribed.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_LayersAChildOverWhatItExtends()
    {
        // Arrange — startup states its own sizes and inherits the rest
        var startup = RecipeLibrary.Resolve("startup");

        // Act
        var resolved = ResolvedRecipe.From(startup.LayerOver(RecipeLibrary.Defaults()), seed: 1);

        // Assert
        resolved.Organization.Teams.Should().Be(4, "startup states its own team count");
        resolved.Organization.CompanyType.Should().Be(CompanyType.Tech, "it says nothing about company type, so default's shows through");
    }

    [Fact]
    public void Resolve_ReadsARecipeFromAFilePath()
    {
        // Arrange
        var path = File_("""{ "extends": "default", "organization": { "teams": 7 } }""");

        // Act
        var resolved = ResolvedRecipe.From(RecipeLibrary.Resolve(path), seed: 1);

        // Assert
        resolved.Organization.Teams.Should().Be(7);
    }

    [Fact]
    public void Resolve_RejectsAKeyItDoesNotRecognise()
    {
        // Arrange — the worst failure this format could have is a typo that parses cleanly and does
        // nothing, leaving the run to succeed while quietly ignoring what was asked for
        var path = File_("""{ "organization": { "teemz": 40 } }""");

        // Act
        var act = () => RecipeLibrary.Resolve(path);

        // Assert — and it has to name the key, or the file has to be read line by line to find it
        act.Should().Throw<RecipeException>().WithMessage("*teemz*");
    }

    [Fact]
    public void Resolve_RejectsANameThatIsNeitherBuiltInNorAFile()
    {
        // Arrange & Act
        var act = () => RecipeLibrary.Resolve("no-such-recipe");

        // Assert — listing what does exist is the difference between a dead end and a next step
        act.Should().Throw<RecipeException>()
            .WithMessage("*no-such-recipe*")
            .WithMessage($"*{RecipeLibrary.DefaultRecipeName}*");
    }

    [Fact]
    public void Resolve_RejectsARecipeExtendingSomethingThatDoesNotExist()
    {
        // Arrange
        var path = File_("""{ "extends": "no-such-parent" }""");

        // Act
        var act = () => RecipeLibrary.Resolve(path);

        // Assert
        act.Should().Throw<RecipeException>().WithMessage("*no-such-parent*");
    }

    [Fact]
    public void Resolve_RefusesAnExtendsCycle()
    {
        // Arrange — two files pointing at each other. Without the check this recurses until the stack
        // gives out, which reports nothing about the recipes involved.
        var first = Path.Combine(Path.GetTempPath(), $"recipe-{Guid.CreateVersion7():N}.json");
        var second = Path.Combine(Path.GetTempPath(), $"recipe-{Guid.CreateVersion7():N}.json");
        File.WriteAllText(first, $$"""{ "extends": {{System.Text.Json.JsonSerializer.Serialize(second)}} }""");
        File.WriteAllText(second, $$"""{ "extends": {{System.Text.Json.JsonSerializer.Serialize(first)}} }""");
        _files.Add(first);
        _files.Add(second);

        // Act
        var act = () => RecipeLibrary.Resolve(first);

        // Assert
        act.Should().Throw<RecipeException>().WithMessage("*extends itself*");
    }

    [Fact]
    public void Resolve_PrefersABuiltInOverAFileOfTheSameName()
    {
        // Arrange — a stray default.json in the working directory must not shadow the shipped defaults,
        // or a run picks up a different bottom layer depending on where it was started from
        var stray = Path.Combine(Directory.GetCurrentDirectory(), "default.json");
        File.WriteAllText(stray, """{ "organization": { "teams": 999 } }""");
        _files.Add(stray);

        // Act
        var resolved = ResolvedRecipe.From(RecipeLibrary.Resolve("default"), seed: 1);

        // Assert
        resolved.Organization.Teams.Should().NotBe(999);
    }

    [Fact]
    public void EveryBuiltIn_PointsAtTheSchemaByUrl()
    {
        // Arrange — `recipes show <name> > mine.json` is the advertised way to start a custom recipe, and
        // it carries this value with it. A path relative to where the built-ins live in this repo resolves
        // nowhere once the output is saved somewhere else, so the editor stops completing and validating
        // on exactly the file that most needs it.
        var missing = RecipeLibrary.BuiltInNames
            .Where(n => RecipeLibrary.Resolve(n).Schema?.StartsWith("https://", StringComparison.Ordinal) != true)
            .ToList();

        // Assert
        missing.Should().BeEmpty();
    }

    [Fact]
    public void Schema_MatchesTheCopyTheDocsSitePublishes()
    {
        // Arrange — the built-ins name an absolute URL, which only resolves because the docs site serves
        // a copy from its static folder. Two files, so they can drift; this is what notices.
        var published = Path.Combine(
            RepositoryRoot(), "docs-site", "static", "schemas", "wayd-data", "recipe.schema.json");

        // Act
        var onDisk = File.ReadAllText(published).ReplaceLineEndings();

        // Assert
        onDisk.Should().Be(RecipeLibrary.Schema().ReplaceLineEndings());
    }

    /// <summary>Walks up from the test binary until the repository root is in hand.</summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Wayd.slnx")))
            directory = directory.Parent;

        directory.Should().NotBeNull("the tests run from inside the repository");

        return directory!.FullName;
    }

    [Fact]
    public void Schema_IsShippedAlongsideTheBuiltIns()
    {
        // Arrange & Act — `recipes schema` writes this out, and the built-ins point at it
        var schema = RecipeLibrary.Schema();

        // Assert
        schema.Should().Contain("wayd-data recipe");
    }
}
