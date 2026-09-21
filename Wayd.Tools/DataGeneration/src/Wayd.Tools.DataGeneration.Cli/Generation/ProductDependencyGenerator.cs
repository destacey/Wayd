using Bogus;

namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// One product relying on another over a period, named by product name — names are unique in the catalog,
/// and the ids only exist once the products import has run.
/// </summary>
public sealed class ProductDependencyModel
{
    public required string ProductName { get; init; }
    public required string DependsOnProductName { get; init; }

    /// <summary><c>Hard</c> or <c>Soft</c>, as the import reads it.</summary>
    public required string Strength { get; init; }

    /// <summary>
    /// <c>Synchronous</c>, <c>Asynchronous</c>, or both separated by a semicolon, as the import reads it.
    /// </summary>
    public string? InteractionStyles { get; init; }

    public string? Description { get; init; }
    public required DateOnly StartsOn { get; init; }
    public DateOnly? EndsOn { get; init; }

    /// <summary>Unique per row: a pair holds at most one link from any one day.</summary>
    public string ImportId => $"{ProductName}|{DependsOnProductName}|{StartsOn:yyyy-MM-dd}";
}

/// <summary>
/// Generates what the catalog's products rely on, shaped like a real estate's dependency graph rather than
/// a uniform scatter.
/// </summary>
/// <remarks>
/// <para>
/// Links run down tiers: applications rely on services, services on other services and the platform, and
/// libraries on almost nothing. Among services a random rank decides which may call which, so calls never
/// loop back and chains form across lines — orders calling inventory calling pricing — several links deep.
/// </para>
/// <para>
/// A few shared platform services draw most of the catalog, weighted so the most fundamental — identity, the
/// gateway — draw the most, and each business line's hub draws its neighbours. About four links in ten cross
/// a product line, which includes nearly every link to a platform service: a link with both ends inside one
/// subtree is invisible to that subtree's rollup, so the cross-branch ones are what a rolled-up map is for.
/// A few service pairs depend on each other, the return direction always soft.
/// </para>
/// <para>
/// History follows the catalog's own lifecycle. A link starts no earlier than both ends first shipped; every
/// link into or out of a retired component ends before it retires, because consumers migrate off first;
/// about half the consumers of a sunset component leave during its sunset; a few links churn; and a few
/// change strength partway, mostly from soft to hard as a dependency became critical.
/// </para>
/// </remarks>
internal sealed class ProductDependencyGenerator
{
    /// <summary>The area name this generator draws its seed under, so it never shifts the delivery history's draws.</summary>
    public const string AreaName = "product-dependencies";

    private const double SameProductShare = 0.40;
    private const double SameLineShare = 0.20;
    private const double PlatformHubShare = 0.25;
    private const double DomainHubShare = 0.10;
    private const double MutualShare = 0.02;
    private const double ProductLevelShare = 0.05;
    private const double ChurnShare = 0.05;
    private const double StrengthChangeShare = 0.04;

    private const string Hard = "Hard";
    private const string Soft = "Soft";

    /// <summary>What an out-degree of one means for each kind of node, before the recipe's average scales it.</summary>
    private const double BaselineDependenciesPerComponent = 3.5;

    private const string Synchronous = "Synchronous";
    private const string Asynchronous = "Asynchronous";

    /// <summary>
    /// Targets a product publishes to or consumes from rather than calls and waits on. A subset of
    /// <see cref="SoftLeaningWords"/> and not the same question: a link can be soft and synchronous (search
    /// degrades but is still called inline), or hard and asynchronous (an order is not placed until the
    /// event is accepted).
    /// </summary>
    private static readonly string[] AsynchronousLeaningWords =
        ["Notification", "Events", "Webhooks", "Exports", "Audit", "Observability", "Analytics", "Reporting",
         "Insights", "Enrichment"];

    private static readonly string[] SoftLeaningWords =
        ["Notification", "Observability", "Audit", "Search", "Email", "Translation", "Media", "Events", "Exports",
         "Webhooks", "Ratings", "Enrichment", "Insights", "Reporting", "Analytics"];

    private static readonly string[] Reasons =
    [
        "Calls its API on every request.",
        "Reads its data to build pages.",
        "Consumes the events it publishes.",
        "Sends it work to process asynchronously.",
        "Uses it to validate requests.",
        "Falls back to cached results when it is unavailable.",
    ];

    private readonly ProductCatalog _catalog;
    private readonly ProductManagementOptions _options;
    private readonly GenerationContext _context;
    private readonly Randomizer _random;
    private readonly IReadOnlyDictionary<string, DateOnly> _firstShipped;

    private readonly List<Node> _nodes = [];
    private readonly Dictionary<string, int> _inDegree = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<(string, string)> _pairs = [];
    private readonly List<Link> _links = [];

    /// <param name="firstShipped">The day each component first shipped. A component missing from it has not.</param>
    public ProductDependencyGenerator(
        ProductCatalog catalog,
        IReadOnlyDictionary<string, DateOnly> firstShipped,
        ProductManagementOptions options,
        GenerationContext context)
    {
        _catalog = catalog;
        _firstShipped = firstShipped;
        _options = options;
        _context = context;
        _random = new Randomizer(context.SeedFor(AreaName));
    }

    private DateOnly Today => _context.AsOf;

    private sealed class Node
    {
        public required string Name { get; init; }
        public required ComponentKind? Kind { get; init; }
        public required HubRole Hub { get; init; }
        public required int Line { get; init; }
        public required string Product { get; init; }
        public required double Rank { get; init; }
        public required DateOnly ExistsFrom { get; init; }
        public DateOnly? SunsetOn { get; init; }
        public DateOnly? RetiredOn { get; init; }

        /// <summary>For a platform hub, how fundamental it is: 0 is the one everything relies on.</summary>
        public int PlatformIndex { get; init; } = -1;

        /// <summary>A whole product rather than one of its components, whose links sit on the product itself.</summary>
        public bool IsProduct => Kind is null;
    }

    /// <param name="Fixed">A strength set for a reason, which a later change of strength must not undo.</param>
    private sealed record Link(Node From, Node To, string Strength, bool OwnBackend, bool Fixed);

    public IReadOnlyList<ProductDependencyModel> Generate()
    {
        BuildNodes();

        var components = _nodes.Where(n => !n.IsProduct).ToList();
        foreach (var node in components)
            Grow(node);

        AddProductLevelLinks();
        AddMutualPairs();

        return [.. _links.SelectMany(Dated)];
    }

    // ---- Nodes ---------------------------------------------------------------------------------

    private void BuildNodes()
    {
        var otherPlatformHubs = 0;

        for (var lineIndex = 0; lineIndex < _catalog.Lines.Count; lineIndex++)
        {
            foreach (var product in _catalog.Lines[lineIndex].Products)
            {
                DateOnly? productFrom = null;

                foreach (var component in product.Components)
                {
                    // Concepts and anything not shipped by today have nothing relying on them yet.
                    if (component.IsConcept || !_firstShipped.TryGetValue(component.Name, out var from))
                        continue;

                    productFrom = productFrom is null || from < productFrom ? from : productFrom;

                    _nodes.Add(new Node
                    {
                        Name = component.Name,
                        Kind = component.Kind,
                        Hub = component.Hub,
                        Line = lineIndex,
                        Product = product.Name,
                        Rank = Tier(component) + _random.Double(0, 0.9),
                        ExistsFrom = from,
                        SunsetOn = component.SunsetOn,
                        RetiredOn = component.RetiredOn,
                        PlatformIndex = component.Hub == HubRole.Platform ? PlatformIndex(component.Name, ref otherPlatformHubs) : -1,
                    });
                }

                if (productFrom is { } existsFrom)
                {
                    _nodes.Add(new Node
                    {
                        Name = product.Name,
                        Kind = null,
                        Hub = HubRole.None,
                        Line = lineIndex,
                        Product = product.Name,
                        Rank = -1,
                        ExistsFrom = existsFrom,
                    });
                }
            }
        }
    }

    /// <summary>
    /// How fundamental a platform service is, from where its name sits in the list the catalog named it from —
    /// identity first. One that kept its own name, past the end of that list, comes after all of them.
    /// </summary>
    private static int PlatformIndex(string name, ref int others)
    {
        var index = Array.IndexOf(ProductManagementVocabulary.PlatformServiceNames, name);
        return index >= 0 ? index : ProductManagementVocabulary.PlatformServiceNames.Length + others++;
    }

    /// <summary>
    /// Where a node sits in the call order. Links run from a lower rank to a higher one, which is what keeps
    /// the graph free of loops while still letting services call each other in long chains.
    /// </summary>
    private static int Tier(ComponentPlan component) => component switch
    {
        { Hub: HubRole.Platform } => 4,
        { Hub: HubRole.Domain } => 3,
        { Kind: ComponentKind.Library } => 5,
        { Kind: ComponentKind.Service } => 2,
        { Kind: ComponentKind.Tool } => 1,
        _ => 0,
    };

    /// <summary>How many products a node of this kind typically relies on, before the recipe scales it.</summary>
    private static double OutDegree(Node node) => node switch
    {
        { Hub: HubRole.Platform } => 1.2,
        { Hub: HubRole.Domain } => 2,
        { Kind: ComponentKind.WebApplication or ComponentKind.MobileApplication } => 5,
        { Kind: ComponentKind.Service } => 3.5,
        { Kind: ComponentKind.Tool } => 2,
        _ => 0.4,
    };

    // ---- Links ---------------------------------------------------------------------------------

    private void Grow(Node source)
    {
        var mean = OutDegree(source) * _options.DependenciesPerComponent / BaselineDependenciesPerComponent;
        var count = (int)Math.Floor(mean * _random.Double(0.5, 1.5) + _random.Double());

        // An application's first link is its own backend, in its own product: the one it cannot run without.
        if (source.Kind is ComponentKind.WebApplication or ComponentKind.MobileApplication && count > 0)
        {
            var backend = Weighted(_nodes.Where(n => n.Product == source.Product && Eligible(source, n) && n.Kind == ComponentKind.Service));
            if (backend is not null)
            {
                Add(source, backend, ownBackend: true);
                count--;
            }
        }

        for (var i = 0; i < count; i++)
        {
            var target = PickTarget(source);
            if (target is not null)
                Add(source, target, ownBackend: false);
        }
    }

    private Node? PickTarget(Node source)
    {
        // A platform service relies only on the more fundamental ones: the gateway on identity, never back.
        if (source.Hub == HubRole.Platform)
            return Weighted(_nodes.Where(n => n.Hub == HubRole.Platform && n.PlatformIndex < source.PlatformIndex && Eligible(source, n)));

        var roll = _random.Double();

        if (roll < PlatformHubShare)
        {
            var hub = Weighted(_nodes.Where(n => n.Hub == HubRole.Platform && Eligible(source, n)), n => 1.0 / (n.PlatformIndex + 1));
            if (hub is not null)
                return hub;
        }
        else if (roll < PlatformHubShare + DomainHubShare && source.Hub != HubRole.Domain)
        {
            var hub = Weighted(_nodes.Where(n => n.Hub == HubRole.Domain && Eligible(source, n)));
            if (hub is not null)
                return hub;
        }

        var scope = _random.Double();
        IEnumerable<Node> candidates = _nodes.Where(n => n.Hub == HubRole.None && Eligible(source, n));

        var scoped = scope < SameProductShare
            ? candidates.Where(n => n.Product == source.Product)
            : scope < SameProductShare + SameLineShare
                ? candidates.Where(n => n.Line == source.Line && n.Product != source.Product)
                : candidates.Where(n => n.Line != source.Line);

        return Weighted(scoped) ?? Weighted(candidates.Where(n => n.Line != source.Line));
    }

    /// <summary>
    /// Whether <paramref name="source"/> may rely on <paramref name="target"/>: up the call order, not already
    /// linked, and never composition — a product and its own components are the tree, not a dependency.
    /// </summary>
    private bool Eligible(Node source, Node target) =>
        target != source
        && !target.IsProduct
        && target.Rank > source.Rank
        && !(source.IsProduct && target.Product == source.Name)
        && !_pairs.Contains((source.Name, target.Name))
        && Overlaps(source, target);

    /// <summary>Whether the two were both running on some day on or before today.</summary>
    private bool Overlaps(Node source, Node target)
    {
        var from = Later(source.ExistsFrom, target.ExistsFrom);
        var until = Earlier(Earlier(source.RetiredOn, target.RetiredOn) is { } retired ? retired.AddDays(-1) : Today, Today);

        return from <= until;
    }

    /// <summary>
    /// Picks one candidate, favouring the ones already relied on — how real graphs grow, and what gives a
    /// catalog its few busy nodes rather than an even spread.
    /// </summary>
    private Node? Weighted(IEnumerable<Node> candidates, Func<Node, double>? weight = null)
    {
        var pool = candidates.ToList();
        if (pool.Count == 0)
            return null;

        var weights = pool
            .Select(n => (weight?.Invoke(n) ?? 1.0) * (1 + 0.3 * _inDegree.GetValueOrDefault(n.Name)))
            .ToList();

        var roll = _random.Double() * weights.Sum();
        for (var i = 0; i < pool.Count; i++)
        {
            roll -= weights[i];
            if (roll <= 0)
                return pool[i];
        }

        return pool[^1];
    }

    private void Add(Node from, Node to, bool ownBackend, string? strength = null)
    {
        _pairs.Add((from.Name, to.Name));
        _inDegree[to.Name] = _inDegree.GetValueOrDefault(to.Name) + 1;
        _links.Add(new Link(from, to, strength ?? PickStrength(from, to, ownBackend), ownBackend, Fixed: strength is not null));
    }

    /// <summary>
    /// The recipe's hard share, leaned by what the link is: an application on its own backend, anything on
    /// identity or the gateway, and a build on its library are nearly always hard; notifications, search and
    /// analytics usually soft.
    /// </summary>
    private string PickStrength(Node from, Node to, bool ownBackend)
    {
        var lean = 0.0;

        if (ownBackend || to.PlatformIndex is 0 or 1)
            lean += 0.3;
        if (to.Kind == ComponentKind.Library)
            lean += 0.25;
        if (SoftLeaningWords.Any(w => to.Name.Contains(w, StringComparison.OrdinalIgnoreCase)))
            lean -= 0.35;

        return _random.Double() < Math.Clamp(_options.HardDependencyFraction + lean, 0.02, 0.98) ? Hard : Soft;
    }

    /// <summary>
    /// A few whole products relying on a platform service themselves, rather than through one component —
    /// the case where the map draws a product's own node inside its box.
    /// </summary>
    private void AddProductLevelLinks()
    {
        foreach (var product in _nodes.Where(n => n.IsProduct).ToList())
        {
            if (_random.Double() >= ProductLevelShare)
                continue;

            var hub = Weighted(_nodes.Where(n => n.Hub == HubRole.Platform && Eligible(product, n)), n => 1.0 / (n.PlatformIndex + 1));
            if (hub is not null)
                Add(product, hub, ownBackend: false);
        }
    }

    /// <summary>
    /// A few service pairs that rely on each other — a callback, events flowing back — with the return
    /// direction soft, since a pair that each cannot run without the other is one service split in two.
    /// </summary>
    private void AddMutualPairs()
    {
        foreach (var link in _links.ToList())
        {
            if (link.From.Kind != ComponentKind.Service || link.To.Kind != ComponentKind.Service
                || link.From.Hub != HubRole.None || link.To.Hub != HubRole.None
                || _pairs.Contains((link.To.Name, link.From.Name))
                || _random.Double() >= MutualShare)
            {
                continue;
            }

            Add(link.To, link.From, ownBackend: false, Soft);
        }
    }

    // ---- History -------------------------------------------------------------------------------

    /// <summary>
    /// Dates a link, as one row or — when its strength changed partway — two rows meeting without sharing a
    /// day, the way the screens record a change.
    /// </summary>
    private IEnumerable<ProductDependencyModel> Dated(Link link)
    {
        var from = Later(link.From.ExistsFrom, link.To.ExistsFrom);

        // A retired component's links all end before it retires: its consumers migrated off first, and its
        // own calls stopped with it.
        var retired = Earlier(link.From.RetiredOn, link.To.RetiredOn);
        var latestStart = retired is { } r ? r.AddDays(-1) : Today;
        if (latestStart < from)
            yield break;

        // Skewed toward the earliest day: most of a service's dependencies date from when it was built.
        var span = latestStart.DayNumber - from.DayNumber;
        var startsOn = from.AddDays((int)(span * Math.Pow(_random.Double(), 2)));

        DateOnly? endsOn = null;

        if (retired is { } retiredOn)
        {
            endsOn = RandomDay(Later(startsOn, retiredOn.AddDays(-180)), retiredOn.AddDays(-1));
        }
        else if (link.To.SunsetOn is { } sunsetOn && sunsetOn < Today && _random.Double() < 0.5)
        {
            endsOn = RandomDay(Later(startsOn, sunsetOn), Today.AddDays(-1));
        }
        else if (link.To.Hub == HubRole.None && !link.OwnBackend && _random.Double() < ChurnShare)
        {
            endsOn = RandomDay(startsOn.AddDays(30), Today.AddDays(-1));
        }

        var description = _random.Double() < 0.4 ? _random.ArrayElement(Reasons) : null;

        // A change needs a day after the start to change on, and the old half needs a day before it to end on.
        if (endsOn is null && !link.Fixed && Today.DayNumber - startsOn.DayNumber > 90 && _random.Double() < StrengthChangeShare)
        {
            var changedOn = startsOn.AddDays(_random.Int(30, Today.DayNumber - startsOn.DayNumber - 10));
            var becameCritical = _random.Double() < 0.8;

            yield return Model(link, becameCritical ? Soft : Hard, description, startsOn, changedOn.AddDays(-1));
            yield return Model(link, becameCritical ? Hard : Soft, description, changedOn, null);
            yield break;
        }

        yield return Model(link, link.Strength, description, startsOn, endsOn);
    }

    /// <summary>A day between the two, or null when the range is empty — a link too short-lived to end is left open.</summary>
    private DateOnly? RandomDay(DateOnly earliest, DateOnly latest) =>
        latest < earliest ? null : earliest.AddDays(_random.Int(0, latest.DayNumber - earliest.DayNumber));

    private static ProductDependencyModel Model(Link link, string strength, string? description, DateOnly startsOn, DateOnly? endsOn) =>
        new()
        {
            ProductName = link.From.Name,
            DependsOnProductName = link.To.Name,
            Strength = strength,
            InteractionStyles = PickInteractionStyles(link),
            Description = description,
            StartsOn = startsOn,
            EndsOn = endsOn,
        };

    /// <summary>
    /// Derived from what the target is, not drawn at random: the style is a property of the link, so the two
    /// rows a strength change produces have to agree on it.
    /// </summary>
    private static string PickInteractionStyles(Link link)
    {
        var asynchronous = AsynchronousLeaningWords.Any(w => link.To.Name.Contains(w, StringComparison.OrdinalIgnoreCase));

        // An application and its own backend do both: the requests it waits on, and the events it publishes back.
        if (asynchronous && link.OwnBackend)
            return $"{Synchronous};{Asynchronous}";

        return asynchronous ? Asynchronous : Synchronous;
    }

    private static DateOnly Later(DateOnly a, DateOnly b) => a > b ? a : b;

    private static DateOnly? Earlier(DateOnly? a, DateOnly? b) =>
        a is null ? b : b is null ? a : a < b ? a : b;

    private static DateOnly Earlier(DateOnly a, DateOnly b) => a < b ? a : b;
}
