using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli.Generation;
using Wayd.Tools.DataGeneration.Cli.Recipes;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

/// <summary>
/// One resolved recipe, generated and written. Both front ends go through here, which is what makes "the
/// page shows the command that would do this" true rather than intended.
/// </summary>
public class GeneratedDatasetTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"wayd-data-{Guid.CreateVersion7():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);

        GC.SuppressFinalize(this);
    }

    private static ResolvedRecipe Resolve(Recipe? recipe = null, int seed = 4242) =>
        ResolvedRecipe.From((recipe ?? new Recipe()).LayerOver(RecipeLibrary.Defaults()), seed);

    [Fact]
    public void From_ProducesTheSameDatasetForTheSameInputs()
    {
        // Arrange — the guarantee the two front ends rest on: the page and the CLI resolve the same
        // recipe and must get the same company, or the command the page prints is a lie
        var recipe = new Recipe { Timeline = new TimelineRecipe { AsOf = new DateTime(2026, 6, 15) } };

        // Act
        var first = GeneratedDataset.From(Resolve(recipe));
        var second = GeneratedDataset.From(Resolve(recipe));

        // Assert
        second.Counts.Should().Be(first.Counts);
        second.Org.Employees.Select(e => e.Email).Should().Equal(first.Org.Employees.Select(e => e.Email));
    }

    [Fact]
    public void WriteTo_WritesEveryFileASeedWouldPost()
    {
        // Arrange & Act
        GeneratedDataset.From(Resolve()).WriteTo(_directory);

        // Assert — named rather than counted, so a file quietly dropped from the writer is caught
        Directory.GetFiles(_directory).Select(Path.GetFileName).Should().BeEquivalentTo(
        [
            "employees.csv", "teams.csv", "team-memberships.csv", "members.csv",
            "strategic-themes.csv", "portfolios.csv", "programs.csv", "projects.csv",
            "project-tasks.csv", "project-stages.csv", "strategic-initiatives.csv",
            "strategic-initiative-kpis.csv", "ppm-finalizations.csv",
        ]);
    }

    [Fact]
    public void WriteTo_WritesOnlyTheOrganizationWhenPpmIsDisabled()
    {
        // Arrange — the org-only shape, which has no PPM model to write at all
        var recipe = new Recipe { Ppm = new PpmRecipe { Enabled = false } };

        // Act
        GeneratedDataset.From(Resolve(recipe)).WriteTo(_directory);

        // Assert
        Directory.GetFiles(_directory).Select(Path.GetFileName).Should().BeEquivalentTo(
            ["employees.csv", "teams.csv", "team-memberships.csv", "members.csv"]);
    }

    [Fact]
    public void Counts_ReportZeroForAnAreaThatDidNotRun()
    {
        // Arrange & Act — the page renders these, and a missing area has to read as none rather than as
        // an absent number
        var dataset = GeneratedDataset.From(Resolve(new Recipe { Ppm = new PpmRecipe { Enabled = false } }));

        // Assert
        dataset.Counts.Employees.Should().BeGreaterThan(0);
        dataset.Counts.Projects.Should().Be(0);
        dataset.Counts.Portfolios.Should().Be(0);
    }

    [Fact]
    public void WriteTo_CreatesTheDirectoryItIsGiven()
    {
        // Arrange — the page lets someone type a folder that does not exist yet
        var nested = Path.Combine(_directory, "nested", "deeper");

        // Act
        GeneratedDataset.From(Resolve()).WriteTo(nested);

        // Assert
        File.Exists(Path.Combine(nested, "employees.csv")).Should().BeTrue();
    }
}
