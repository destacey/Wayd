using System.Globalization;
using Bogus;
using static Wayd.Tools.DataGeneration.Cli.Generation.ProductManagementVocabulary;

namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// Generates a product catalog and its delivery history from a <see cref="ProductCatalog"/>: each value
/// stream's product line with its own environments, each ART's product, and the components each team owns —
/// so the catalog maps onto teams that exist, and onto the projects named after those components.
/// <para>
/// Components ship on their own cadence across the history: services often, web and mobile applications
/// less, libraries and tools rarely. A minority of ARTs ship their services together as release packages on
/// a train instead. Every version a service or package ships walks the product line's environments, and a
/// production deployment fails or is rolled back at a rate that varies by team and falls over time — which
/// is what makes deployment frequency and change failure rate move rather than sit flat. Releases announce,
/// per product line and period, what shipped.
/// </para>
/// </summary>
public sealed class ProductManagementGenerator
{
    /// <summary>The area name this generator draws its seed under.</summary>
    public const string AreaName = "product-management";

    /// <summary>How far past today versions and packages are planned. Nobody schedules two years of versions.</summary>
    private const int PlanningHorizonDays = 90;

    /// <summary>The share of production failures that deployed successfully and were reverted afterwards.</summary>
    private const double RollbackShare = 0.4;

    private const double PackagedChangeProbability = 0.75;

    private readonly ProductCatalog _catalog;
    private readonly ProductManagementOptions _options;
    private readonly GenerationContext _context;
    private readonly Faker _faker;

    private readonly List<DeploymentEnvironmentModel> _environments = [];
    private readonly List<ProductModel> _products = [];
    private readonly List<VersionModel> _versions = [];
    private readonly List<ReleasePackageModel> _packages = [];
    private readonly List<ReleasePackageComponentModel> _packageComponents = [];
    private readonly List<ReleaseModel> _releases = [];
    private readonly List<ReleaseContentModel> _releaseContents = [];
    private readonly List<DeploymentModel> _deployments = [];

    private readonly List<ProductLine> _lines = [];

    /// <param name="catalog">
    /// The catalog every generator in the run shares, so PPM and Planning name the same components. Derived
    /// from the options when omitted — a pure function of the org, the run's seed and its shape.
    /// </param>
    public ProductManagementGenerator(OrgStructure org, ProductManagementOptions options, GenerationContext context, ProductCatalog? catalog = null)
    {
        _catalog = catalog ?? ProductCatalog.From(org, context, ProductCatalogShape.For(options));
        _options = options;
        _context = context;

        // Derived from the area name, so adding another generator does not shift this one's data.
        _faker = new Faker { Random = new Randomizer(context.SeedFor(AreaName)) };
    }

    /// <summary>
    /// Where delivery history begins: the window's start, unless the company is younger than the window.
    /// </summary>
    private DateOnly HistoryStart => _context.WindowStart > _context.FoundedOn ? _context.WindowStart : _context.FoundedOn;

    private DateOnly Today => _context.AsOf;

    private DateOnly Horizon
    {
        get
        {
            var planned = Today.AddDays(PlanningHorizonDays);
            return planned < _context.WindowEnd ? planned : _context.WindowEnd;
        }
    }

    public GeneratedProductManagement Generate()
    {
        BuildCatalog();

        foreach (var line in _lines)
        {
            foreach (var group in line.Groups)
            {
                if (group.Packaged)
                    BuildTrain(group);

                foreach (var component in group.Components.Where(c => !c.InTrain))
                    BuildCadence(component);
            }

            BuildReleases(line);
        }

        // Last, and under its own seed, so the delivery history above is the same with or without it, and
        // each link can start no earlier than both of its ends first shipped.
        var dependencies = _options.Dependencies
            ? new ProductDependencyGenerator(_catalog, FirstShipped(), _options, _context).Generate()
            : [];

        return new GeneratedProductManagement(
            _environments,
            _products,
            _versions,
            _packages,
            _packageComponents,
            _releases,
            _releaseContents,
            _deployments,
            dependencies);
    }

    /// <summary>The day each product first shipped a version, by name. A product with nothing shipped by today is absent.</summary>
    private Dictionary<string, DateOnly> FirstShipped() =>
        _versions
            .Select(v => (v.ProductName, Shipped: v.ReleasedDate ?? v.CutDate))
            .Where(v => v.Shipped is { } shipped && shipped <= Today)
            .GroupBy(v => v.ProductName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Min(v => v.Shipped!.Value), StringComparer.OrdinalIgnoreCase);

    // ---- Catalog ------------------------------------------------------------------------------

    private sealed class BuildCounter(int start)
    {
        public int Value { get; set; } = start;
    }

    private sealed class ProductLine
    {
        public required string Name { get; init; }
        public required string Code { get; init; }
        public required string Development { get; init; }
        public required string Testing { get; init; }
        public string? Acceptance { get; init; }
        public DateOnly? AcceptanceRetiredOn { get; init; }
        public required string Staging { get; init; }
        public required string Production { get; init; }
        public required bool Monthly { get; init; }
        public List<ArtProduct> Groups { get; } = [];
        public bool InFlightUsed { get; set; }
    }

    /// <summary>The product one ART delivers, and the components its teams own.</summary>
    private sealed class ArtProduct
    {
        public required string Code { get; init; }
        public required string ProductName { get; init; }
        public required ProductLine Line { get; init; }
        public required bool Packaged { get; init; }
        public required BuildCounter Builds { get; init; }
        public required double Tempo { get; init; }
        public required double Risk { get; init; }
        public List<Component> Components { get; } = [];
        public List<ReleasePackageModel> ShippedPackages { get; } = [];
        public List<ReleasePackageModel> PendingPackages { get; } = [];
    }

    private sealed class Component
    {
        public required string Name { get; init; }
        public required ComponentKind Kind { get; init; }
        public required string Status { get; init; }
        public required ArtProduct Group { get; init; }
        public required double Tempo { get; init; }
        public required double Risk { get; init; }
        public required BuildCounter Builds { get; init; }
        public DateOnly? SunsetOn { get; init; }
        public DateOnly? RetiredOn { get; init; }
        public int Major { get; set; }
        public int Minor { get; set; }
        public int Patch { get; set; }
        public string? LastNumber { get; set; }
        public bool InTrain { get; set; }

        /// <summary>Services and web applications are deployed; everything else is published.</summary>
        public bool Deploys => Kind is ComponentKind.Service or ComponentKind.WebApplication;

        /// <summary>Whether a release announces this component's versions. Libraries and tools are internal.</summary>
        public bool CustomerFacing => Kind is not (ComponentKind.Library or ComponentKind.Tool);

        public List<VersionModel> Versions { get; } = [];
    }

    private void BuildCatalog()
    {
        // Chosen as a count rather than a coin flip per ART: a handful of ARTs flipping at 25% lands on
        // none often enough that a seed would regularly show no release packages at all.
        var arts = _catalog.Lines.SelectMany(l => l.Products).ToList();
        var packagedCount = arts.Count == 0 || _options.PackagedArtFraction <= 0
            ? 0
            : Math.Clamp((int)Math.Round(arts.Count * _options.PackagedArtFraction), 1, arts.Count);
        var packaged = _faker.PickRandom(arts, packagedCount).Select(a => a.ArtCode).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var plan in _catalog.Lines)
        {
            var line = AddProductLine(plan);

            foreach (var product in plan.Products)
            {
                AddProduct(product.Name, $"Delivered by the {product.ArtName}.", ProductType, line.Name, ActiveStatus, tags: null);

                var group = new ArtProduct
                {
                    Code = product.ArtCode,
                    ProductName = product.Name,
                    Line = line,
                    Packaged = packaged.Contains(product.ArtCode),
                    Builds = new BuildCounter(_faker.Random.Int(100, 900)),
                    Tempo = _faker.Random.Double(0.8, 1.3),
                    Risk = _faker.Random.Double(0.5, 1.5),
                };
                line.Groups.Add(group);

                foreach (var component in product.Components)
                {
                    if (component.IsConcept)
                        AddConcept(group, component);
                    else
                        AddComponent(group, component);
                }
            }
        }
    }

    private ProductLine AddProductLine(ProductLinePlan plan)
    {
        AddProduct(plan.Name, $"The {plan.Domain} product line.", ProductLineType, parentName: null, ActiveStatus, tags: null);

        var slug = plan.Slug;

        // About half the lines once ran a user-acceptance environment they have since retired, so a
        // backfill has history pointing at an environment nobody deploys into any more.
        DateOnly? acceptanceRetiredOn = null;
        var span = Today.DayNumber - HistoryStart.DayNumber;
        if (span > 120 && _faker.Random.Bool())
            acceptanceRetiredOn = HistoryStart.AddDays(_faker.Random.Int(span / 4, span * 2 / 3));

        var line = new ProductLine
        {
            Name = plan.Name,
            Code = plan.Code,
            Development = AddEnvironment($"{slug}-dev", Development, 0, isActive: true),
            Testing = AddEnvironment($"{slug}-test", Testing, 1, isActive: true),
            Acceptance = acceptanceRetiredOn is null ? null : AddEnvironment($"{slug}-uat", Testing, 2, isActive: false),
            AcceptanceRetiredOn = acceptanceRetiredOn,
            Staging = AddEnvironment($"{slug}-staging", Staging, 3, isActive: true),
            Production = AddEnvironment($"{slug}-prod", Production, 4, isActive: true),
            Monthly = _faker.Random.Double() < 0.4,
        };

        _lines.Add(line);
        return line;
    }

    private void AddComponent(ArtProduct group, ComponentPlan plan)
    {
        AddProduct(plan.Name, $"{Describe(plan.Kind)} owned by the {plan.TeamName} team.", TypeOf(plan.Kind), group.ProductName, plan.Status, TagsFor(plan.Kind));

        group.Components.Add(new Component
        {
            Name = plan.Name,
            Kind = plan.Kind,
            Status = plan.Status,
            Group = group,
            Tempo = _faker.Random.Double(0.7, 1.5),
            Risk = _faker.Random.Double(0.4, 1.6),
            Builds = new BuildCounter(_faker.Random.Int(100, 900)),
            SunsetOn = plan.SunsetOn,
            RetiredOn = plan.RetiredOn,
            Major = _faker.Random.Int(1, 4),
            Minor = _faker.Random.Int(0, 12),
        });
    }

    private void AddConcept(ArtProduct group, ComponentPlan plan)
    {
        AddProduct(plan.Name, $"A service the {plan.TeamName} team is exploring.", ServiceType, group.ProductName, ConceptStatus, TagsFor(ComponentKind.Service));

        // Planned only: a concept has a target for its first version and nothing cut.
        if (Horizon <= Today)
            return;

        var target = ShipDay(Today.AddDays(_faker.Random.Int(21, Math.Max(21, Horizon.DayNumber - Today.DayNumber))));
        if (target <= Horizon)
            AddVersion(plan.Name, "0.1.0", target, cut: null, released: null, "First version for early adopters.");
    }

    // ---- Independent cadence ------------------------------------------------------------------

    /// <summary>
    /// Ships one component on its own: a version every interval, each walked through the environments,
    /// with a hotfix after a rollback. Released behind today, cut but not released around it, planned ahead.
    /// </summary>
    private void BuildCadence(Component component)
    {
        var date = HistoryStart.AddDays(CutLead(component.Kind, max: true) + _faker.Random.Int(0, (int)Interval(component, HistoryStart)));

        while (true)
        {
            var interval = Interval(component, date);
            var shipOn = ShipDay(date);

            if (component.RetiredOn is { } retiredOn && shipOn > retiredOn)
                return;

            var cut = PreviousWorkday(shipOn.AddDays(-CutLead(component.Kind)));
            DateOnly next;

            if (shipOn <= Today)
            {
                var version = AddVersion(component, NextNumber(component), Target(cut, shipOn), cut, shipOn, Pick(EngineeringNotes));

                next = shipOn.AddDays((int)interval);

                if (component.Deploys)
                {
                    var rolledBack = Deploy(component.Group.Line, version.Handle, null, component.Builds,
                        component.Risk, cut, shipOn, hotfix: false, version.Number);

                    if (rolledBack || _faker.Random.Double() < 0.05)
                        next = Later(next, Hotfix(component, shipOn));
                }
            }
            else if (cut <= Today)
            {
                var version = AddVersion(component, NextNumber(component), shipOn, cut, released: null, Pick(EngineeringNotes));

                if (component.Deploys)
                    DeployReady(component.Group.Line, version.Handle, null, component.Builds, cut, version.Number);

                next = shipOn.AddDays((int)interval);
            }
            else if (shipOn <= Horizon)
            {
                AddVersion(component, NextNumber(component), shipOn, cut: null, released: null, Pick(EngineeringNotes));
                next = shipOn.AddDays((int)interval);
            }
            else
            {
                return;
            }

            date = next;
        }
    }

    /// <summary>
    /// A patch shipped a few days after a bad release, straight through staging to production. Returns the
    /// day it shipped, or the day it would have, so the next regular version lands after it.
    /// </summary>
    private DateOnly Hotfix(Component component, DateOnly after)
    {
        var shipOn = ShipDay(after.AddDays(_faker.Random.Int(1, 3)));
        if (shipOn > Today)
            return after;

        component.Patch++;
        var number = CurrentNumber(component);
        var version = AddVersion(component, number, shipOn, shipOn, shipOn, Pick(HotfixNotes));

        Deploy(component.Group.Line, version.Handle, null, component.Builds, component.Risk, shipOn, shipOn, hotfix: true, number);

        return shipOn.AddDays(2);
    }

    // ---- Release trains -----------------------------------------------------------------------

    /// <summary>
    /// Ships an ART's services together: each train cuts a package whose manifest lists every service,
    /// changed or carried forward, and the package — not the services — walks the environments.
    /// </summary>
    private void BuildTrain(ArtProduct group)
    {
        var members = group.Components.Where(c => c.Deploys && c.Status != ConceptStatus).ToList();
        if (members.Count == 0)
            return;

        foreach (var member in members)
            member.InTrain = true;

        var sequenceByYear = new Dictionary<int, int>();
        var date = HistoryStart.AddDays(7 + _faker.Random.Int(0, (int)TrainInterval(group, HistoryStart)));

        while (true)
        {
            var interval = TrainInterval(group, date);
            var shipOn = ShipDay(date);
            if (shipOn > Horizon)
                return;

            var riding = members.Where(m => m.RetiredOn is null || shipOn <= m.RetiredOn).ToList();
            if (riding.Count == 0)
                return;

            var cut = PreviousWorkday(shipOn.AddDays(-_faker.Random.Int(3, 7)));
            var state = shipOn <= Today ? Stage.Released : cut <= Today ? Stage.Ready : Stage.Planned;

            sequenceByYear[shipOn.Year] = sequenceByYear.GetValueOrDefault(shipOn.Year) + 1;
            var packageVersion = $"{group.Code}-{shipOn.Year}.{sequenceByYear[shipOn.Year]:D2}";

            var package = AddPackage(packageVersion, $"{group.ProductName} {shipOn.ToString("MMMM yyyy", CultureInfo.InvariantCulture)}",
                state == Stage.Released ? Target(cut, shipOn) : shipOn, state == Stage.Released ? shipOn : null);

            var changed = false;
            foreach (var member in riding)
            {
                var changes = member.LastNumber is null
                    || _faker.Random.Double() < (member.Status == SunsetStatus ? 0.3 : PackagedChangeProbability)
                    || (!changed && member == riding[^1]);

                if (changes)
                {
                    changed = true;
                    AddVersion(member, NextNumber(member), shipOn,
                        state == Stage.Planned ? null : cut,
                        state == Stage.Released ? shipOn : null,
                        Pick(EngineeringNotes));
                }

                AddComponentLine(packageVersion, member.Name, member.LastNumber!, changes ? Changed : CarriedForward);
            }

            var next = shipOn.AddDays((int)interval);

            switch (state)
            {
                case Stage.Released:
                    group.ShippedPackages.Add(package);
                    var rolledBack = Deploy(group.Line, null, packageVersion, group.Builds, group.Risk, cut, shipOn, hotfix: false, packageVersion);
                    if (rolledBack || _faker.Random.Double() < 0.04)
                        next = Later(next, TrainHotfix(group, riding, packageVersion, shipOn));
                    break;

                case Stage.Ready:
                    group.PendingPackages.Add(package);
                    DeployReady(group.Line, null, packageVersion, group.Builds, cut, packageVersion);
                    break;

                default:
                    group.PendingPackages.Add(package);
                    break;
            }

            date = next;
        }
    }

    /// <summary>A patch package: one service changed, the rest carried forward, shipped a few days later.</summary>
    private DateOnly TrainHotfix(ArtProduct group, IReadOnlyList<Component> riding, string packageVersion, DateOnly after)
    {
        var shipOn = ShipDay(after.AddDays(_faker.Random.Int(1, 3)));
        if (shipOn > Today)
            return after;

        var fixedComponent = _faker.PickRandom(riding.ToList());
        fixedComponent.Patch++;
        AddVersion(fixedComponent, CurrentNumber(fixedComponent), shipOn, shipOn, shipOn, Pick(HotfixNotes));

        var hotfixVersion = $"{packageVersion}.1";
        AddPackage(hotfixVersion, $"{group.ProductName} hotfix", shipOn, shipOn);

        foreach (var member in riding)
            AddComponentLine(hotfixVersion, member.Name, member.LastNumber!, member == fixedComponent ? Changed : CarriedForward);

        Deploy(group.Line, null, hotfixVersion, group.Builds, group.Risk, shipOn, shipOn, hotfix: true, hotfixVersion);

        return shipOn.AddDays(2);
    }

    private enum Stage { Released, Ready, Planned }

    // ---- Deployments --------------------------------------------------------------------------

    /// <summary>
    /// Walks one shipped version or package through its line's environments and into production. The same
    /// build is promoted from one environment to the next; a failure is retried with a new build. Returns
    /// whether production was rolled back, which is what prompts a hotfix.
    /// </summary>
    private bool Deploy(ProductLine line, string? versionHandle, string? packageVersion, BuildCounter builds,
        double risk, DateOnly cut, DateOnly shipOn, bool hotfix, string artifactPrefix)
    {
        builds.Value += _faker.Random.Int(1, 6);
        var pipeline = new Pipeline(versionHandle, packageVersion, builds, artifactPrefix);

        if (!hotfix)
        {
            var testOn = NotAfter(Workday(cut.AddDays(_faker.Random.Int(0, 1))), shipOn);

            var devOn = NotAfter(Workday(cut), shipOn);
            Attempt(pipeline, line.Development, Development, devOn, failureRate: 0.05, morning: devOn == shipOn);
            Attempt(pipeline, line.Testing, Testing, testOn, failureRate: 0.08, morning: testOn == shipOn);

            if (line.Acceptance is not null && testOn < line.AcceptanceRetiredOn)
                Attempt(pipeline, line.Acceptance, Testing, testOn, failureRate: 0.06, morning: testOn == shipOn);

            var stagingOn = Later(PreviousWorkday(shipOn.AddDays(-_faker.Random.Int(0, 1))), testOn);
            Attempt(pipeline, line.Staging, Staging, stagingOn, failureRate: 0.05, morning: stagingOn == shipOn);
        }
        else
        {
            Attempt(pipeline, line.Staging, Staging, shipOn, failureRate: 0.02, morning: true);
        }

        return Attempt(pipeline, line.Production, Production, shipOn, ChangeFailureRate(risk, shipOn));
    }

    /// <summary>
    /// A version or package cut but not yet shipped: through the early environments, and — for one item per
    /// product line — into staging, still in flight.
    /// </summary>
    private void DeployReady(ProductLine line, string? versionHandle, string? packageVersion, BuildCounter builds,
        DateOnly cut, string artifactPrefix)
    {
        builds.Value += _faker.Random.Int(1, 6);
        var pipeline = new Pipeline(versionHandle, packageVersion, builds, artifactPrefix);

        var devOn = Workday(cut);
        if (devOn >= Today)
            return;

        Attempt(pipeline, line.Development, Development, devOn, failureRate: 0.05);

        var testOn = Workday(devOn.AddDays(_faker.Random.Int(0, 1)));
        if (testOn >= Today)
            return;

        Attempt(pipeline, line.Testing, Testing, testOn, failureRate: 0.08);

        if (line.InFlightUsed)
            return;

        line.InFlightUsed = true;
        var startedAt = At(Today, TimeOnly.MinValue).AddMinutes(-_faker.Random.Int(20, 90));
        if (startedAt <= pipeline.Cursor)
            return;

        _deployments.Add(new DeploymentModel
        {
            VersionHandle = versionHandle,
            PackageVersion = packageVersion,
            EnvironmentName = line.Staging,
            ArtifactId = pipeline.ArtifactId,
            StartedAt = startedAt,
        });
    }

    private sealed class Pipeline(string? versionHandle, string? packageVersion, BuildCounter builds, string artifactPrefix)
    {
        public string? VersionHandle { get; } = versionHandle;
        public string? PackageVersion { get; } = packageVersion;
        public BuildCounter Builds { get; } = builds;
        public string ArtifactId => $"{artifactPrefix}.{Builds.Value}";
        public DateTimeOffset Cursor { get; set; } = DateTimeOffset.MinValue;
    }

    /// <summary>
    /// Deploys the pipeline's current build into one environment. A failure outside production is retried
    /// with a new build; in production it either fails and is retried, or succeeds and is rolled back.
    /// Returns whether it ended rolled back.
    /// </summary>
    /// <param name="morning">
    /// Whether production follows later the same day. Those steps run early so that even a chain of
    /// retries leaves production on the day the version was released, which is the date the metrics read.
    /// </param>
    private bool Attempt(Pipeline pipeline, string environment, string category, DateOnly day, double failureRate, bool morning = false)
    {
        var hour = category == Production ? _faker.Random.Int(14, 17) : morning ? _faker.Random.Int(9, 11) : _faker.Random.Int(14, 19);
        var startedAt = At(day, new TimeOnly(hour, _faker.Random.Int(0, 59)));
        if (startedAt <= pipeline.Cursor)
            startedAt = pipeline.Cursor.AddMinutes(_faker.Random.Int(10, 40));

        var completedAt = startedAt.AddMinutes(Duration(category));

        if (_faker.Random.Double() >= failureRate)
        {
            Record(pipeline, environment, startedAt, Succeeded, completedAt);
            return false;
        }

        if (category == Production && _faker.Random.Double() < RollbackShare)
        {
            Record(pipeline, environment, startedAt, RolledBack, completedAt,
                rolledBackAt: completedAt.AddMinutes(_faker.Random.Int(20, 240)), reason: Pick(RollbackReasons));
            return true;
        }

        Record(pipeline, environment, startedAt, Failed, completedAt, reason: Pick(FailureReasons));

        pipeline.Builds.Value += _faker.Random.Int(1, 3);
        var retryAt = completedAt.AddMinutes(_faker.Random.Int(30, 90));
        Record(pipeline, environment, retryAt, Succeeded, retryAt.AddMinutes(Duration(category)));

        return false;
    }

    private void Record(Pipeline pipeline, string environment, DateTimeOffset startedAt, string outcome,
        DateTimeOffset completedAt, DateTimeOffset? rolledBackAt = null, string? reason = null)
    {
        _deployments.Add(new DeploymentModel
        {
            VersionHandle = pipeline.VersionHandle,
            PackageVersion = pipeline.PackageVersion,
            EnvironmentName = environment,
            ArtifactId = pipeline.ArtifactId,
            StartedAt = startedAt,
            Outcome = outcome,
            CompletedAt = completedAt,
            RolledBackAt = rolledBackAt,
            Reason = reason,
        });

        pipeline.Cursor = rolledBackAt ?? completedAt;
    }

    private int Duration(string category) => category switch
    {
        Development => _faker.Random.Int(4, 12),
        Testing => _faker.Random.Int(6, 18),
        Staging => _faker.Random.Int(8, 25),
        _ => _faker.Random.Int(10, 40),
    };

    // ---- Releases -----------------------------------------------------------------------------

    /// <summary>
    /// Announces what each product line shipped, one release per month or quarter: the last version of each
    /// customer-facing component and every package shipped in the period. Past periods are released a few
    /// days after they close; the current and next are still being planned.
    /// </summary>
    private void BuildReleases(ProductLine line)
    {
        var components = line.Groups.SelectMany(g => g.Components)
            .Where(c => c.CustomerFacing && !c.InTrain)
            .ToList();

        var (start, end) = Period(line, HistoryStart);
        var periodsAfterToday = 0;

        while (periodsAfterToday < 2)
        {
            var past = end < Today;
            if (!past)
                periodsAfterToday++;

            var versions = components
                .Select(c => c.Versions
                    .Where(v => !IsHotfix(v) && InPeriod(past ? v.ReleasedDate : v.ReleasedDate ?? v.TargetDate, start, end))
                    .LastOrDefault())
                .OfType<VersionModel>()
                .ToList();

            var packages = line.Groups
                .SelectMany(g => past ? g.ShippedPackages : g.ShippedPackages.Concat(g.PendingPackages))
                .Where(p => InPeriod(p.ReleasedDate ?? p.TargetDate, start, end))
                .ToList();

            if (!past || versions.Count + packages.Count > 0)
                AddRelease(line, start, end, past, versions, packages);

            (start, end) = Period(line, end.AddDays(1));
        }
    }

    private void AddRelease(ProductLine line, DateOnly start, DateOnly end, bool past,
        IReadOnlyList<VersionModel> versions, IReadOnlyList<ReleasePackageModel> packages)
    {
        var quarter = (start.Month - 1) / 3 + 1;
        var version = line.Monthly
            ? $"{line.Code} {start.ToString("yyyy.MM", CultureInfo.InvariantCulture)}"
            : $"{line.Code} {start.Year} Q{quarter}";
        var name = line.Monthly
            ? $"{line.Name} {start.ToString("MMMM yyyy", CultureInfo.InvariantCulture)}"
            : $"{line.Name} Q{quarter} {start.Year}";

        // Announced a few days after the period closes. One that would be announced after today has not
        // been yet, even though everything in it has shipped.
        DateOnly? releasedOn = past ? ShipDay(end.AddDays(_faker.Random.Int(1, 5))) : null;
        if (releasedOn > Today)
            releasedOn = null;

        var highlights = _faker.PickRandom(ReleaseHighlights, 2).ToList();

        _releases.Add(new ReleaseModel
        {
            Version = version,
            Name = name,
            ProductName = line.Name,
            TargetDate = Workday(end.AddDays(3)),
            ReleasedDate = releasedOn,
            Notes = $"{char.ToUpperInvariant(highlights[0][0])}{highlights[0][1..]} and {highlights[1]} across {line.Name}.",
        });

        foreach (var package in packages)
        {
            _releaseContents.Add(new ReleaseContentModel { ReleaseVersion = version, Kind = "Package", PackageVersion = package.Version });
        }

        foreach (var shipped in versions)
        {
            _releaseContents.Add(new ReleaseContentModel { ReleaseVersion = version, Kind = "Version", VersionHandle = shipped.Handle });
        }
    }

    private static (DateOnly Start, DateOnly End) Period(ProductLine line, DateOnly on)
    {
        var start = line.Monthly
            ? new DateOnly(on.Year, on.Month, 1)
            : new DateOnly(on.Year, (on.Month - 1) / 3 * 3 + 1, 1);

        return (start, start.AddMonths(line.Monthly ? 1 : 3).AddDays(-1));
    }

    private static bool InPeriod(DateOnly? date, DateOnly start, DateOnly end) =>
        date is { } d && d >= start && d <= end;

    /// <summary>A regular version resets the patch number, so anything else is a hotfix.</summary>
    private static bool IsHotfix(VersionModel version) => !version.Number.EndsWith(".0", StringComparison.Ordinal);

    // ---- Row builders -------------------------------------------------------------------------

    private void AddProduct(string name, string description, string typeName, string? parentName, string status, string? tags) =>
        _products.Add(new ProductModel
        {
            Name = name,
            Description = description,
            ProductTypeName = typeName,
            ParentName = parentName,
            Status = status,
            Tags = tags,
        });

    /// <summary>Unique because product line slugs are.</summary>
    private string AddEnvironment(string name, string category, int ringOrder, bool isActive)
    {
        _environments.Add(new DeploymentEnvironmentModel { Name = name, Category = category, RingOrder = ringOrder, IsActive = isActive });

        return name;
    }

    private VersionModel AddVersion(Component component, string number, DateOnly target, DateOnly? cut, DateOnly? released, string notes)
    {
        var version = AddVersion(component.Name, number, target, cut, released, notes);
        component.Versions.Add(version);
        component.LastNumber = number;

        return version;
    }

    private VersionModel AddVersion(string productName, string number, DateOnly target, DateOnly? cut, DateOnly? released, string notes)
    {
        var version = new VersionModel
        {
            ProductName = productName,
            Number = number,
            TargetDate = target,
            CutDate = cut,
            ReleasedDate = released,
            Notes = notes,
        };

        _versions.Add(version);
        return version;
    }

    private ReleasePackageModel AddPackage(string version, string name, DateOnly target, DateOnly? released)
    {
        var package = new ReleasePackageModel { Version = version, Name = name, TargetDate = target, ReleasedDate = released };
        _packages.Add(package);

        return package;
    }

    private void AddComponentLine(string packageVersion, string productName, string versionNumber, string kind) =>
        _packageComponents.Add(new ReleasePackageComponentModel
        {
            PackageVersion = packageVersion,
            ProductName = productName,
            VersionNumber = versionNumber,
            Kind = kind,
        });

    /// <summary>The next regular version: a minor bump, and now and then a major one.</summary>
    private string NextNumber(Component component)
    {
        if (component.LastNumber is not null)
        {
            if (_faker.Random.Double() < 0.03)
            {
                component.Major++;
                component.Minor = 0;
            }
            else
            {
                component.Minor++;
            }
        }

        component.Patch = 0;
        return CurrentNumber(component);
    }

    private static string CurrentNumber(Component component) => $"{component.Major}.{component.Minor}.{component.Patch}";

    // ---- Timeline -----------------------------------------------------------------------------

    /// <summary>How far through the history a date sits, from 0 at its start to 1 at today and after.</summary>
    private double Progress(DateOnly date)
    {
        var span = Today.DayNumber - HistoryStart.DayNumber;
        return span <= 0 ? 1 : Math.Clamp((date.DayNumber - HistoryStart.DayNumber) / (double)span, 0, 1);
    }

    /// <summary>
    /// Days until a component's next version. Teams speed up across the history — the factor runs from 1.3
    /// down to 0.85 — so deployment frequency climbs toward today rather than holding flat.
    /// </summary>
    private double Interval(Component component, DateOnly date)
    {
        var kindFactor = component.Kind switch
        {
            ComponentKind.Service => 1.0,
            ComponentKind.WebApplication => 1.4,
            ComponentKind.MobileApplication => 2.0,
            ComponentKind.Library => 3.0,
            _ => 3.5,
        };

        var winding = component.SunsetOn is { } sunsetOn && date > sunsetOn ? 3.0 : 1.0;
        var days = _options.VersionIntervalDays * kindFactor * component.Tempo * winding * (1.3 - 0.45 * Progress(date));

        return Math.Max(3, days * _faker.Random.Double(0.6, 1.4));
    }

    /// <summary>A train runs slower than any one service would on its own, since it waits for all of them.</summary>
    private double TrainInterval(ArtProduct group, DateOnly date) =>
        Math.Max(7, _options.VersionIntervalDays * 2.5 * group.Tempo * (1.3 - 0.45 * Progress(date)) * _faker.Random.Double(0.8, 1.2));

    /// <summary>
    /// The chance a production deployment fails or is rolled back: the org's rate, scaled by how risky this
    /// team's changes are and falling from 1.5× at the start of the history to 0.6× by today.
    /// </summary>
    private double ChangeFailureRate(double risk, DateOnly date) =>
        Math.Clamp(_options.ChangeFailureRate * risk * (1.5 - 0.9 * Progress(date)), 0, 0.9);

    /// <summary>Days from scope freeze to shipping, which depends on how much a kind of component is tested.</summary>
    private int CutLead(ComponentKind kind, bool max = false) => kind switch
    {
        ComponentKind.Service => max ? 4 : _faker.Random.Int(1, 4),
        ComponentKind.WebApplication => max ? 6 : _faker.Random.Int(2, 6),
        ComponentKind.MobileApplication => max ? 10 : _faker.Random.Int(5, 10),
        _ => max ? 2 : _faker.Random.Int(0, 2),
    };

    /// <summary>A day something can ship on: a weekday, and not inside the year-end change freeze.</summary>
    private static DateOnly ShipDay(DateOnly date)
    {
        if (date is { Month: 12, Day: >= 22 })
            date = new DateOnly(date.Year + 1, 1, 3);
        else if (date is { Month: 1, Day: <= 2 })
            date = new DateOnly(date.Year, 1, 3);

        return Workday(date);
    }

    private static DateOnly Workday(DateOnly date) => date.DayOfWeek switch
    {
        DayOfWeek.Saturday => date.AddDays(2),
        DayOfWeek.Sunday => date.AddDays(1),
        _ => date,
    };

    private static DateOnly PreviousWorkday(DateOnly date) => date.DayOfWeek switch
    {
        DayOfWeek.Saturday => date.AddDays(-1),
        DayOfWeek.Sunday => date.AddDays(-2),
        _ => date,
    };

    /// <summary>
    /// What a shipped item was aiming for. A quarter of them slipped a few days, but a target is never set
    /// before scope was even frozen.
    /// </summary>
    private DateOnly Target(DateOnly cut, DateOnly shipOn)
    {
        var slip = _faker.Random.Double() < 0.25 ? _faker.Random.Int(1, 7) : 0;
        return Later(PreviousWorkday(shipOn.AddDays(-slip)), cut);
    }

    private static DateOnly NotAfter(DateOnly date, DateOnly limit) => date > limit ? limit : date;

    private static DateOnly Later(DateOnly a, DateOnly b) => a > b ? a : b;

    private static DateTimeOffset At(DateOnly day, TimeOnly time) => new(day.ToDateTime(time), TimeSpan.Zero);

    // ---- Product types ------------------------------------------------------------------------

    private static string TypeOf(ComponentKind kind) => kind switch
    {
        ComponentKind.Service => ServiceType,
        ComponentKind.WebApplication or ComponentKind.MobileApplication => ApplicationType,
        ComponentKind.Library => LibraryType,
        _ => ToolType,
    };

    private static string Describe(ComponentKind kind) => kind switch
    {
        ComponentKind.Service => "A backend service",
        ComponentKind.WebApplication => "A web application",
        ComponentKind.MobileApplication => "A mobile app",
        ComponentKind.Library => "A client library",
        _ => "A command-line tool",
    };

    private static string? TagsFor(ComponentKind kind) => kind switch
    {
        ComponentKind.Service => PlatformTag("server"),
        ComponentKind.WebApplication => PlatformTag("web"),
        ComponentKind.MobileApplication => $"{PlatformTag("ios")};{PlatformTag("android")}",
        ComponentKind.Tool => PlatformTag("cli"),
        _ => null,
    };

    private string Pick(string[] pool) => _faker.PickRandom(pool);
}
