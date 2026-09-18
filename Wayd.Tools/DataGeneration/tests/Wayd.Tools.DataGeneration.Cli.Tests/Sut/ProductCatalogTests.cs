using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli.Generation;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

/// <summary>
/// The catalog every generator derives: how dense it is, and which of its services the rest leans on.
/// </summary>
public class ProductCatalogTests
{
    private static readonly GenerationContext _context = new() { AsOf = new DateOnly(2026, 6, 15), Seed = 1234 };

    private static OrgStructure Org(int valueStreams = 6, int teams = 60) =>
        new OrgGenerator(new OrgOptions { ValueStreams = valueStreams, Teams = teams }, _context).Generate().Structure;

    private static List<ComponentPlan> Components(ProductCatalog catalog) =>
        [.. catalog.Lines.SelectMany(l => l.Products).SelectMany(p => p.Components)];

    [Fact]
    public void From_AtTheBaselineWithoutHubs_IsTheCatalogItAlwaysWas()
    {
        // Arrange
        var org = Org();

        // Act
        var implicitBaseline = ProductCatalog.From(org, _context);
        var statedBaseline = ProductCatalog.From(org, _context, new ProductCatalogShape(ProductCatalog.BaselineComponentsPerTeam, NamesHubs: false, null));

        // Assert — a pinned seed from before the density knob must still generate the same components
        Components(statedBaseline).Should().Equal(Components(implicitBaseline));
    }

    [Fact]
    public void From_GivesEachTeamMoreComponentsWhenAskedForADenserCatalog()
    {
        // Arrange
        var org = Org();
        var teams = org.ValueStreams.SelectMany(v => v.Arts).Sum(a => a.Teams.Count);

        // Act
        var dense = ProductCatalog.From(org, _context, new ProductCatalogShape(4, NamesHubs: false, null));

        // Assert
        var perTeam = Components(dense).Count(c => !c.IsConcept) / (double)teams;
        perTeam.Should().BeInRange(3, 5);
    }

    [Fact]
    public void From_NamesComponentsUniquelyHoweverDense()
    {
        // Act
        var dense = ProductCatalog.From(Org(), _context, new ProductCatalogShape(8, NamesHubs: true, null));

        // Assert
        var names = Components(dense).Select(c => c.Name).ToList();
        names.Should().OnlyHaveUniqueItems(n => n.ToUpperInvariant());
    }

    [Fact]
    public void From_PutsTheSharedServicesInAPlatformLineWhenTheCatalogHasThreeOrMore()
    {
        // Act
        var catalog = ProductCatalog.From(Org(), _context, new ProductCatalogShape(4, NamesHubs: true, null));

        // Assert
        var platform = catalog.Lines.Should().ContainSingle(l => l.IsPlatform).Subject;
        platform.Products.SelectMany(p => p.Components).Should().Contain(c => c.Hub == HubRole.Platform);
        Components(catalog).Should().Contain(c => c.Name == "Identity Service" && c.Hub == HubRole.Platform);
    }

    [Fact]
    public void From_NeverSuffixesAPlatformServicesName()
    {
        // Arrange — across seeds, some org will already own a component the platform list would name
        var suffixed = Enumerable.Range(1, 15)
            .Select(seed => new GenerationContext { AsOf = _context.AsOf, Seed = seed })
            .SelectMany(context => Components(ProductCatalog.From(
                new OrgGenerator(new OrgOptions { ValueStreams = 6, Teams = 60 }, context).Generate().Structure,
                context,
                new ProductCatalogShape(4, NamesHubs: true, null))))
            .Where(c => c.Hub == HubRole.Platform && char.IsDigit(c.Name[^1]))
            .Select(c => c.Name)
            .ToList();

        // Assert — "Identity Service 2" beside an "Identity Service" says nothing about which one everything uses
        suffixed.Should().BeEmpty();
    }

    [Fact]
    public void From_PicksOneHubInEveryOtherLine()
    {
        // Act
        var catalog = ProductCatalog.From(Org(), _context, new ProductCatalogShape(4, NamesHubs: true, null));

        // Assert
        catalog.Lines.Where(l => !l.IsPlatform).Should().AllSatisfy(line =>
            line.Products.SelectMany(p => p.Components).Count(c => c.Hub == HubRole.Domain).Should().Be(1));
    }

    [Fact]
    public void From_MakesOnlyRunningServicesIntoHubs()
    {
        // Act
        var catalog = ProductCatalog.From(Org(), _context, new ProductCatalogShape(4, NamesHubs: true, null));

        // Assert — a sunset or retired hub would end most of the catalog's links with it
        Components(catalog).Where(c => c.Hub != HubRole.None).Should().AllSatisfy(c =>
        {
            c.Kind.Should().Be(ComponentKind.Service);
            c.Status.Should().Be("Active");
        });
    }

    [Fact]
    public void From_SpreadsTheHubsWhenTheCatalogIsTooSmallForAPlatformLine()
    {
        // Act
        var catalog = ProductCatalog.From(Org(valueStreams: 2, teams: 12), _context, new ProductCatalogShape(2, NamesHubs: true, null));

        // Assert
        catalog.Lines.Should().NotContain(l => l.IsPlatform);
        Components(catalog).Should().Contain(c => c.Hub == HubRole.Platform);
    }

    [Fact]
    public void From_HonoursAStatedNumberOfPlatformServices()
    {
        // Act
        var catalog = ProductCatalog.From(Org(), _context, new ProductCatalogShape(4, NamesHubs: true, PlatformServices: 5));

        // Assert
        Components(catalog).Count(c => c.Hub == HubRole.Platform).Should().Be(5);
    }
}
