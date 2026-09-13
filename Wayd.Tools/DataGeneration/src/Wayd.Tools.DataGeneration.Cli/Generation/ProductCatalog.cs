using Bogus;
using static Wayd.Tools.DataGeneration.Cli.Generation.ProductManagementVocabulary;

namespace Wayd.Tools.DataGeneration.Cli.Generation;

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

    public static ProductCatalog From(OrgStructure org, GenerationContext context) =>
        new Builder(new Faker { Random = new Randomizer(context.SeedFor(AreaName)) }, context).Build(org);

    private sealed class Builder(Faker faker, GenerationContext context)
    {
        private readonly Faker _faker = faker;
        private readonly GenerationContext _context = context;
        private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _codes = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _slugs = new(StringComparer.OrdinalIgnoreCase);

        private sealed record LineDraft(string Domain, string Name, List<ProductDraft> Products);

        private sealed record ProductDraft(string ArtCode, string ArtName, string Name, List<ComponentPlan> Components);

        public ProductCatalog Build(OrgStructure org)
        {
            List<LineDraft> lines = [];

            foreach (var valueStream in org.ValueStreams)
            {
                var domain = valueStream.Domain;
                var line = new LineDraft(domain, Unique($"{domain} {Pick(ProductLineSuffixes)}"), []);

                foreach (var art in valueStream.Arts)
                {
                    var artBase = WithoutLastWord(art.Name);
                    var productName = Unique(string.Equals(artBase, domain, StringComparison.OrdinalIgnoreCase)
                        ? $"{domain} {Pick(ProductNouns)}"
                        : artBase);

                    var product = new ProductDraft(art.TeamCode, art.Name, productName, []);

                    // Every team owns something it deploys; some also publish a library or a tool beside it.
                    foreach (var team in art.Teams)
                    {
                        product.Components.Add(Component(domain, team, _faker.Random.Double() < 0.65 ? ComponentKind.Service : ComponentKind.WebApplication, productName));

                        if (_faker.Random.Double() < 0.2)
                            product.Components.Add(Component(domain, team, _faker.Random.Bool(0.6f) ? ComponentKind.Library : ComponentKind.Tool, productName));
                    }

                    // Roughly one product in three also ships a mobile app, built by one of its teams.
                    if (art.Teams.Count > 0 && _faker.Random.Double() < 0.3)
                        product.Components.Add(Component(domain, _faker.PickRandom(art.Teams.ToList()), ComponentKind.MobileApplication, productName));

                    line.Products.Add(product);
                }

                lines.Add(line);
            }

            AddConcepts(lines, org);

            return new ProductCatalog([.. lines.Select(l => new ProductLinePlan(
                l.Domain, l.Name, UniqueCode(l.Domain), UniqueSlug(l.Domain),
                [.. l.Products.Select(p => new ArtProductPlan(p.ArtCode, p.ArtName, p.Name, p.Components))]))]);
        }

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
public sealed record ProductLinePlan(string Domain, string Name, string Code, string Slug, IReadOnlyList<ArtProductPlan> Products);

/// <summary>The product one ART delivers, and the components its teams own.</summary>
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
    DateOnly? RetiredOn = null)
{
    /// <summary>A service still at the idea stage, which has nothing but a planned first version.</summary>
    public bool IsConcept => Status == ProductManagementVocabulary.ConceptStatus;
}
