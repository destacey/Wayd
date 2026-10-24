using Microsoft.EntityFrameworkCore;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Infrastructure.Persistence.Initialization;
using Wayd.ProductManagement.Application.Deployments.Queries;
using Wayd.ProductManagement.Application.Products.Queries;
using Wayd.ProductManagement.Application.ReleasePackages.Queries;
using Wayd.ProductManagement.Application.Releases.Queries;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

// The delivery artifact record, not System.Version.
using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Reads <c>StatusEnteredOn</c> through each record's detail query on a real provider.
/// </summary>
/// <remarks>
/// The value comes from a correlated subquery into the shared transition log, reached through a second
/// DbContext interface. In-memory fakes run it as LINQ-to-Objects, so only a database shows that it
/// translates and picks the record's latest transition. <c>VersionLookupTests</c> covers the version.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class StatusEnteredOnProjectionTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    private static readonly Instant Created = Instant.FromUtc(2026, 3, 4, 10, 0, 0);
    private static readonly Instant Moved = Instant.FromUtc(2026, 3, 9, 15, 30, 0);

    private static IDateTimeProvider DateTimeProvider()
    {
        var provider = new Mock<IDateTimeProvider>();
        provider.SetupGet(d => d.Now).Returns(Created);
        provider.SetupGet(d => d.Today).Returns(new LocalDate(2026, 3, 4));

        return provider.Object;
    }

    /// <summary>The system workflow's statuses for one owner type, in order, seeding them on first use.</summary>
    private static async Task<List<StatusRef>> Statuses(WaydDbContext context, WorkflowOwnerDescriptor owner)
    {
        ProductWorkflowOwners.Register();

        if (!await context.StatusWorkflows.AnyAsync(
                w => w.OwnerType == owner.Key && w.IsSystem, TestContext.Current.CancellationToken))
        {
            await new ProductManagementWorkflowSeeder()
                .Initialize(context, DateTimeProvider(), TestContext.Current.CancellationToken);
        }

        var workflow = await context.StatusWorkflows
            .Include(w => w.Statuses)
            .FirstAsync(w => w.OwnerType == owner.Key && w.IsSystem, TestContext.Current.CancellationToken);

        return [.. workflow.Statuses.OrderBy(s => s.Order).Select(StatusRef.From)];
    }

    private static async Task<Product> SeedProduct(WaydDbContext context)
    {
        if (!await context.ProductTypes.AnyAsync(TestContext.Current.CancellationToken))
        {
            await new ProductTypeSeeder().Initialize(context, DateTimeProvider(), TestContext.Current.CancellationToken);
        }

        var productType = await context.ProductTypes
            .Where(t => t.IsReleasable)
            .Select(t => t.Id)
            .FirstAsync(TestContext.Current.CancellationToken);

        var product = Product.Create(
            $"Entered {Guid.CreateVersion7()}",
            null,
            productType,
            null,
            null,
            (await Statuses(context, ProductWorkflowOwners.Product))[0],
            EventActor.System,
            Created);

        context.Products.Add(product);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return product;
    }

    [Fact]
    public async Task Product_ShouldReportWhenItEnteredItsLatestStatus()
    {
        // Arrange — created, then moved five days later.
        await using var context = _fixture.CreateContext();
        var product = await SeedProduct(context);
        var statuses = await Statuses(context, ProductWorkflowOwners.Product);
        product.ChangeStatus(statuses[1], EventActor.System, Moved).IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var reader = _fixture.CreateContext();
        var sut = new GetProductQueryHandler(reader, reader);

        // Act
        var dto = await sut.Handle(
            new GetProductQuery(new IdOrKey(product.Id.ToString())), TestContext.Current.CancellationToken);

        // Assert
        dto!.StatusEnteredOn.Should().Be(Moved);
    }

    [Fact]
    public async Task Release_ShouldReportWhenItEnteredItsStatus()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var release = Release.Create(
            null,
            $"R-{Guid.CreateVersion7():N}"[..20],
            null,
            null,
            null,
            (await Statuses(context, ProductWorkflowOwners.Release))[0],
            EventActor.System,
            Created).Value;
        context.Releases.Add(release);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var reader = _fixture.CreateContext();
        var sut = new GetReleaseQueryHandler(reader, reader);

        // Act
        var dto = await sut.Handle(
            new GetReleaseQuery(new IdOrKey(release.Id.ToString())), TestContext.Current.CancellationToken);

        // Assert
        dto!.StatusEnteredOn.Should().Be(Created);
    }

    [Fact]
    public async Task ReleasePackage_ShouldReportWhenItEnteredItsStatus()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var product = await SeedProduct(context);
        var package = ReleasePackage.Create(
            $"P-{Guid.CreateVersion7():N}"[..20],
            null,
            null,
            [(product.Id, null, "1.0", ManifestEntryKind.Changed)],
            (await Statuses(context, ProductWorkflowOwners.ReleasePackage))[0],
            EventActor.System,
            Created).Value;
        context.ReleasePackages.Add(package);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var reader = _fixture.CreateContext();
        var sut = new GetReleasePackageQueryHandler(reader, reader);

        // Act
        var dto = await sut.Handle(
            new GetReleasePackageQuery(new IdOrKey(package.Id.ToString())), TestContext.Current.CancellationToken);

        // Assert
        dto!.StatusEnteredOn.Should().Be(Created);
    }

    [Fact]
    public async Task Deployment_ShouldReportWhenItEnteredItsStatus()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var product = await SeedProduct(context);
        var version = Version.Create(
            product.Id,
            "1.0",
            null,
            null,
            null,
            isProductReleasable: true,
            (await Statuses(context, ProductWorkflowOwners.Version))[0],
            product.Name,
            EventActor.System,
            Created).Value;
        var environment = DeploymentEnvironment.Create(
            $"env-{Guid.CreateVersion7():N}"[..16], EnvironmentCategory.Testing, 1, EventActor.System, Created);
        context.Versions.Add(version);
        context.DeploymentEnvironments.Add(environment);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var deployment = Deployment.Create(
            version.Id,
            null,
            environment.Id,
            environment.Category,
            null,
            Created,
            (await Statuses(context, ProductWorkflowOwners.Deployment))[0],
            environment.Name,
            EventActor.System,
            Created).Value;
        context.Deployments.Add(deployment);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var reader = _fixture.CreateContext();
        var sut = new GetDeploymentQueryHandler(reader, reader);

        // Act
        var dto = await sut.Handle(
            new GetDeploymentQuery(new IdOrKey(deployment.Id.ToString())), TestContext.Current.CancellationToken);

        // Assert
        dto!.StatusEnteredOn.Should().Be(Created);
    }
}
