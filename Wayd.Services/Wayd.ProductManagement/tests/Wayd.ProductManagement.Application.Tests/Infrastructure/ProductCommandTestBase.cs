using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Settings;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Domain.Models;

// The delivery artifact record, not System.Version.
using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.ProductManagement.Application.Tests.Infrastructure;

/// <summary>
/// The scaffolding every Product command handler test needs: a fake context, a frozen clock, a known
/// actor, and seeding for the rows the handlers look up before touching the aggregate.
/// </summary>
public abstract class ProductCommandTestBase
{
    protected static readonly Instant Now = Instant.FromUtc(2026, 4, 1, 9, 0, 0);
    protected static readonly LocalDate Today = new(2026, 4, 1);

    /// <summary>
    /// The employee the acting account is linked to, frozen onto every transition these commands write.
    /// </summary>
    protected static readonly Guid ActingEmployeeId = Guid.CreateVersion7();

    protected readonly FakeProductManagementDbContext DbContext = new();
    protected readonly Mock<ICurrentUser> CurrentUser = new();
    protected readonly Mock<ICurrentPrincipal> CurrentPrincipal = new();
    protected readonly Mock<IDateTimeProvider> DateTimeProvider = new();

    /// <summary>The product tag settings in effect: free tags off, as a new install has them.</summary>
    protected readonly Mock<ISettings<ProductTagSettings>> TagSettings = new();

    protected ProductCommandTestBase()
    {
        CurrentUser.Setup(u => u.GetUserId()).Returns(Guid.CreateVersion7().ToString());
        CurrentPrincipal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActingEmployeeId);
        DateTimeProvider.SetupGet(d => d.Now).Returns(Now);
        DateTimeProvider.SetupGet(d => d.Today).Returns(Today);
        TagSettings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(new ProductTagSettings());
    }

    /// <summary>Turns free tags on for products.</summary>
    protected void AllowFreeTags() =>
        TagSettings.Setup(s => s.Get(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductTagSettings { AllowsFreeTags = true });

    /// <summary>A saved free tag.</summary>
    protected ProductTag SeedFreeTag(string name = "migration wave")
    {
        var tag = ProductTag.CreateFree(name, EventActor.System, Now).Value;
        tag.ClearDomainEvents();
        DbContext.AddProductTag(tag);

        return tag;
    }

    /// <summary>
    /// Records the tags the product carries as saved assignments. The fake keeps no link between a product's own
    /// collection and the assignment set, which is what "does anything else carry this tag" reads.
    /// </summary>
    protected void SaveAssignments(Product product)
    {
        foreach (var assignment in product.Tags)
            DbContext.AddProductTagAssignment(assignment);
    }

    protected static ILogger<T> Logger<T>() => Mock.Of<ILogger<T>>();

    protected static StatusRef Status(
        string name = "Active",
        StatusCategory category = StatusCategory.Active,
        ProductStatusAlias alias = ProductStatusAlias.Active) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), name, category, (int)alias);

    protected ProductType SeedType(string name = "Application", bool isReleasable = true, bool isActive = true)
    {
        var productType = ProductType.Create(name, null, isReleasable, 1);

        if (!isActive)
        {
            productType.Deactivate();
        }

        DbContext.AddProductType(productType);

        return productType;
    }

    protected Product SeedProduct(
        string name = "Checkout",
        Guid? parentId = null,
        Guid? productTypeId = null,
        StatusRef? status = null,
        string? description = null,
        string? externalId = null)
    {
        var product = Product.Create(
            name,
            description,
            productTypeId ?? Guid.CreateVersion7(),
            parentId,
            externalId,
            status ?? Status(),
            EventActor.System,
            Now);

        // Created rows are already persisted as far as these tests are concerned, so the opening
        // transition and the deferred creation event are not part of what the command under test did.
        product.ClearDomainEvents();
        DbContext.AddProduct(product);

        return product;
    }

    /// <summary>
    /// A version against a product, for the handlers that refuse a change once one exists.
    /// </summary>
    protected Version SeedVersion(Guid productId, string number = "1.0", StatusRef? status = null)
    {
        var version = Version.Create(
            productId,
            number,
            null,
            null,
            null,
            isProductReleasable: true,
            status ?? Status("Planned", StatusCategory.Proposed, ProductStatusAlias.None),
            "Checkout",
            EventActor.System,
            Now).Value;

        version.ClearDomainEvents();
        DbContext.AddVersion(version);

        return version;
    }

    /// <summary>
    /// A release, for the handlers that amend an announcement's contents or announce it.
    /// </summary>
    /// <remarks>
    /// Seeded with no contents. The handlers under test attach them, and a release legitimately starts
    /// empty — unlike a package, whose manifest is authored up front.
    /// </remarks>
    protected Release SeedRelease(Guid? productId = null, string version = "2026.07", StatusRef? status = null)
    {
        var release = Release.Create(
            productId,
            version,
            null,
            null,
            null,
            status ?? Status("Planned", StatusCategory.Proposed, ProductStatusAlias.None),
            EventActor.System,
            Now).Value;

        release.ClearDomainEvents();
        DbContext.AddRelease(release);

        return release;
    }

    /// <summary>
    /// A package carrying a real manifest, built through the aggregate's own factory.
    /// </summary>
    /// <remarks>
    /// The manifest is populated because every interesting refusal depends on it. Note that a package
    /// seeded here always has its components in memory, so a handler that omits
    /// <c>.Include(p =&gt; p.Components)</c> still passes — <c>ReleasePackageIncludeTests</c> covers that.
    /// </remarks>
    /// <param name="versionId">
    /// The release the manifest line came from, where the test needs one. Null by default because a
    /// manifest line legitimately names a version that was never cut as a release here.
    /// </param>
    protected ReleasePackage SeedReleasePackage(
        Guid productId,
        string version = "2026.14",
        StatusRef? status = null,
        Guid? versionId = null)
    {
        var package = ReleasePackage.Create(
            version,
            null,
            null,
            [(productId, versionId, "1.0", ManifestEntryKind.Changed)],
            status ?? Status("Planned", StatusCategory.Proposed, ProductStatusAlias.None),
            EventActor.System,
            Now).Value;

        package.ClearDomainEvents();
        DbContext.AddReleasePackage(package);

        // The manifest lines are their own set, and a real provider populates it through the
        // navigation. Nothing does that here, so a query filtering on components — "which packages
        // name this release?" — would find none unless they are added explicitly.
        foreach (var component in package.Components)
        {
            DbContext.AddReleasePackageComponent(component);
        }

        return package;
    }

    protected DeploymentEnvironment SeedEnvironment(
        string name = "Production",
        EnvironmentCategory category = EnvironmentCategory.Production,
        int ringOrder = 3)
    {
        var environment = DeploymentEnvironment.Create(name, category, ringOrder, EventActor.System, Now);
        environment.ClearDomainEvents();
        DbContext.AddDeploymentEnvironment(environment);

        return environment;
    }

    /// <summary>
    /// An in-flight deployment against a seeded environment.
    /// </summary>
    /// <param name="category">
    /// Frozen onto the deployment, as the handler does — the change-failure predicate reads this rather
    /// than the environment.
    /// </param>
    /// <param name="environment">The environment it went into; a fresh one of <paramref name="category"/> when omitted.</param>
    /// <param name="package">The package deployed; a fresh version when omitted.</param>
    protected Deployment SeedDeployment(
        EnvironmentCategory category = EnvironmentCategory.Production,
        DeploymentEnvironment? environment = null,
        ReleasePackage? package = null)
    {
        environment ??= SeedEnvironment($"env-{Guid.CreateVersion7()}"[..12], category, 1);
        Guid? versionId = null;
        if (package is null)
        {
            var product = SeedProduct($"product-{Guid.CreateVersion7()}"[..16]);
            versionId = SeedVersion(product.Id).Id;
        }

        var deployment = Deployment.Create(
            versionId,
            package?.Id,
            environment.Id,
            category,
            null,
            Now,
            Status("In Progress", StatusCategory.Active, ProductStatusAlias.InProgress),
            environment.Name,
            EventActor.System,
            Now).Value;

        deployment.ClearDomainEvents();
        DbContext.AddDeployment(deployment);

        return deployment;
    }

    /// <summary>
    /// A dependency recorded through the aggregate, and added to the set queries read.
    /// </summary>
    /// <remarks>
    /// Skips the composition check, which is the add handler's to exercise. Ended at
    /// <paramref name="endsOn"/> where one is given.
    /// </remarks>
    protected ProductDependency SeedDependency(
        Product product,
        Guid dependsOnProductId,
        DependencyStrength strength = DependencyStrength.Hard,
        LocalDate? startsOn = null,
        LocalDate? endsOn = null,
        string? description = null,
        InteractionStyle? interactionStyle = null)
    {
        var dependency = product.AddDependency(
            dependsOnProductId, strength, interactionStyle, description, startsOn ?? Today.PlusDays(-30), [], [], Today, EventActor.System, Now).Value;

        if (endsOn is not null)
        {
            product.EndDependency(dependency.Id, endsOn.Value, Today, EventActor.System, Now);
        }

        product.ClearDomainEvents();
        DbContext.AddProductDependencies([dependency]);

        return dependency;
    }

    /// <summary>The module's copy of a team, as the sync or the <c>Team*</c> events leave it.</summary>
    protected ProductTeam SeedTeam(string name = "Payments", string code = "PAY", TeamType type = TeamType.Team, bool isActive = true)
    {
        var team = new ProductTeam(new TeamSource(Guid.CreateVersion7(), ++_teamKey, name, new TeamCode(code), type, isActive), Now);
        DbContext.AddProductTeam(team);

        return team;
    }

    private int _teamKey;

    /// <summary>
    /// A team link recorded through the aggregate, and added to the set queries read.
    /// </summary>
    /// <remarks>
    /// The team is set on the link's navigation, which only EF fills: the read queries project through it, and
    /// in memory an unset navigation would throw rather than join.
    /// </remarks>
    protected ProductTeamLink SeedTeamLink(Product product, ProductTeam team)
    {
        product.LinkTeam(team, EventActor.System, Now);
        var link = product.TeamLinks.Single(l => l.TeamId == team.Id);

        typeof(ProductTeamLink).GetProperty(nameof(ProductTeamLink.Team))!.SetValue(link, team);

        product.ClearDomainEvents();
        DbContext.AddProductTeamLinks([link]);

        return link;
    }

    /// <summary>A team as Organization holds it, for building the module's copy.</summary>
    protected sealed record TeamSource(Guid Id, int Key, string Name, TeamCode Code, TeamType Type, bool IsActive) : ISimpleTeam;

    protected (ProductTagCategory Category, ProductTag Tag) SeedTag(
        string categoryName = "Platform",
        string tagName = "ios",
        bool allowsMany = true,
        bool categoryActive = true,
        bool tagActive = true)
    {
        var category = ProductTagCategory.Create(categoryName, null, allowsMany, 1, EventActor.System, Now);
        var tag = category.AddTag(tagName, null, EventActor.System, Now).Value;

        if (!tagActive)
        {
            category.DeactivateTag(tag.Id, EventActor.System, Now);
        }

        if (!categoryActive)
        {
            category.Deactivate(EventActor.System, Now);
        }

        category.ClearDomainEvents();
        DbContext.AddProductTagCategory(category);
        DbContext.AddProductTag(tag);

        return (category, tag);
    }
}
