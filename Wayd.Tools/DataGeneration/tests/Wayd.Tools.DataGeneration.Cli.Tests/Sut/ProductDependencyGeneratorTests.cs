using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli.Generation;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

/// <summary>
/// The generated dependency graph, checked against the rules the dependency import enforces and against the
/// shape a real estate's graph has. A row breaking an import rule fails its whole product at seed time, far
/// from the code that produced it.
/// </summary>
public class ProductDependencyGeneratorTests
{
    private static readonly DateOnly _asOf = new(2026, 6, 15);

    private static GenerationContext Context(int seed = 1234) => new() { AsOf = _asOf, Seed = seed };

    private sealed record Estate(ProductCatalog Catalog, IReadOnlyList<ProductDependencyModel> Links, GenerationContext Context);

    /// <summary>
    /// An estate of a few hundred components over six value streams, each component having first shipped
    /// when <paramref name="firstShipped"/> says — by default, at the start of the history.
    /// </summary>
    private static Estate Generate(
        int seed = 1234,
        ProductManagementOptions? options = null,
        Func<ComponentPlan, DateOnly?>? firstShipped = null,
        int valueStreams = 6,
        int teams = 60)
    {
        var context = Context(seed);
        options ??= new ProductManagementOptions { ComponentsPerTeam = 4 };

        var org = new OrgGenerator(new OrgOptions { ValueStreams = valueStreams, Teams = teams }, context).Generate();
        var catalog = ProductCatalog.From(org.Structure, context, ProductCatalogShape.For(options));

        var shipped = catalog.Lines
            .SelectMany(l => l.Products)
            .SelectMany(p => p.Components)
            .Select(c => (c.Name, On: firstShipped is null ? context.WindowStart : firstShipped(c)))
            .Where(c => c.On is not null)
            .ToDictionary(c => c.Name, c => c.On!.Value, StringComparer.OrdinalIgnoreCase);

        var links = new ProductDependencyGenerator(catalog, shipped, options, context).Generate();

        return new Estate(catalog, links, context);
    }

    private static readonly Estate _estate = Generate();

    private static Dictionary<string, (string Line, string Product, ComponentPlan? Component)> Placement(ProductCatalog catalog)
    {
        var placement = new Dictionary<string, (string, string, ComponentPlan?)>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in catalog.Lines)
        {
            foreach (var product in line.Products)
            {
                placement[product.Name] = (line.Name, product.Name, null);
                foreach (var component in product.Components)
                    placement[component.Name] = (line.Name, product.Name, component);
            }
        }

        return placement;
    }

    [Fact]
    public void Generate_NeverLinksAProductToItselfOrToAnythingAboveOrBelowIt()
    {
        // Arrange
        var placement = Placement(_estate.Catalog);

        // Act
        var composition = _estate.Links.Where(l =>
            string.Equals(l.ProductName, l.DependsOnProductName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(placement[l.DependsOnProductName].Product, l.ProductName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(placement[l.ProductName].Product, l.DependsOnProductName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(placement[l.DependsOnProductName].Line, l.ProductName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(placement[l.ProductName].Line, l.DependsOnProductName, StringComparison.OrdinalIgnoreCase)).ToList();

        // Assert — the domain refuses these, which would fail the product's whole group
        composition.Should().BeEmpty();
    }

    [Fact]
    public void Generate_KeepsEveryLinkInsideTheHistoryAndEndingAfterItStarts()
    {
        // Act
        var misdated = _estate.Links.Where(l =>
            l.StartsOn < _estate.Context.WindowStart
            || l.StartsOn > _asOf
            || l.EndsOn is { } end && (end < l.StartsOn || end > _asOf)).ToList();

        // Assert
        misdated.Should().BeEmpty();
    }

    [Fact]
    public void Generate_NeverOverlapsTwoLinksOnOnePair()
    {
        // Act
        var overlapping = _estate.Links
            .GroupBy(l => (l.ProductName, l.DependsOnProductName))
            .Where(g =>
            {
                var ordered = g.OrderBy(l => l.StartsOn).ToList();
                return ordered.Count(l => l.EndsOn is null) > 1
                    || ordered.Zip(ordered.Skip(1)).Any(p => p.First.EndsOn is not { } end || end >= p.Second.StartsOn);
            })
            .Select(g => g.Key)
            .ToList();

        // Assert — the second row of an overlapping pair is refused, keeping its product out
        overlapping.Should().BeEmpty();
    }

    [Fact]
    public void Generate_LeavesNothingOpenOnARetiredComponent()
    {
        // Arrange
        var placement = Placement(_estate.Catalog);
        bool Retired(string name) => placement[name].Component?.RetiredOn is not null;

        // Act
        var open = _estate.Links.Where(l => l.EndsOn is null && (Retired(l.ProductName) || Retired(l.DependsOnProductName))).ToList();

        // Assert — consumers migrate off before a component retires, and its own calls stop with it
        open.Should().BeEmpty();
    }

    [Fact]
    public void Generate_StartsALinkNoEarlierThanBothEndsFirstShipped()
    {
        // Arrange — each component first ships on a day of its own
        var context = Context();
        var shippedOn = new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);
        var estate = Generate(firstShipped: c =>
        {
            var on = context.WindowStart.AddDays(Math.Abs(c.Name.GetHashCode(StringComparison.Ordinal)) % 400);
            shippedOn[c.Name] = on;
            return on;
        });
        var placement = Placement(estate.Catalog);

        DateOnly ExistsFrom(string name) =>
            placement[name].Component is not null
                ? shippedOn[name]
                : shippedOn.Where(s => placement[s.Key].Product == name && placement[s.Key].Component is not null).Min(s => s.Value);

        // Act
        var early = estate.Links.Where(l => l.StartsOn < ExistsFrom(l.ProductName) || l.StartsOn < ExistsFrom(l.DependsOnProductName)).ToList();

        // Assert
        early.Should().BeEmpty();
    }

    [Fact]
    public void Generate_LinksNothingThatHasNotShipped()
    {
        // Arrange — only services have shipped
        var estate = Generate(firstShipped: c => c.Kind == ComponentKind.Service ? Context().WindowStart : null);
        var placement = Placement(estate.Catalog);

        // Act
        var unshipped = estate.Links.Where(l =>
            placement[l.ProductName].Component is { Kind: not ComponentKind.Service }
            || placement[l.DependsOnProductName].Component is { Kind: not ComponentKind.Service }).ToList();

        // Assert
        unshipped.Should().BeEmpty();
    }

    [Fact]
    public void Generate_ConcentratesTheCatalogOnItsPlatformServices()
    {
        // Arrange
        var placement = Placement(_estate.Catalog);
        var fanIn = _estate.Links
            .Where(l => l.EndsOn is null)
            .GroupBy(l => l.DependsOnProductName, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ToList();

        // Act
        var busiest = placement[fanIn[0].Key].Component;

        // Assert — a few shared services carry most of the catalog, the most fundamental most of all
        busiest.Should().NotBeNull();
        busiest!.Hub.Should().Be(HubRole.Platform);
        busiest.Name.Should().Be("Identity Service");
    }

    [Fact]
    public void Generate_CrossesProductLinesForAtLeastAThirdOfItsLinks()
    {
        // Arrange — a link inside one subtree is invisible to that subtree's rollup, so these are what a map needs
        var placement = Placement(_estate.Catalog);

        // Act
        var crossing = _estate.Links.Count(l => placement[l.ProductName].Line != placement[l.DependsOnProductName].Line);

        // Assert
        crossing.Should().BeGreaterThan(_estate.Links.Count / 3);
    }

    [Fact]
    public void Generate_MixesStrengthAndRecordsHistory()
    {
        // Act
        var open = _estate.Links.Where(l => l.EndsOn is null).ToList();
        var changedPairs = _estate.Links.GroupBy(l => (l.ProductName, l.DependsOnProductName)).Count(g => g.Count() > 1);

        // Assert
        open.Count(l => l.Strength == "Hard").Should().BeInRange(open.Count * 4 / 10, open.Count * 8 / 10);
        _estate.Links.Count(l => l.EndsOn is not null).Should().BeGreaterThan(0);
        changedPairs.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Generate_MakesTheReturnDirectionOfAMutualPairSoft()
    {
        // Arrange
        var open = _estate.Links.Where(l => l.EndsOn is null).ToList();
        var pairs = open.Select(l => (l.ProductName, l.DependsOnProductName)).ToHashSet();

        // Act
        var mutual = open.Where(l => pairs.Contains((l.DependsOnProductName, l.ProductName))).ToList();

        // Assert — a pair that each cannot run without the other is one service split in two
        mutual.Should().NotBeEmpty();
        mutual.GroupBy(l => string.CompareOrdinal(l.ProductName, l.DependsOnProductName) < 0
                ? (l.ProductName, l.DependsOnProductName)
                : (l.DependsOnProductName, l.ProductName))
            .Should().AllSatisfy(pair => pair.Should().Contain(l => l.Strength == "Soft"));
    }

    [Fact]
    public void Generate_RecordsAFewLinksOnAWholeProduct()
    {
        // Arrange
        var placement = Placement(_estate.Catalog);

        // Act
        var onProducts = _estate.Links.Where(l => placement[l.ProductName].Component is null).ToList();

        // Assert
        onProducts.Should().NotBeEmpty();
    }

    [Fact]
    public void Generate_IsTheSameForTheSameSeed()
    {
        // Act
        var again = Generate();

        // Assert
        again.Links.Select(l => l.ImportId).Should().Equal(_estate.Links.Select(l => l.ImportId));
    }

    [Fact]
    public void Generate_ScalesItsLinksWithTheRecipesAverage()
    {
        // Act
        var sparse = Generate(options: new ProductManagementOptions { ComponentsPerTeam = 4, DependenciesPerComponent = 1.5 });

        // Assert
        sparse.Links.Count.Should().BeLessThan(_estate.Links.Count * 2 / 3);
    }
}
