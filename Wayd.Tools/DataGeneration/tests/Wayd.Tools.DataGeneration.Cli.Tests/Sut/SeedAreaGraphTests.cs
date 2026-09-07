using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli.Seeding;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

public class SeedAreaGraphTests
{
    /// <summary>A stand-in area: the graph only reads the name and the dependencies.</summary>
    private sealed record FakeArea(string Name, IReadOnlyList<string> DependsOn) : ISeedArea
    {
        public bool ShouldRun(SeedContext context) => true;

        public Task Run(SeedContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static ISeedArea Area(string name, params string[] dependsOn) => new FakeArea(name, dependsOn);

    private static IReadOnlyList<string> OrderOf(params ISeedArea[] areas) =>
        [.. SeedAreaGraph.Order(areas).Select(a => a.Name)];

    [Fact]
    public void Order_PutsEveryAreaAfterWhatItDependsOn()
    {
        // Arrange — registered in the reverse of the order they have to run in
        var areas = new[]
        {
            Area("projects", "portfolios"),
            Area("portfolios", "employees"),
            Area("employees"),
        };

        // Act
        var ordered = OrderOf(areas);

        // Assert
        ordered.Should().Equal("employees", "portfolios", "projects");
    }

    [Fact]
    public void Order_KeepsRegistrationOrderBetweenAreasTheGraphLeavesFree()
    {
        // Arrange — neither depends on the other, so nothing but registration decides
        var areas = new[] { Area("teams"), Area("employees") };

        // Act
        var ordered = OrderOf(areas);

        // Assert — a seed run has to read the same way twice, so ties cannot fall out of a hash
        ordered.Should().Equal("teams", "employees");
    }

    [Fact]
    public void Order_PlacesAnAreaAfterEveryOneOfItsDependencies()
    {
        // Arrange — projects wait on three things registered at different depths
        var areas = new[]
        {
            Area("projects", "portfolios", "programs", "settings"),
            Area("programs", "portfolios"),
            Area("settings"),
            Area("portfolios"),
        };

        // Act
        var ordered = OrderOf(areas);

        // Assert
        ordered.Should().HaveCount(4);
        ordered.ToList().IndexOf("projects").Should().BeGreaterThan(ordered.ToList().IndexOf("programs"));
        ordered.ToList().IndexOf("projects").Should().BeGreaterThan(ordered.ToList().IndexOf("settings"));
        ordered.ToList().IndexOf("programs").Should().BeGreaterThan(ordered.ToList().IndexOf("portfolios"));
    }

    [Fact]
    public void Order_RefusesADependencyOnAnAreaThatDoesNotExist()
    {
        // Arrange — the likeliest mistake when adding an area is a mistyped dependency, which would
        // otherwise run the area too early and fail much further along
        var areas = new[] { Area("projects", "portfolioz"), Area("portfolios") };

        // Act
        var act = () => SeedAreaGraph.Order(areas);

        // Assert
        act.Should().Throw<SeedException>().WithMessage("*'projects' depends on 'portfolioz'*");
    }

    [Fact]
    public void Order_RefusesACycle()
    {
        // Arrange
        var areas = new[] { Area("a", "b"), Area("b", "a") };

        // Act
        var act = () => SeedAreaGraph.Order(areas);

        // Assert
        act.Should().Throw<SeedException>().WithMessage("*dependency cycle*");
    }

    [Fact]
    public void Order_RefusesTwoAreasWithTheSameName()
    {
        // Arrange — a duplicate name would silently overwrite the ids the first area published
        var areas = new[] { Area("projects"), Area("projects") };

        // Act
        var act = () => SeedAreaGraph.Order(areas);

        // Assert
        act.Should().Throw<SeedException>().WithMessage("*both named 'projects'*");
    }
}
