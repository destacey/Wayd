using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli.Generation;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

/// <summary>
/// The generated catalog and delivery history, checked against the rules each Product Management import
/// enforces. Every import is atomic, so one row breaking one of these fails a whole file at seed time —
/// far from the generator code that produced it.
/// </summary>
public class ProductManagementGeneratorTests
{
    private static readonly DateOnly _asOf = new(2026, 6, 15);

    private static readonly HashSet<string> _releasableTypes =
        new(["Product", "Application", "Service", "Tool", "Library"], StringComparer.OrdinalIgnoreCase);

    private static GenerationContext Context(int seed = 1234) => new() { AsOf = _asOf, Seed = seed };

    private static GeneratedProductManagement Generate(
        ProductManagementOptions? options = null, OrgOptions? orgOptions = null, int seed = 1234)
    {
        var context = Context(seed);
        var org = new OrgGenerator(orgOptions ?? new OrgOptions { ValueStreams = 3, Teams = 18 }, context).Generate();

        return new ProductManagementGenerator(org.Structure, options ?? new ProductManagementOptions(), context).Generate();
    }

    private static readonly GeneratedProductManagement _default = Generate();

    [Fact]
    public void Generate_ProducesEveryKindOfRecord()
    {
        // Arrange & Act
        var data = _default;

        // Assert — an empty set here is a seed that skips an area and demonstrates nothing
        data.Environments.Should().NotBeEmpty();
        data.Products.Should().NotBeEmpty();
        data.Versions.Should().NotBeEmpty();
        data.ReleasePackages.Should().NotBeEmpty();
        data.Releases.Should().NotBeEmpty();
        data.Deployments.Should().NotBeEmpty();
    }

    [Fact]
    public void Generate_NamesEveryProductUniquelyAndEveryParentWithinTheFile()
    {
        // Arrange — a parent must be a row in the same file, and names are what the rows are keyed by
        var data = _default;
        var names = data.Products.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        var orphans = data.Products.Where(p => p.ParentName is not null && !names.Contains(p.ParentName)).ToList();

        // Assert
        names.Should().HaveCount(data.Products.Count);
        orphans.Should().BeEmpty();
    }

    [Fact]
    public void Generate_UsesOnlyTheTypesStatusesAndTagsWaydSeeds()
    {
        // Arrange — each is resolved by name, and a name the environment does not hold fails the row
        string[] types = ["Product Line", "Product", "Service", "Application", "Tool", "Library"];
        string[] statuses = ["Concept", "Active", "Sunset", "Retired"];
        string[] tags = ["Platform|web", "Platform|ios", "Platform|android", "Platform|desktop", "Platform|cli", "Platform|server"];

        // Act
        var products = _default.Products;

        // Assert
        products.Should().OnlyContain(p => types.Contains(p.ProductTypeName));
        products.Should().OnlyContain(p => statuses.Contains(p.Status));
        products.SelectMany(p => (p.Tags ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
            .Should().OnlyContain(t => tags.Contains(t));
    }

    [Fact]
    public void Generate_CutsVersionsOnlyAgainstReleasableProducts()
    {
        // Arrange
        var typeByName = _default.Products.ToDictionary(p => p.Name, p => p.ProductTypeName, StringComparer.OrdinalIgnoreCase);

        // Act
        var unreleasable = _default.Versions.Where(v => !_releasableTypes.Contains(typeByName[v.ProductName])).ToList();

        // Assert
        unreleasable.Should().BeEmpty();
    }

    [Fact]
    public void Generate_NumbersEachVersionOncePerProduct()
    {
        // Arrange & Act
        var duplicates = _default.Versions
            .GroupBy(v => v.Handle, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        // Assert
        duplicates.Should().BeEmpty();
    }

    [Fact]
    public void Generate_DatesEveryVersionConsistentlyWithToday()
    {
        // Arrange — the dates decide the status, so a released date ahead of today claims a version shipped
        // on a day that has not happened
        var versions = _default.Versions;

        // Act & Assert
        versions.Should().OnlyContain(v => v.ReleasedDate == null || v.CutDate == null || v.ReleasedDate >= v.CutDate);
        versions.Should().OnlyContain(v => v.TargetDate == null || v.CutDate == null || v.TargetDate >= v.CutDate);
        versions.Should().OnlyContain(v => v.ReleasedDate == null || v.ReleasedDate <= _asOf);
        versions.Should().OnlyContain(v => v.CutDate == null || v.CutDate <= _asOf);
        versions.Where(v => v.CutDate == null).Should().OnlyContain(v => v.TargetDate > _asOf);
    }

    [Fact]
    public void Generate_PutsNothingBeforeTheCompanyExisted()
    {
        // Arrange
        var foundedOn = Context().FoundedOn;

        // Act & Assert
        _default.Versions.Should().OnlyContain(v => (v.CutDate ?? v.TargetDate) >= foundedOn);
        _default.Deployments.Should().OnlyContain(d => DateOnly.FromDateTime(d.StartedAt.UtcDateTime) >= foundedOn);
    }

    [Fact]
    public void Generate_GivesEveryPackageAManifestOfDistinctExistingProducts()
    {
        // Arrange
        var data = _default;
        var products = data.Products.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var lines = data.ReleasePackageComponents.ToLookup(c => c.PackageVersion, StringComparer.OrdinalIgnoreCase);

        // Act & Assert — a package needs at least one line, and names a product at most once
        data.ReleasePackages.Select(p => p.Version).Should().OnlyHaveUniqueItems();
        data.ReleasePackages.Should().OnlyContain(p => lines[p.Version].Any());
        data.ReleasePackages.Should().OnlyContain(p =>
            lines[p.Version].Select(l => l.ProductName).Distinct(StringComparer.OrdinalIgnoreCase).Count() == lines[p.Version].Count());
        data.ReleasePackageComponents.Should().OnlyContain(c => products.Contains(c.ProductName));
    }

    [Fact]
    public void Generate_LinksEveryManifestLineToAVersionRecord()
    {
        // Arrange — a line naming a version that was never cut is stored as bare text, and a release then
        // cannot see that the version ships inside the package
        var handles = _default.Versions.Select(v => v.Handle).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        var unlinked = _default.ReleasePackageComponents
            .Where(c => !handles.Contains(VersionModel.HandleFor(c.ProductName, c.VersionNumber)))
            .ToList();

        // Assert
        unlinked.Should().BeEmpty();
    }

    [Fact]
    public void Generate_ReleasesOnlyWhatHasShipped()
    {
        // Arrange — the import refuses a released date while anything the release carries has not shipped
        var data = _default;
        var versions = data.Versions.ToDictionary(v => v.Handle, StringComparer.OrdinalIgnoreCase);
        var packages = data.ReleasePackages.ToDictionary(p => p.Version, StringComparer.OrdinalIgnoreCase);
        var contents = data.ReleaseContents.ToLookup(c => c.ReleaseVersion, StringComparer.OrdinalIgnoreCase);

        // Act
        var premature = data.Releases
            .Where(r => r.ReleasedDate is not null)
            .Where(r => contents[r.Version].Any(c => c.Kind == "Version"
                ? versions[c.VersionHandle!].ReleasedDate is null
                : packages[c.PackageVersion!].ReleasedDate is null))
            .Select(r => r.Version)
            .ToList();

        // Assert
        premature.Should().BeEmpty();
        data.Releases.Should().OnlyContain(r => r.ReleasedDate == null || r.ReleasedDate <= _asOf);
    }

    [Fact]
    public void Generate_NeverAnnouncesAVersionBothDirectlyAndInsideAPackage()
    {
        // Arrange
        var data = _default;
        var manifests = data.ReleasePackageComponents
            .ToLookup(c => c.PackageVersion, c => VersionModel.HandleFor(c.ProductName, c.VersionNumber), StringComparer.OrdinalIgnoreCase);

        // Act
        var doubled = data.ReleaseContents
            .GroupBy(c => c.ReleaseVersion, StringComparer.OrdinalIgnoreCase)
            .Where(release =>
            {
                var packaged = release.Where(c => c.Kind == "Package").SelectMany(c => manifests[c.PackageVersion!]).ToHashSet(StringComparer.OrdinalIgnoreCase);
                return release.Any(c => c.Kind == "Version" && packaged.Contains(c.VersionHandle!));
            })
            .Select(release => release.Key)
            .ToList();

        // Assert
        doubled.Should().BeEmpty();
        data.Releases.Select(r => r.Version).Should().OnlyHaveUniqueItems();
        data.ReleaseContents.Select(c => $"{c.ReleaseVersion}|{c.PackageVersion}|{c.VersionHandle}").Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Generate_RecordsEveryDeploymentTheImportAccepts()
    {
        // Arrange
        var data = _default;
        var environments = data.Environments.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
        var versions = data.Versions.Select(v => v.Handle).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var packages = data.ReleasePackages.Select(p => p.Version).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act & Assert
        data.Deployments.Should().OnlyContain(d => (d.VersionHandle == null) != (d.PackageVersion == null));
        data.Deployments.Should().OnlyContain(d => d.VersionHandle == null || versions.Contains(d.VersionHandle));
        data.Deployments.Should().OnlyContain(d => d.PackageVersion == null || packages.Contains(d.PackageVersion));
        data.Deployments.Should().OnlyContain(d => environments.ContainsKey(d.EnvironmentName));
        data.Deployments.Should().OnlyContain(d => (d.Outcome == null) == (d.CompletedAt == null));
        data.Deployments.Should().OnlyContain(d => d.CompletedAt == null || d.CompletedAt >= d.StartedAt);
        data.Deployments.Should().OnlyContain(d => (d.Outcome == "RolledBack") == (d.RolledBackAt != null));
        data.Deployments.Should().OnlyContain(d => d.RolledBackAt == null || d.RolledBackAt >= d.CompletedAt);
        data.Deployments.Select(d => d.ImportId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Generate_SendsOnlyFinishedDeploymentsToARetiredEnvironment()
    {
        // Arrange — a retired environment accepts history, never a deployment still in flight
        var retired = _default.Environments.Where(e => !e.IsActive).Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        var inFlight = _default.Deployments.Where(d => retired.Contains(d.EnvironmentName) && d.Outcome == null).ToList();

        // Assert
        inFlight.Should().BeEmpty();
    }

    [Fact]
    public void Generate_DeploysNothingAfterToday()
    {
        // Arrange
        var endOfToday = new DateTimeOffset(_asOf.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(1);

        // Act & Assert — an outcome is a fact about the past; only an in-flight deployment reaches today
        _default.Deployments.Where(d => d.Outcome != null).Should().OnlyContain(d => d.StartedAt < endOfToday);
        _default.Deployments.Where(d => d.Outcome == null).Should().OnlyContain(d => d.StartedAt < endOfToday);
    }

    [Fact]
    public void Generate_ReleasesAVersionOnTheDayProductionFirstReceivesIt()
    {
        // Arrange — deployment frequency is read from production, so a version shipped with no production
        // deployment would release something the metrics never saw
        var data = _default;
        var production = data.Environments.Where(e => e.Category == "Production").Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var firstProduction = data.Deployments
            .Where(d => d.VersionHandle != null && production.Contains(d.EnvironmentName))
            .GroupBy(d => d.VersionHandle!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => DateOnly.FromDateTime(g.Min(d => d.StartedAt).UtcDateTime), StringComparer.OrdinalIgnoreCase);

        // Act
        var mismatched = data.Versions
            .Where(v => firstProduction.TryGetValue(v.Handle, out var deployed) && deployed != v.ReleasedDate)
            .Select(v => v.Handle)
            .ToList();

        // Assert
        firstProduction.Should().NotBeEmpty();
        mismatched.Should().BeEmpty();
    }

    [Fact]
    public void Generate_FailsProductionAtRoughlyTheRequestedRate()
    {
        // Arrange — the rate the delivery metrics page reports: failed and rolled back over every finished
        // production deployment. A larger org, so the average is not at the mercy of a few teams.
        var data = Generate(orgOptions: new OrgOptions { ValueStreams = 4, Teams = 40 });
        var production = data.Environments.Where(e => e.Category == "Production").Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var finished = data.Deployments.Where(d => production.Contains(d.EnvironmentName) && d.Outcome != null).ToList();

        // Act
        var rate = finished.Count(d => d.Outcome != "Succeeded") / (double)finished.Count;

        // Assert
        rate.Should().BeInRange(0.07, 0.17);
    }

    [Fact]
    public void Generate_ImprovesDeliveryAcrossTheHistory()
    {
        // Arrange — the point of the trend: a flat line demonstrates nothing on the metrics page
        var data = Generate(orgOptions: new OrgOptions { ValueStreams = 4, Teams = 40 });
        var production = data.Environments.Where(e => e.Category == "Production").Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var midpoint = new DateTimeOffset(_asOf.AddYears(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var finished = data.Deployments.Where(d => production.Contains(d.EnvironmentName) && d.Outcome != null).ToList();

        var earlier = finished.Where(d => d.StartedAt < midpoint).ToList();
        var later = finished.Where(d => d.StartedAt >= midpoint).ToList();

        // Act
        static double FailureRate(IReadOnlyList<DeploymentModel> deployments) =>
            deployments.Count(d => d.Outcome != "Succeeded") / (double)deployments.Count;

        // Assert
        later.Count(d => d.Outcome != "Failed").Should().BeGreaterThan(earlier.Count(d => d.Outcome != "Failed"));
        FailureRate(later).Should().BeLessThan(FailureRate(earlier));
    }

    [Fact]
    public void Generate_ShipsNoPackagesWhenNoArtIsPackaged()
    {
        // Arrange & Act
        var data = Generate(new ProductManagementOptions { PackagedArtFraction = 0 });

        // Assert
        data.ReleasePackages.Should().BeEmpty();
        data.Deployments.Should().OnlyContain(d => d.PackageVersion == null);
    }

    [Fact]
    public void Generate_DeploysOnlyPackagesWhenEveryArtIsPackaged()
    {
        // Arrange — a service riding a train reaches production inside its package, never on its own, or
        // one change would be counted twice
        var data = Generate(new ProductManagementOptions { PackagedArtFraction = 1 });
        var typeByName = data.Products.ToDictionary(p => p.Name, p => p.ProductTypeName, StringComparer.OrdinalIgnoreCase);

        // Act
        var serviceDeployments = data.Deployments
            .Where(d => d.VersionHandle is { } handle && typeByName[handle.Split('|')[0]] == "Service")
            .ToList();

        // Assert
        data.ReleasePackages.Should().NotBeEmpty();
        serviceDeployments.Should().BeEmpty();
    }

    [Fact]
    public void Generate_ProducesTheSameDataForTheSameSeedAndDate()
    {
        // Arrange & Act
        var first = Generate(seed: 99);
        var second = Generate(seed: 99);

        // Assert
        second.Deployments.Select(d => d.ImportId).Should().Equal(first.Deployments.Select(d => d.ImportId));
        second.Versions.Select(v => v.Handle).Should().Equal(first.Versions.Select(v => v.Handle));
    }

    [Fact]
    public void Generate_StaysWithinTheSingleFileImportsForALargeOrganization()
    {
        // Arrange — products and environments are posted as one file each, and the product tree cannot be
        // split, so these have to fit under the 10,000-row cap on their own
        var data = Generate(orgOptions: new OrgOptions { ValueStreams = 6, Teams = 60 });

        // Act & Assert
        data.Products.Should().HaveCountLessThan(10_000);
        data.Environments.Should().HaveCountLessThan(10_000);
    }
}
