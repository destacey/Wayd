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
        var recipe = new Recipe { Timeline = new TimelineRecipe { AsOf = new DateOnly(2026, 6, 15) } };

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
            "deployment-environments.csv", "products.csv", "versions.csv", "release-packages.csv",
            "release-package-components.csv", "releases.csv", "release-contents.csv", "deployments.csv",
            "product-dependencies.csv",
            "planning-intervals.csv", "planning-interval-objectives.csv", "risks.csv",
        ]);
    }

    [Fact]
    public void WriteTo_WritesOnlyTheOrganizationWhenTheOtherAreasAreDisabled()
    {
        // Arrange — the org-only shape, which has no PPM, Product Management or Planning model to write at all
        var recipe = new Recipe
        {
            Ppm = new PpmRecipe { Enabled = false },
            ProductManagement = new ProductManagementRecipe { Enabled = false },
            Planning = new PlanningRecipe { Enabled = false },
        };

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
    public void Counts_ReportZeroForProductManagementWhenItDidNotRun()
    {
        // Arrange & Act
        var dataset = GeneratedDataset.From(Resolve(new Recipe { ProductManagement = new ProductManagementRecipe { Enabled = false } }));

        // Assert — PPM still ran, so the zeros are this area's alone
        dataset.Counts.Projects.Should().BeGreaterThan(0);
        dataset.Counts.Products.Should().Be(0);
        dataset.Counts.Versions.Should().Be(0);
        dataset.Counts.ReleasePackages.Should().Be(0);
        dataset.Counts.Releases.Should().Be(0);
        dataset.Counts.Deployments.Should().Be(0);
    }

    [Fact]
    public void Counts_ReportZeroForPlanningWhenItDidNotRun()
    {
        // Arrange & Act
        var dataset = GeneratedDataset.From(Resolve(new Recipe { Planning = new PlanningRecipe { Enabled = false } }));

        // Assert — the other areas still ran, so the zeros are this area's alone
        dataset.Counts.Projects.Should().BeGreaterThan(0);
        dataset.Counts.Products.Should().BeGreaterThan(0);
        dataset.Counts.PlanningIntervals.Should().Be(0);
        dataset.Counts.Objectives.Should().Be(0);
        dataset.Counts.Risks.Should().Be(0);
    }

    [Fact]
    public void From_NamesProjectsAfterTheProductsTheCatalogHolds()
    {
        // Arrange — the two areas derive the catalog separately, so this is what proves they agree
        var dataset = GeneratedDataset.From(Resolve());
        var products = dataset.ProductManagement!.Products.Select(p => p.Name).ToList();

        // Act
        var namedForAProduct = dataset.Ppm!.Projects
            .Count(project => products.Any(product => project.Name.EndsWith($" {product}", StringComparison.Ordinal)));

        // Assert — most ART projects work on something their team builds
        namedForAProduct.Should().BeGreaterThan(dataset.Ppm.Projects.Count / 3);
    }

    // ---- People across areas ------------------------------------------------------------------
    //
    // Attrition means the org holds people who have left, and every area names people. These are the rules
    // that keep the two coherent, checked over everything a seed would post rather than per generator.

    private static readonly DateOnly _asOf = new(2026, 6, 15);

    private static GeneratedDataset WithAttrition(Recipe? recipe = null) => GeneratedDataset.From(Resolve(
        new Recipe
        {
            Timeline = new TimelineRecipe { AsOf = _asOf },
            Organization = new OrganizationRecipe { AttritionRate = 0.2, ArtTier = recipe?.Organization?.ArtTier },
            Ppm = recipe?.Ppm,
        }));

    private static IEnumerable<string> People(params string?[] columns) =>
        columns.SelectMany(c => string.IsNullOrWhiteSpace(c) ? [] : c.Split(';'));

    [Fact]
    public void From_NamesOnlyPeopleStillEmployedOnOpenWork()
    {
        // Arrange — open work led by someone inactive is a record nobody can manage, since PPM mutation needs
        // delivery leadership held by a person who can still sign in
        var dataset = WithAttrition();
        var ppm = dataset.Ppm!;
        var inactive = dataset.Org.Employees.Where(e => !e.IsActive).Select(e => e.EmployeeNumber).ToHashSet();
        var finalized = ppm.Finalizations.Select(f => f.Name).ToHashSet();

        // Act
        var named = ppm.Projects.Where(p => p.Status is not ("Completed" or "Canceled"))
                .SelectMany(p => People(p.Sponsors, p.Owners, p.Managers, p.Members).Select(n => (Record: p.Key, Person: n)))
            .Concat(ppm.Programs.Where(p => !finalized.Contains(p.Name))
                .SelectMany(p => People(p.Sponsors, p.Owners, p.Managers).Select(n => (Record: p.Name, Person: n))))
            .Concat(ppm.Portfolios.Where(p => !finalized.Contains(p.Name))
                .SelectMany(p => People(p.Sponsors, p.Owners, p.Managers).Select(n => (Record: p.Name, Person: n))))
            .Concat(ppm.StrategicInitiatives.Where(i => i.Status is not ("Completed" or "Canceled"))
                .SelectMany(i => People(i.Sponsors, i.Owners).Select(n => (Record: i.Name, Person: n))))
            .Concat(ppm.ProjectTasks.Where(t => t.Status != "Completed")
                .SelectMany(t => People(t.Assignees).Select(n => (Record: $"{t.ProjectKey}/{t.Name}", Person: n))))
            .Concat(dataset.Planning!.Risks.Where(r => r.Status != "Closed")
                .SelectMany(r => People(r.AssigneeEmployeeNumber).Select(n => (Record: r.Handle, Person: n))))
            .ToList();

        // Assert
        inactive.Should().NotBeEmpty();
        named.Where(n => inactive.Contains(n.Person)).Should().BeEmpty();
    }

    [Fact]
    public void From_NamesPeopleOnFinishedWorkWhoWereThereWhileItRan()
    {
        // Arrange — the fidelity attrition exists for: completed work owned by people who have since gone,
        // and never by someone who joined after it finished or left before it began
        var dataset = WithAttrition();
        var positions = dataset.Org.Structure.Positions!;
        Tenure TenureOf(string employeeNumber) => positions[employeeNumber].Single(t => t.EmployeeNumber == employeeNumber);

        var closed = dataset.Ppm!.Projects.Where(p => p.Status is "Completed" or "Canceled").ToList();

        // Act
        var outside = closed
            .SelectMany(p => People(p.Sponsors, p.Owners, p.Managers, p.Members).Select(n => (Project: p, Tenure: TenureOf(n))))
            .Where(x => x.Tenure.HiredOn > x.Project.End || x.Tenure.LeftOn < x.Project.Start)
            .Select(x => $"{x.Project.Key} ({x.Project.Start}..{x.Project.End}) names {x.Tenure}")
            .ToList();

        var reportedBeforeJoining = dataset.Planning!.Risks
            .Where(r => TenureOf(r.ReportedByEmployeeNumber).HiredOn > DateOnly.FromDateTime(r.ReportedAt.UtcDateTime))
            .Select(r => r.Handle)
            .ToList();

        // Assert
        closed.SelectMany(p => People(p.Owners)).Where(n => TenureOf(n).LeftOn != null).Should().NotBeEmpty(
            "a finished project owned by someone who has since left is what this is meant to produce");
        outside.Should().BeEmpty();
        reportedBeforeJoining.Should().BeEmpty();
    }

    [Fact]
    public void From_WithArtsAndProgramsOff_StillStaffsEveryArea()
    {
        // Arrange — the areas that grouped work by ART plan and ship by value stream instead
        var recipe = new Recipe
        {
            Organization = new OrganizationRecipe { ArtTier = Generation.StructureMode.Off },
            Ppm = new PpmRecipe { Programs = Generation.StructureMode.Off },
        };

        // Act
        var dataset = WithAttrition(recipe);
        var withArts = WithAttrition();

        // Assert
        dataset.Ppm!.Programs.Should().BeEmpty();
        dataset.Ppm.Projects.Should().OnlyContain(p => p.ProgramName == null);
        dataset.Ppm.Portfolios.Should().OnlyContain(p => p.Owners != null && p.Sponsors != null);
        dataset.Ppm.Projects.Count.Should().BeGreaterThan(withArts.Ppm!.Projects.Count / 2, "a value stream's teams carry the load of the ARTs they replaced");
        dataset.Planning!.PlanningIntervals.Should().OnlyContain(p => p.ArtCode == null);
        dataset.Planning.PlanningIntervals.Select(p => p.Name).Should().OnlyHaveUniqueItems();
        dataset.ProductManagement!.ReleasePackages.Select(p => p.Version).Should().OnlyHaveUniqueItems();
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
