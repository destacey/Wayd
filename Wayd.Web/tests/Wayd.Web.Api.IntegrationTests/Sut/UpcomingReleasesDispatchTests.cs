using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.ProductManagement.Application;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.ReleasePackages.Commands;
using Wayd.ProductManagement.Application.Releases.Commands;
using Wayd.ProductManagement.Application.Releases.Queries;
using Wayd.ProductManagement.Application.Versions.Commands;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Runs the upcoming releases query against SQL Server.
/// </summary>
/// <remarks>
/// The query decides in SQL which releases reach a team's products — announced under one or a line above it,
/// or carrying one directly or as a changed package line — through correlated subqueries over the release's
/// contents. An in-memory fake runs those as LINQ to Objects, so a shape EF cannot translate throws only here.
/// </remarks>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class UpcomingReleasesDispatchTests(WaydSqlServerApiFactory factory)
{
    private static readonly LocalDate Today = new(2026, 4, 1);

    private readonly WaydSqlServerApiFactory _factory = factory;

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..24];

    private static async Task<Guid> Product(IDispatcher dispatcher, Guid productTypeId, Guid? parentId)
    {
        var product = await dispatcher.Send(
            new CreateProductCommand(Unique("Node"), null, productTypeId, parentId, null),
            TestContext.Current.CancellationToken);
        Assert.True(product.IsSuccess, product.IsFailure ? product.Error : null);
        return product.Value.Id;
    }

    private static async Task<Guid> Release(IDispatcher dispatcher, Guid? productId, LocalDate target)
    {
        var release = await dispatcher.Send(
            new PlanReleaseCommand(productId, Unique("Rel"), null, target, null),
            TestContext.Current.CancellationToken);
        Assert.True(release.IsSuccess, release.IsFailure ? release.Error : null);
        return release.Value.Id;
    }

    private static async Task<Guid> Package(IDispatcher dispatcher, Guid productId, ManifestEntryKind kind)
    {
        var package = await dispatcher.Send(
            new AssembleReleasePackageCommand(Unique("Pkg"), null, null,
            [
                new ManifestEntry(productId, null, "1.0", kind),
            ]),
            TestContext.Current.CancellationToken);
        Assert.True(package.IsSuccess, package.IsFailure ? package.Error : null);
        return package.Value.Id;
    }

    private static async Task SetContents(
        IDispatcher dispatcher, Guid releaseId, IReadOnlyCollection<Guid> versionIds, IReadOnlyCollection<Guid> packageIds)
    {
        var contents = await dispatcher.Send(
            new SetReleaseContentsCommand(releaseId, versionIds, packageIds), TestContext.Current.CancellationToken);
        Assert.True(contents.IsSuccess, contents.IsFailure ? contents.Error : null);
    }

    [Fact]
    public async Task Dispatch_GetTeamsUpcomingReleasesQuery_ReachesReleasesThroughTheProductLineAndWhatTheyCarry()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>();

        var code = $"T{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var team = await dispatcher.Send(
            new CreateTeamCommand($"Upcoming Releases {code}", new TeamCode(code), null, new LocalDate(2026, 1, 15)),
            TestContext.Current.CancellationToken);
        Assert.True(team.IsSuccess, team.IsFailure ? team.Error : null);

        var productTypeId = await dbContext.ProductTypes
            .Where(t => t.IsActive && t.IsReleasable)
            .Select(t => t.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
        var line = await Product(dispatcher, productTypeId, null);
        var teamProduct = await Product(dispatcher, productTypeId, line);
        var otherProduct = await Product(dispatcher, productTypeId, null);

        var linked = await dispatcher.Send(
            new SetProductTeamsCommand(teamProduct, [team.Value.Id]), TestContext.Current.CancellationToken);
        Assert.True(linked.IsSuccess, linked.IsFailure ? linked.Error : null);

        var version = await dispatcher.Send(
            new PlanVersionCommand(teamProduct, "4.1.0", null, Today.PlusDays(5), null),
            TestContext.Current.CancellationToken);
        Assert.True(version.IsSuccess, version.IsFailure ? version.Error : null);
        var otherVersion = await dispatcher.Send(
            new PlanVersionCommand(otherProduct, "1.0.0", null, Today.PlusDays(5), null),
            TestContext.Current.CancellationToken);
        Assert.True(otherVersion.IsSuccess, otherVersion.IsFailure ? otherVersion.Error : null);

        var underLine = await Release(dispatcher, line, Today.PlusDays(-1));
        var carryingVersion = await Release(dispatcher, null, Today.PlusDays(2));
        await SetContents(dispatcher, carryingVersion, [version.Value.Id], []);
        var shippingInPackage = await Release(dispatcher, null, Today.PlusDays(3));
        await SetContents(dispatcher, shippingInPackage, [], [await Package(dispatcher, teamProduct, ManifestEntryKind.Changed)]);

        await Release(dispatcher, otherProduct, Today.PlusDays(1));
        var carryingOther = await Release(dispatcher, null, Today.PlusDays(1));
        await SetContents(dispatcher, carryingOther, [otherVersion.Value.Id], [await Package(dispatcher, otherProduct, ManifestEntryKind.Changed)]);
        await Release(dispatcher, line, Today.PlusDays(15));

        // Act
        var result = await dispatcher.Send(
            new GetTeamsUpcomingReleasesQuery([team.Value.Id], Today, 14, IncludeReleases: true, IncludeVersions: true),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([underLine, carryingVersion, shippingInPackage], result.Releases!.Select(r => r.Id));
        Assert.True(result.Releases![0].IsPastTarget);
        Assert.All(result.Releases!, r => Assert.Equal([teamProduct], r.TeamProducts.Select(p => p.Id)));
        Assert.Equal([version.Value.Id], result.Versions!.Select(v => v.Id));
    }
}
