using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli.Generation;
using static Wayd.Tools.DataGeneration.Cli.Generation.ProductManagementVocabulary;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

/// <summary>
/// Which teams work on which products, checked against what the product import accepts — an active team
/// named by a code it holds — and against what a believable catalog looks like.
/// </summary>
public class ProductTeamLinkGeneratorTests
{
    private static readonly DateOnly _asOf = new(2026, 6, 15);

    private sealed record Run(
        GeneratedOrg Org,
        ProductCatalog Catalog,
        IReadOnlyDictionary<string, IReadOnlyList<string>> Links);

    private static Run Generate(double componentsPerTeam = ProductCatalog.BaselineComponentsPerTeam, int seed = 1234)
    {
        var context = new GenerationContext { AsOf = _asOf, Seed = seed };
        var org = new OrgGenerator(new OrgOptions { ValueStreams = 3, Teams = 18 }, context).Generate();
        var catalog = ProductCatalog.From(org.Structure, context, new ProductCatalogShape(componentsPerTeam, NamesHubs: false, PlatformServices: null));

        return new Run(org, catalog, new ProductTeamLinkGenerator(catalog, org.Structure, context).Generate());
    }

    private static readonly Run _default = Generate();

    private static IReadOnlyList<string> TeamsOn(Run run, string productName) =>
        run.Links.TryGetValue(productName, out var teams) ? teams : [];

    private static List<(ProductLinePlan Line, ArtProductPlan Product, ComponentPlan Component)> Components(Run run) =>
        [.. run.Catalog.Lines.SelectMany(l => l.Products.SelectMany(p => p.Components.Select(c => (l, p, c))))];

    [Fact]
    public void Generate_NamesOnlyActiveTeamsTheOrgHolds()
    {
        // Arrange — an unknown or inactive team fails its row, and a failed row keeps out its whole product tree
        var active = _default.Org.Teams.Where(t => t.IsActive).Select(t => t.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        var named = _default.Links.Values.SelectMany(t => t).ToList();

        // Assert
        named.Should().NotBeEmpty();
        named.Should().OnlyContain(code => active.Contains(code));
    }

    [Fact]
    public void Generate_LinksEveryWorkedComponentToTheTeamThatOwnsIt_AndNoRetiredOne()
    {
        // Act
        var components = Components(_default);

        // Assert
        components.Where(c => c.Component.Status != RetiredStatus)
            .Should().OnlyContain(c => TeamsOn(_default, c.Component.Name).Contains(c.Component.TeamCode));
        components.Where(c => c.Component.Status == RetiredStatus)
            .Should().NotBeEmpty()
            .And.OnlyContain(c => TeamsOn(_default, c.Component.Name).Count == 0);
    }

    [Fact]
    public void Generate_SharesSomeProductsBetweenTwoOrThreeTeams_AndNeverMore()
    {
        // Act
        var counts = Components(_default).Select(c => TeamsOn(_default, c.Component.Name).Count).ToList();

        // Assert
        counts.Should().Contain(2);
        counts.Should().Contain(3);
        counts.Should().OnlyContain(n => n <= 3);
        counts.Count(n => n == 1).Should().BeGreaterThan(counts.Count(n => n > 1), "most products are one team's");
    }

    [Fact]
    public void Generate_LinksTheMostSpecificProducts_AndTeamsOfTeamsOnlyToProductLines()
    {
        // Arrange — product pages roll links up to their ancestors, so a link on an ART's product says nothing
        var teamsOfTeams = _default.Org.Teams.Where(t => t.Type == "TeamOfTeams").Select(t => t.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var lines = _default.Catalog.Lines.Select(l => l.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var artProducts = _default.Catalog.Lines.SelectMany(l => l.Products).Select(p => p.Name).ToList();

        // Act
        var onLines = _default.Links.Where(l => lines.Contains(l.Key)).ToList();

        // Assert
        artProducts.Should().OnlyContain(name => TeamsOn(_default, name).Count == 0);
        onLines.Should().NotBeEmpty();
        onLines.Should().OnlyContain(l => l.Value.Count == 1 && teamsOfTeams.Contains(l.Value[0]));
        _default.Links.Where(l => !lines.Contains(l.Key)).SelectMany(l => l.Value)
            .Should().OnlyContain(code => !teamsOfTeams.Contains(code));
    }

    [Theory]
    [InlineData(ProductCatalog.BaselineComponentsPerTeam, 1.5, 3.0)]
    [InlineData(4.0, 4.0, 8.0)]
    public void Generate_GivesEveryDeliveryTeamAFewProducts_WithinOneProductLine(double componentsPerTeam, double fewest, double most)
    {
        // Arrange
        var run = Generate(componentsPerTeam);
        var lineByProduct = Components(run).ToDictionary(c => c.Component.Name, c => c.Line.Name, StringComparer.OrdinalIgnoreCase);
        var deliveryTeams = run.Org.Teams.Where(t => t.Type == "Team").Select(t => t.Code).ToList();

        // Act
        var byTeam = run.Links
            .Where(l => lineByProduct.ContainsKey(l.Key))
            .SelectMany(l => l.Value.Select(team => (Team: team, Product: l.Key)))
            .GroupBy(x => x.Team, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Product).ToList(), StringComparer.OrdinalIgnoreCase);

        // Assert
        deliveryTeams.Should().OnlyContain(t => byTeam.ContainsKey(t), "every delivery team works on something");
        byTeam.Values.Average(p => p.Count).Should().BeInRange(fewest, most);
        byTeam.Values.Count(p => p.Select(x => lineByProduct[x]).Distinct().Count() == 1)
            .Should().BeGreaterThan(byTeam.Count * 3 / 4, "a team's products are usually in its own line");
    }

    [Fact]
    public void Generate_LinksTheSameTeamsForTheSameSeedAndDate()
    {
        // Act
        var first = Generate(seed: 99).Links;
        var second = Generate(seed: 99).Links;

        // Assert
        second.Keys.Should().BeEquivalentTo(first.Keys);
        foreach (var (product, teams) in first)
            second[product].Should().Equal(teams);
    }
}
