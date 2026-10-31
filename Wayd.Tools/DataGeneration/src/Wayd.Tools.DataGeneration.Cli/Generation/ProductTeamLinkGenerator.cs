using Bogus;
using static Wayd.Tools.DataGeneration.Cli.Generation.ProductManagementVocabulary;

namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// Decides which teams work on which products: the catalog's components, mostly, by the codes of the teams
/// that build them.
/// </summary>
/// <remarks>
/// <para>
/// A component is worked on by the team that owns it. About one in three is shared with a second team, and a
/// quarter of those with a third — a sibling in the same ART as a rule, now and then a team elsewhere in the
/// line. So each delivery team works on the few related products it owns plus what it shares, nearly all inside
/// one product line, and more of them as the catalog is denser. A retired component has nobody on it, and a team
/// left with nothing — its own components all retired — picks up a sibling's, or failing that another in its
/// line, so a delivery team works on nothing only when every component in its line has retired.
/// </para>
/// <para>
/// Links go on the most specific products, since product pages roll links up to their ancestors: an ART's
/// product carries none of its own. Most product lines carry their value stream's team of teams, or in an org
/// without one, one of their ARTs — a count of lines rather than a coin flip per line, so a small catalog
/// never lands on none.
/// </para>
/// <para>
/// Drawn under its own seed, after the catalog, so the catalog and the delivery history are the same with or
/// without it. Teams are named by code, which the import resolves, so nothing waits on the ids the
/// teams import hands back.
/// </para>
/// </remarks>
internal sealed class ProductTeamLinkGenerator(ProductCatalog catalog, OrgStructure org, GenerationContext context)
{
    /// <summary>The area name this generator draws its seed under.</summary>
    public const string AreaName = "product-team-links";

    private const double SharedShare = 0.35;
    private const double ThirdTeamShare = 0.25;
    private const double SameArtShare = 0.8;
    private const double LineTeamOfTeamsShare = 0.6;
    private const int MostTeamsPerProduct = 3;

    private readonly ProductCatalog _catalog = catalog;
    private readonly OrgStructure _org = org;
    private readonly Randomizer _random = new(context.SeedFor(AreaName));

    /// <summary>The team codes linked to each product, by product name, in the order they were linked.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Generate()
    {
        Dictionary<string, List<string>> teamsByProduct = new(StringComparer.OrdinalIgnoreCase);

        void Link(string productName, string teamCode)
        {
            if (!teamsByProduct.TryGetValue(productName, out var teams))
                teamsByProduct[productName] = teams = [];

            if (!teams.Contains(teamCode, StringComparer.OrdinalIgnoreCase))
                teams.Add(teamCode);
        }

        var linesWithTeamOfTeams = _catalog.Lines.Count == 0
            ? []
            : _random.Shuffle(_catalog.Lines.Select(l => l.Name))
                .Take(Math.Max(1, (int)Math.Round(_catalog.Lines.Count * LineTeamOfTeamsShare)))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var line in _catalog.Lines)
        {
            var valueStream = _org.ValueStreams.FirstOrDefault(v => string.Equals(v.Domain, line.Domain, StringComparison.OrdinalIgnoreCase));
            var lineTeams = valueStream?.Arts.SelectMany(a => a.Teams).Select(t => t.TeamCode).ToList() ?? [];

            if (linesWithTeamOfTeams.Contains(line.Name))
                LinkLineToTeamOfTeams(line, valueStream, Link);

            List<ComponentPlan> lineWorked = [];

            foreach (var product in line.Products)
            {
                var artTeams = product.Components.Select(c => c.TeamCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var worked = product.Components.Where(c => c.Status != RetiredStatus).ToList();
                lineWorked.AddRange(worked);

                foreach (var component in worked)
                {
                    Link(component.Name, component.TeamCode);

                    if (_random.Double() >= SharedShare)
                        continue;

                    var extra = _random.Double() < ThirdTeamShare ? 2 : 1;
                    for (var i = 0; i < extra; i++)
                    {
                        if (PickOtherTeam(teamsByProduct[component.Name], artTeams, lineTeams) is { } other)
                            Link(component.Name, other);
                    }
                }
            }

            foreach (var product in line.Products)
            {
                var artTeams = product.Components.Select(c => c.TeamCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var worked = product.Components.Where(c => c.Status != RetiredStatus).ToList();
                TopUp(worked, lineWorked, artTeams, teamsByProduct, Link);
            }
        }

        return teamsByProduct.ToDictionary(p => p.Key, p => (IReadOnlyList<string>)p.Value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Puts the value stream's team of teams on its product line; in an org without that tier, one of the
    /// line's ARTs instead.
    /// </summary>
    private void LinkLineToTeamOfTeams(ProductLinePlan line, ValueStreamNode? valueStream, Action<string, string> link)
    {
        if (valueStream is null)
            return;

        var arts = valueStream.Arts.Select(a => a.TeamCode).OfType<string>().ToList();
        var teamOfTeams = valueStream.TeamCode ?? (arts.Count > 0 ? _random.ListItem(arts) : null);

        if (teamOfTeams is not null)
            link(line.Name, teamOfTeams);
    }

    /// <summary>A team not yet on the product: a sibling in the same ART as a rule, otherwise one from elsewhere in the line.</summary>
    private string? PickOtherTeam(List<string> already, List<string> artTeams, List<string> lineTeams)
    {
        var pool = _random.Double() < SameArtShare ? artTeams : lineTeams;
        var candidates = pool.Where(t => !already.Contains(t, StringComparer.OrdinalIgnoreCase)).ToList();

        if (candidates.Count == 0)
            candidates = [.. artTeams.Where(t => !already.Contains(t, StringComparer.OrdinalIgnoreCase))];

        return candidates.Count == 0 ? null : _random.ListItem(candidates);
    }

    /// <summary>
    /// Gives a team that works on nothing — its own components have all retired — one of its siblings', or
    /// failing that one elsewhere in its line, within the cap the sharing above keeps. Run once the whole line is
    /// linked, so the fallback sees every product in it.
    /// </summary>
    private void TopUp(
        List<ComponentPlan> worked,
        List<ComponentPlan> lineWorked,
        List<string> artTeams,
        Dictionary<string, List<string>> teamsByProduct,
        Action<string, string> link)
    {
        foreach (var team in artTeams)
        {
            if (lineWorked.Any(c => teamsByProduct.TryGetValue(c.Name, out var t) && t.Contains(team, StringComparer.OrdinalIgnoreCase)))
                continue;

            bool Open(ComponentPlan c) => teamsByProduct.TryGetValue(c.Name, out var t) && t.Count < MostTeamsPerProduct;

            var candidates = worked.Where(Open).ToList();
            if (candidates.Count == 0)
                candidates = [.. lineWorked.Where(Open)];

            if (candidates.Count > 0)
                link(_random.ListItem(candidates).Name, team);
        }
    }
}
