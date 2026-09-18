using Bogus;
using static Wayd.Tools.DataGeneration.Cli.Generation.ProductManagementVocabulary;

namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// Whether a component is one the rest of the catalog leans on: a shared platform service most products
/// rely on, or the service a business line's neighbours call into.
/// </summary>
public enum HubRole
{
    None,
    Domain,
    Platform,
}

/// <summary>
/// How dense the catalog is, and whether its hubs are picked out. Shared by every generator that derives
/// the catalog, so they all name the same components.
/// </summary>
/// <param name="NamesHubs">
/// Picks out the hubs the dependency map is built around, naming the platform ones for what they are. Off
/// when no dependencies are generated, since a hub nothing depends on is only a renamed service.
/// </param>
public sealed record ProductCatalogShape(double ComponentsPerTeam, bool NamesHubs, int? PlatformServices)
{
    public static ProductCatalogShape Baseline { get; } =
        new(ProductCatalog.BaselineComponentsPerTeam, NamesHubs: false, PlatformServices: null);

    public static ProductCatalogShape For(ProductManagementOptions options) =>
        new(options.ComponentsPerTeam, options.Dependencies, options.PlatformServices);
}

/// <summary>What a catalog component is, which decides its product type and how it ships.</summary>
public enum ComponentKind
{
    Service,
    WebApplication,
    MobileApplication,
    Library,
    Tool,
}

/// <summary>
/// What the organization builds: a product line per value stream, a product per ART, and the components
/// each team owns — their names, and when any of them were wound down.
/// </summary>
/// <remarks>
/// Derived from the org under its own seed rather than inside either generator, because two areas need the
/// same answer. The Product Management generator builds the catalog from it, and the PPM generator names
/// projects after the components their owning team builds — which has to hold whether or not the catalog
/// itself is seeded. Deriving it here, rather than while the org is generated, leaves the org's own data
/// untouched.
/// </remarks>
public sealed class ProductCatalog
{
    /// <summary>The area name the catalog's names are drawn under.</summary>
    public const string AreaName = "product-catalog";

    /// <summary>
    /// Components per team the catalog has at its baseline: one deployable each, with a library or tool
    /// beside about one in five and a mobile app on about one product in three.
    /// </summary>
    public const double BaselineComponentsPerTeam = 1.3;

    /// <summary>Drawn under its own name, so picking hubs never shifts a name or a lifecycle drawn before it.</summary>
    private const string HubAreaName = "product-catalog.hubs";

    private readonly Dictionary<string, List<ComponentPlan>> _componentsByTeam = new(StringComparer.OrdinalIgnoreCase);

    private ProductCatalog(IReadOnlyList<ProductLinePlan> lines)
    {
        Lines = lines;

        foreach (var component in lines.SelectMany(l => l.Products).SelectMany(p => p.Components))
        {
            if (!_componentsByTeam.TryGetValue(component.TeamCode, out var owned))
                _componentsByTeam[component.TeamCode] = owned = [];

            owned.Add(component);
        }
    }

    public IReadOnlyList<ProductLinePlan> Lines { get; }

    /// <summary>
    /// What one team owns and still runs on a given day. Concepts are excluded — they are ideas, not
    /// something a project works on — and so is anything retired by then.
    /// </summary>
    public IReadOnlyList<ComponentPlan> ComponentsOf(string teamCode, DateOnly on) =>
        _componentsByTeam.TryGetValue(teamCode, out var owned)
            ? [.. owned.Where(c => !c.IsConcept && (c.RetiredOn is null || on < c.RetiredOn))]
            : [];

    public static ProductCatalog From(OrgStructure org, GenerationContext context, ProductCatalogShape? shape = null) =>
        new Builder(new Faker { Random = new Randomizer(context.SeedFor(AreaName)) }, context, shape ?? ProductCatalogShape.Baseline).Build(org);

    private sealed class Builder(Faker faker, GenerationContext context, ProductCatalogShape shape)
    {
        private readonly Faker _faker = faker;
        private readonly GenerationContext _context = context;
        private readonly ProductCatalogShape _shape = shape;
        private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _codes = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _slugs = new(StringComparer.OrdinalIgnoreCase);

        private sealed record LineDraft(string Domain, string Name, string Code, string Slug, List<ProductDraft> Products)
        {
            public bool IsPlatform { get; set; }
        }

        private sealed record ProductDraft(string ArtCode, string ArtName, string Name, List<ComponentPlan> Components);

        public ProductCatalog Build(OrgStructure org)
        {
            List<LineDraft> lines = [];

            foreach (var valueStream in org.ValueStreams)
            {
                var domain = valueStream.Domain;
                var line = new LineDraft(domain, Unique($"{domain} {Pick(ProductLineSuffixes)}"), UniqueCode(domain), UniqueSlug(domain), []);

                foreach (var art in valueStream.Arts)
                {
                    // A group with no ART is the whole value stream's teams: it has no name of its own to take a
                    // product name from, and its line's code tells its packages apart, since a line holds only it.
                    var artBase = art.TeamCode is null ? domain : WithoutLastWord(art.Name);
                    var productName = Unique(string.Equals(artBase, domain, StringComparison.OrdinalIgnoreCase)
                        ? $"{domain} {Pick(ProductNouns)}"
                        : artBase);

                    var product = art.TeamCode is null
                        ? new ProductDraft(line.Code, $"{domain} teams", productName, [])
                        : new ProductDraft(art.TeamCode, art.Name, productName, []);

                    // Every team owns something it deploys; some also publish a library or a tool beside it.
                    foreach (var team in art.Teams)
                    {
                        product.Components.Add(Component(domain, team, _faker.Random.Double() < 0.65 ? ComponentKind.Service : ComponentKind.WebApplication, productName));

                        if (_faker.Random.Double() < 0.2)
                            product.Components.Add(Component(domain, team, _faker.Random.Bool(0.6f) ? ComponentKind.Library : ComponentKind.Tool, productName));

                        AddOwnedServices(domain, team, product);
                    }

                    // Roughly one product in three also ships a mobile app, built by one of its teams.
                    if (art.Teams.Count > 0 && _faker.Random.Double() < 0.3)
                        product.Components.Add(Component(domain, _faker.PickRandom(art.Teams.ToList()), ComponentKind.MobileApplication, productName));

                    line.Products.Add(product);
                }

                lines.Add(line);
            }

            AddConcepts(lines, org);

            if (_shape.NamesHubs)
                PickHubs(lines);

            return new ProductCatalog([.. lines.Select(l => new ProductLinePlan(
                l.Domain, l.Name, l.Code, l.Slug,
                [.. l.Products.Select(p => new ArtProductPlan(p.ArtCode, p.ArtName, p.Name, p.Components))],
                l.IsPlatform))]);
        }

        /// <summary>
        /// The services a team owns beyond its first deployable, when the catalog is denser than its baseline.
        /// </summary>
        /// <remarks>
        /// Draws nothing at the baseline, so a seed pinned before the knob existed still generates the same
        /// catalog. Mostly services, named for a capability of the team's own — "Loyalty Ledger Service" —
        /// because a team running five services gives each a job, not a number.
        /// </remarks>
        private void AddOwnedServices(string domain, TeamNode team, ProductDraft product)
        {
            var extra = _shape.ComponentsPerTeam - BaselineComponentsPerTeam;
            if (extra <= 0)
                return;

            var count = (int)Math.Floor(extra * _faker.Random.Double(0.5, 1.5) + _faker.Random.Double());

            var teamBase = WithoutLastWord(team.Name);
            var own = teamBase.StartsWith($"{domain} ", StringComparison.OrdinalIgnoreCase) ? teamBase[(domain.Length + 1)..] : teamBase;

            for (var i = 0; i < count; i++)
            {
                var kind = _faker.Random.Double() < 0.85 ? ComponentKind.Service : ComponentKind.WebApplication;
                var (status, sunsetOn, retiredOn) = PickLifecycle();
                var noun = kind == ComponentKind.Service ? Pick(ServiceNouns) : Pick(WebApplicationNouns);
                var name = Unique($"{own} {Pick(CapabilityNouns)} {noun}");

                product.Components.Add(new ComponentPlan(name, kind, team.TeamCode, team.Name, status, sunsetOn, retiredOn));
            }
        }

        /// <summary>
        /// Picks out the services the rest of the catalog leans on: a handful of shared platform services, and
        /// one hub in every other line.
        /// </summary>
        /// <remarks>
        /// With three lines or more, one of them is the platform — a platform-sounding domain when the org has
        /// one — and its services become the shared ones, renamed for what they are, since a map whose busiest
        /// node reads "Loyalty Gateway" says nothing about why everything depends on it. That puts nearly every
        /// link to a hub across a branch, which is the case a subtree's rollup has to handle. A smaller company
        /// has no platform line: whichever teams own identity and the gateway sit beside their products, so the
        /// hubs are spread across the lines. Only running services qualify — a sunset or retired hub would end
        /// most of the catalog's links with it.
        /// </remarks>
        private void PickHubs(List<LineDraft> lines)
        {
            var random = new Randomizer(_context.SeedFor(HubAreaName));

            static bool Running(ComponentPlan c) =>
                c.Kind == ComponentKind.Service && !c.IsConcept && c.Status == ActiveStatus;

            static IEnumerable<(ProductDraft Product, ComponentPlan Component)> RunningIn(IEnumerable<LineDraft> of) =>
                of.SelectMany(l => l.Products).SelectMany(p => p.Components.Where(Running).Select(c => (p, c)));

            var services = RunningIn(lines).ToList();
            if (services.Count == 0)
                return;

            var platformCount = Math.Min(
                services.Count,
                _shape.PlatformServices ?? Math.Clamp((int)Math.Round(services.Count / 40.0), 3, 8));

            List<(ProductDraft Product, ComponentPlan Component)> candidates;
            if (lines.Count >= 3)
            {
                var platform = lines.FirstOrDefault(l => PlatformDomains.Contains(l.Domain)) ?? lines[0];
                platform.IsPlatform = true;

                // The platform line's own services first, then the others', so a thin platform line is topped
                // up rather than leaving the catalog without its shared services.
                candidates =
                [
                    .. random.Shuffle(RunningIn([platform])),
                    .. random.Shuffle(RunningIn(lines.Where(l => l != platform))),
                ];
            }
            else
            {
                candidates = [.. random.Shuffle(services)];
            }

            // A running service that already carries a platform name is that service, wherever it sits: it becomes
            // the hub rather than leaving the hub to be called "Identity Service 2" beside it. A name held by
            // anything else — a retired component — is skipped rather than suffixed.
            var byName = lines.SelectMany(l => l.Products)
                .SelectMany(p => p.Components.Select(c => (Product: p, Component: c)))
                .ToDictionary(e => e.Component.Name, StringComparer.OrdinalIgnoreCase);
            HashSet<string> hubs = new(StringComparer.OrdinalIgnoreCase);
            var cursor = 0;

            (ProductDraft Product, ComponentPlan Component)? NextCandidate()
            {
                while (cursor < candidates.Count && hubs.Contains(candidates[cursor].Component.Name))
                    cursor++;

                return cursor < candidates.Count ? candidates[cursor++] : null;
            }

            foreach (var platformName in PlatformServiceNames)
            {
                if (hubs.Count == platformCount)
                    break;

                if (byName.TryGetValue(platformName, out var existing))
                {
                    if (Running(existing.Component) && hubs.Add(platformName))
                        Replace(existing.Product, existing.Component, existing.Component with { Hub = HubRole.Platform });

                    continue;
                }

                if (NextCandidate() is not { } candidate)
                    break;

                var name = Unique(platformName);
                hubs.Add(name);
                Replace(candidate.Product, candidate.Component, candidate.Component with { Name = name, Hub = HubRole.Platform });
            }

            // A recipe asking for more shared services than there are names keeps the rest under their own.
            while (hubs.Count < platformCount && NextCandidate() is { } extra)
            {
                hubs.Add(extra.Component.Name);
                Replace(extra.Product, extra.Component, extra.Component with { Hub = HubRole.Platform });
            }

            foreach (var line in lines.Where(l => !l.IsPlatform))
            {
                var hub = random.Shuffle(RunningIn([line]).Where(e => e.Component.Hub == HubRole.None)).FirstOrDefault();
                if (hub.Component is not null)
                    Replace(hub.Product, hub.Component, hub.Component with { Hub = HubRole.Domain });
            }
        }

        private static void Replace(ProductDraft product, ComponentPlan component, ComponentPlan replacement) =>
            product.Components[product.Components.IndexOf(component)] = replacement;

        /// <summary>A few components still at the idea stage, so the catalog holds something that has not shipped.</summary>
        private void AddConcepts(List<LineDraft> lines, OrgStructure org)
        {
            var products = lines.SelectMany(l => l.Products).Where(p => p.Components.Count > 0).ToList();
            var teamCount = org.ValueStreams.SelectMany(v => v.Arts).Sum(a => a.Teams.Count);
            var count = Math.Min(products.Count, Math.Max(1, teamCount / 12));

            foreach (var product in _faker.PickRandom(products, count))
            {
                var owner = _faker.PickRandom(product.Components);
                var name = Unique($"{WithoutLastWord(owner.Name)} {Pick(ConceptNouns)}");

                product.Components.Add(new ComponentPlan(name, ComponentKind.Service, owner.TeamCode, owner.TeamName, ConceptStatus));
            }
        }

        /// <summary>
        /// Most components are active. A few are being wound down, shipping rarely after a sunset date, and a
        /// few were retired partway through the history and ship nothing after it.
        /// </summary>
        private (string Status, DateOnly? SunsetOn, DateOnly? RetiredOn) PickLifecycle()
        {
            var historyStart = _context.WindowStart > _context.FoundedOn ? _context.WindowStart : _context.FoundedOn;
            var span = _context.AsOf.DayNumber - historyStart.DayNumber;
            var roll = _faker.Random.Double();

            if (span > 180 && roll < 0.05)
                return (RetiredStatus, null, historyStart.AddDays(_faker.Random.Int(span / 4, span - 60)));

            if (span > 120 && roll < 0.13)
                return (SunsetStatus, historyStart.AddDays(_faker.Random.Int(span * 2 / 5, span - 30)), null);

            return (ActiveStatus, null, null);
        }

        private ComponentPlan Component(string domain, TeamNode team, ComponentKind kind, string productName)
        {
            var (status, sunsetOn, retiredOn) = PickLifecycle();

            if (kind == ComponentKind.MobileApplication)
                return new ComponentPlan(Unique($"{productName} Mobile"), kind, team.TeamCode, team.Name, status, sunsetOn, retiredOn);

            var noun = kind switch
            {
                ComponentKind.Service => Pick(ServiceNouns),
                ComponentKind.WebApplication => Pick(WebApplicationNouns),
                ComponentKind.Library => Pick(LibraryNouns),
                _ => Pick(ToolNouns),
            };

            // A team is named for its line's domain and then its own — "Payments Loyalty Platform" — and the
            // component needs only the second: it already sits under the line in the catalog. The domain comes
            // back only to tell apart two teams whose own names match.
            var teamBase = WithoutLastWord(team.Name);
            var own = teamBase.StartsWith($"{domain} ", StringComparison.OrdinalIgnoreCase) ? teamBase[(domain.Length + 1)..] : teamBase;

            var name = _names.Contains($"{own} {noun}") ? Unique($"{teamBase} {noun}") : Unique($"{own} {noun}");

            return new ComponentPlan(name, kind, team.TeamCode, team.Name, status, sunsetOn, retiredOn);
        }

        private string Unique(string name) => MakeUnique(name, _names, " ");

        /// <summary>A short code a release version starts with — "PAY 2026.07" — unique across product lines.</summary>
        private string UniqueCode(string domain)
        {
            var words = domain.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => new string([.. w.Where(char.IsLetter)]))
                .Where(w => w.Length > 0)
                .ToList();

            var code = words.Count == 1
                ? words[0][..Math.Min(3, words[0].Length)]
                : string.Concat(words.Select(w => w[0]));

            return MakeUnique(code.ToUpperInvariant(), _codes, string.Empty);
        }

        /// <summary>The lowercase stem environment names are built from — "trust-safety-prod".</summary>
        private string UniqueSlug(string domain)
        {
            var slug = new string([.. domain.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-')]);
            while (slug.Contains("--", StringComparison.Ordinal))
                slug = slug.Replace("--", "-", StringComparison.Ordinal);

            return MakeUnique(slug.Trim('-'), _slugs, "-");
        }

        private string Pick(string[] pool) => _faker.PickRandom(pool);
    }

    /// <summary>
    /// A name without its last word. Team and ART names end in their kind — "Platform", "Train" — which a
    /// product named after them should not repeat.
    /// </summary>
    internal static string WithoutLastWord(string name)
    {
        var lastSpace = name.TrimEnd().LastIndexOf(' ');
        return lastSpace > 0 ? name[..lastSpace] : name;
    }

    private static string MakeUnique(string name, HashSet<string> used, string separator)
    {
        if (used.Add(name))
            return name;

        var n = 2;
        string candidate;
        do
        {
            candidate = $"{name}{separator}{n}";
            n++;
        }
        while (!used.Add(candidate));

        return candidate;
    }
}

/// <summary>A product line: one value stream's products, and the names its releases and environments take.</summary>
/// <param name="IsPlatform">The line holding the shared platform services, when the catalog has one.</param>
public sealed record ProductLinePlan(string Domain, string Name, string Code, string Slug, IReadOnlyList<ArtProductPlan> Products, bool IsPlatform = false);

/// <summary>
/// The product one ART delivers, and the components its teams own. With the ART tier off the group is a
/// value stream's teams, and <see cref="ArtCode"/> is its line's code.
/// </summary>
public sealed record ArtProductPlan(string ArtCode, string ArtName, string Name, IReadOnlyList<ComponentPlan> Components);

/// <summary>
/// A component one team owns, and where it is in its life: a sunset component ships rarely after
/// <see cref="SunsetOn"/>, and a retired one nothing after <see cref="RetiredOn"/>.
/// </summary>
public sealed record ComponentPlan(
    string Name,
    ComponentKind Kind,
    string TeamCode,
    string TeamName,
    string Status,
    DateOnly? SunsetOn = null,
    DateOnly? RetiredOn = null,
    HubRole Hub = HubRole.None)
{
    /// <summary>A service still at the idea stage, which has nothing but a planned first version.</summary>
    public bool IsConcept => Status == ProductManagementVocabulary.ConceptStatus;
}
