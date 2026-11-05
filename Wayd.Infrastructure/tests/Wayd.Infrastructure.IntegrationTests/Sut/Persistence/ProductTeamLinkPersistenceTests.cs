using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Infrastructure.Persistence.Initialization;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Products.Queries;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Product team links through the real handlers and SQL Server.
/// </summary>
/// <remarks>
/// Each command runs in a context that has never seen the product, which is the only way a missing
/// <c>.Include(p =&gt; p.TeamLinks)</c> shows: the handler fakes hold a product's links in memory whatever the
/// query asked for. The reads project through the link's team navigation, which the fakes set by hand.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class ProductTeamLinkPersistenceTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    private static readonly Instant Now = Instant.FromUtc(2026, 6, 1, 12, 0, 0);
    private static readonly LocalDate Today = new(2026, 6, 1);

    private sealed record TeamSource(Guid Id, int Key, string Name, TeamCode Code, TeamType Type, bool IsActive) : ISimpleTeam;

    [Fact]
    public async Task Link_WhenTheTeamIsAlreadyLinked_SucceedsWithoutASecondLink()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var product = await SeedProduct($"Storefront Web {Guid.CreateVersion7()}");
        var team = await SeedTeam();
        await Link(product.Id, team.Id);

        // Act — a fresh context, so the aggregate knows of the first link only if the handler loads it
        Result second;
        await using (var context = _fixture.CreateContext())
        {
            second = await LinkHandler(context).Handle(new LinkProductTeamsCommand(product.Id, [team.Id]), ct);
        }

        // Assert — no change, not the unique index turning the save into a generic error
        second.IsSuccess.Should().BeTrue(second.IsFailure ? second.Error : null);

        await using var verify = _fixture.CreateContext();
        (await verify.ProductTeamLinks.CountAsync(l => l.ProductId == product.Id, ct)).Should().Be(1);
    }

    [Fact]
    public async Task TwoLinksForTheSameTeam_SavedFromRacingContexts_AreRejectedByTheDatabase()
    {
        // Arrange — each context checks the aggregate before the other has saved
        var ct = TestContext.Current.CancellationToken;
        var product = await SeedProduct($"Storefront Web {Guid.CreateVersion7()}");
        var team = await SeedTeam();

        await using var first = _fixture.CreateContext();
        await using var second = _fixture.CreateContext();

        var firstProduct = await first.Products.Include(p => p.TeamLinks).SingleAsync(p => p.Id == product.Id, ct);
        var secondProduct = await second.Products.Include(p => p.TeamLinks).SingleAsync(p => p.Id == product.Id, ct);
        var firstTeam = await first.ProductTeams.SingleAsync(t => t.Id == team.Id, ct);
        var secondTeam = await second.ProductTeams.SingleAsync(t => t.Id == team.Id, ct);

        firstProduct.LinkTeam(firstTeam, EventActor.System, Now).IsSuccess.Should().BeTrue();
        secondProduct.LinkTeam(secondTeam, EventActor.System, Now).IsSuccess.Should().BeTrue();

        await first.SaveChangesAsync(ct);

        // Act
        var act = () => second.SaveChangesAsync(ct);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Link_WhenAnotherRequestLinksTheSameTeamFirst_Succeeds()
    {
        // Arrange — the clock is read after the product is loaded and before the save, so another request's
        // link lands in between: the handler's aggregate check passes and its save hits the unique index
        var ct = TestContext.Current.CancellationToken;
        var product = await SeedProduct($"Storefront Web {Guid.CreateVersion7()}");
        var team = await SeedTeam();

        var otherRequestRan = false;
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(d => d.Today).Returns(Today);
        clock.SetupGet(d => d.Now).Returns(() =>
        {
            if (!otherRequestRan)
            {
                otherRequestRan = true;
                Link(product.Id, team.Id).GetAwaiter().GetResult();
            }

            return Now;
        });

        // Act
        Result result;
        await using (var context = _fixture.CreateContext())
        {
            result = await new LinkProductTeamsCommandHandler(
                    context, CurrentUser(), Mock.Of<IDispatcher>(), Mock.Of<ILogger<LinkProductTeamsCommandHandler>>(), clock.Object)
                .Handle(new LinkProductTeamsCommand(product.Id, [team.Id]), ct);
        }

        // Assert — a repeat of a link is a no-op success, however close together the two arrive
        otherRequestRan.Should().BeTrue();
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await using var verify = _fixture.CreateContext();
        (await verify.ProductTeamLinks.CountAsync(l => l.ProductId == product.Id, ct)).Should().Be(1);
    }

    [Fact]
    public async Task DeletingATeamCopyThatIsLinked_IsRejectedByTheDatabase()
    {
        // Arrange — teams are deactivated, never deleted, so a delete reaching a linked copy is a bug that must
        // not take the links with it
        var ct = TestContext.Current.CancellationToken;
        var product = await SeedProduct($"Storefront Web {Guid.CreateVersion7()}");
        var team = await SeedTeam();
        await Link(product.Id, team.Id);

        await using var context = _fixture.CreateContext();
        context.ProductTeams.Remove(await context.ProductTeams.SingleAsync(t => t.Id == team.Id, ct));

        // Act
        var act = () => context.SaveChangesAsync(ct);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        await using var verify = _fixture.CreateContext();
        (await verify.ProductTeamLinks.CountAsync(l => l.TeamId == team.Id, ct)).Should().Be(1);
    }

    [Fact]
    public async Task DeletingAProduct_DeletesItsLinks()
    {
        // Arrange — loaded without its links, so only the database's cascade can take them
        var ct = TestContext.Current.CancellationToken;
        var product = await SeedProduct($"Storefront Web {Guid.CreateVersion7()}");
        var team = await SeedTeam();
        await Link(product.Id, team.Id);

        await using (var context = _fixture.CreateContext())
        {
            context.Products.Remove(await context.Products.SingleAsync(p => p.Id == product.Id, ct));

            // Act
            await context.SaveChangesAsync(ct);
        }

        // Assert
        await using var verify = _fixture.CreateContext();
        (await verify.ProductTeamLinks.AnyAsync(l => l.ProductId == product.Id, ct)).Should().BeFalse();
        (await verify.ProductTeams.AnyAsync(t => t.Id == team.Id, ct)).Should().BeTrue();
    }

    [Fact]
    public async Task Link_WhenAnotherWriterCopiesTheTeamFirst_UsesThatCopyAndLinks()
    {
        // Arrange — the TeamCreated handler saves the copy while the link is reading Organization
        var ct = TestContext.Current.CancellationToken;
        var product = await SeedProduct($"Storefront Web {Guid.CreateVersion7()}");
        var code = new TeamCode(Convert.ToHexString(Guid.NewGuid().ToByteArray())[..10]);
        var source = new TeamSource(Guid.CreateVersion7(), Random.Shared.Next(1_000_000, int.MaxValue), $"Team {code.Value}", code, TeamType.Team, true);

        var dispatcher = new Mock<IDispatcher>();
        dispatcher
            .Setup(d => d.Send(It.Is<GetSimpleTeamQuery>(q => q.Id == source.Id), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await using var other = _fixture.CreateContext();
                other.ProductTeams.Add(new ProductTeam(source, Now));
                await other.SaveChangesAsync(ct);
                return (ISimpleTeam?)source;
            });

        // Act
        Result result;
        await using (var context = _fixture.CreateContext())
        {
            result = await new LinkProductTeamsCommandHandler(
                    context, CurrentUser(), dispatcher.Object, Mock.Of<ILogger<LinkProductTeamsCommandHandler>>(), Clock())
                .Handle(new LinkProductTeamsCommand(product.Id, [source.Id]), ct);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await using var verify = _fixture.CreateContext();
        (await verify.ProductTeams.CountAsync(t => t.Id == source.Id, ct)).Should().Be(1);
        (await verify.ProductTeamLinks.SingleAsync(l => l.ProductId == product.Id, ct)).TeamId.Should().Be(source.Id);
    }

    [Fact]
    public async Task Unlink_DeletesTheSavedLink()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var product = await SeedProduct($"Storefront Web {Guid.CreateVersion7()}");
        var team = await SeedTeam();
        var other = await SeedTeam();
        await Link(product.Id, team.Id);
        await Link(product.Id, other.Id);

        // Act
        Result result;
        await using (var context = _fixture.CreateContext())
        {
            result = await new UnlinkProductTeamCommandHandler(
                    context, CurrentUser(), Mock.Of<ILogger<UnlinkProductTeamCommandHandler>>(), Clock())
                .Handle(new UnlinkProductTeamCommand(product.Id, team.Id), ct);
        }

        // Assert
        result.IsSuccess.Should().BeTrue();
        await using var verify = _fixture.CreateContext();
        (await verify.ProductTeamLinks.Where(l => l.ProductId == product.Id).Select(l => l.TeamId).ToListAsync(ct))
            .Should().Equal(other.Id);
    }

    [Fact]
    public async Task SetProductTeams_LinksTheNewTeamsAndUnlinksTheOmittedOnes()
    {
        // Arrange — a fresh context for the handler, so the product's links come only from its .Include
        var ct = TestContext.Current.CancellationToken;
        var product = await SeedProduct($"Storefront Web {Guid.CreateVersion7()}");
        var kept = await SeedTeam();
        var dropped = await SeedTeam();
        var added = await SeedTeam();
        await Link(product.Id, kept.Id);
        await Link(product.Id, dropped.Id);

        // Act
        Result result;
        await using (var context = _fixture.CreateContext())
        {
            result = await new SetProductTeamsCommandHandler(
                    context, CurrentUser(), Mock.Of<IDispatcher>(), Mock.Of<ILogger<SetProductTeamsCommandHandler>>(), Clock())
                .Handle(new SetProductTeamsCommand(product.Id, [kept.Id, added.Id]), ct);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await using var verify = _fixture.CreateContext();
        (await verify.ProductTeamLinks.Where(l => l.ProductId == product.Id).Select(l => l.TeamId).ToListAsync(ct))
            .Should().BeEquivalentTo([kept.Id, added.Id]);
    }

    [Fact]
    public async Task SetTeamProducts_LinksAndUnlinksAcrossProducts_InOneSave()
    {
        // Arrange — a fresh context for the handler, so it finds the team's current products only by querying
        var ct = TestContext.Current.CancellationToken;
        var kept = await SeedProduct($"Checkout {Guid.CreateVersion7()}");
        var dropped = await SeedProduct($"Legacy Cart {Guid.CreateVersion7()}");
        var added = await SeedProduct($"Storefront Web {Guid.CreateVersion7()}");
        var team = await SeedTeam();
        var other = await SeedTeam();
        await Link(kept.Id, team.Id);
        await Link(dropped.Id, team.Id);
        await Link(dropped.Id, other.Id);

        // Act
        Result result;
        await using (var context = _fixture.CreateContext())
        {
            result = await new SetTeamProductsCommandHandler(
                    context, CurrentUser(), Mock.Of<IDispatcher>(), Mock.Of<ILogger<SetTeamProductsCommandHandler>>(), Clock())
                .Handle(new SetTeamProductsCommand(team.Id, [kept.Id, added.Id]), ct);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        await using var verify = _fixture.CreateContext();
        (await verify.ProductTeamLinks.Where(l => l.TeamId == team.Id).Select(l => l.ProductId).ToListAsync(ct))
            .Should().BeEquivalentTo([kept.Id, added.Id]);
        (await verify.ProductTeamLinks.Where(l => l.ProductId == dropped.Id).Select(l => l.TeamId).ToListAsync(ct))
            .Should().Equal(other.Id);
    }

    [Fact]
    public async Task Queries_ReadTheTeamAndRollUpADescendantsLinks()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var storefront = await SeedProduct($"Storefront {Guid.CreateVersion7()}");
        var web = await SeedProduct($"Storefront Web {Guid.CreateVersion7()}", storefront.Id);
        var team = await SeedTeam(TeamType.TeamOfTeams);
        await Link(web.Id, team.Id);

        // Act
        await using var context = _fixture.CreateContext();
        var onStorefront = await new GetProductTeamsQueryHandler(context)
            .Handle(new GetProductTeamsQuery(new IdOrKey(storefront.Key)), ct);
        var onTeam = await new GetTeamProductsQueryHandler(context)
            .Handle(new GetTeamProductsQuery(team.Id), ct);

        // Assert
        onStorefront.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Product = new { web.Id, web.Name },
            Team = new { team.Id, team.Name, Code = team.Code.Value, Type = TeamType.TeamOfTeams, IsActive = true },
        });
        onTeam.Should().ContainSingle().Which.ProductPath.Should().ContainSingle().Which.Id.Should().Be(storefront.Id);
    }

    [Fact]
    public async Task ProductList_CarriesEachProductsOwnTeamsByName()
    {
        // Arrange — the list projects the teams in SQL, through the link's team navigation
        var ct = TestContext.Current.CancellationToken;
        var storefront = await SeedProduct($"Storefront {Guid.CreateVersion7()}");
        var web = await SeedProduct($"Storefront Web {Guid.CreateVersion7()}", storefront.Id);
        var zeta = await SeedTeam(name: "Zeta");
        var alpha = await SeedTeam(TeamType.TeamOfTeams, name: "Alpha");
        await Link(web.Id, zeta.Id);
        await Link(web.Id, alpha.Id);

        // Act
        await using var context = _fixture.CreateContext();
        var products = await new GetProductsQueryHandler(context)
            .Handle(new GetProductsQuery(ParentId: storefront.Id), ct);
        var parent = await new GetProductsQueryHandler(context)
            .Handle(new GetProductsQuery(), ct);

        // Assert
        var row = products.Should().ContainSingle().Subject;
        row.Teams.Select(t => t.Id).Should().Equal(alpha.Id, zeta.Id);
        row.Teams.First().Should().BeEquivalentTo(new
        {
            alpha.Id, alpha.Key, alpha.Name, Code = alpha.Code.Value, Type = TeamType.TeamOfTeams, IsActive = true,
        });
        parent.Single(p => p.Id == storefront.Id).Teams.Should().BeEmpty();
    }

    private async Task Link(Guid productId, Guid teamId)
    {
        await using var context = _fixture.CreateContext();

        var result = await LinkHandler(context).Handle(
            new LinkProductTeamsCommand(productId, [teamId]), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
    }

    private static LinkProductTeamsCommandHandler LinkHandler(WaydDbContext context) =>
        new(context, CurrentUser(), Mock.Of<IDispatcher>(), Mock.Of<ILogger<LinkProductTeamsCommandHandler>>(), Clock());

    private static ICurrentUser CurrentUser()
    {
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.GetUserId()).Returns(Guid.CreateVersion7().ToString());
        return user.Object;
    }

    private static IDateTimeProvider Clock()
    {
        var provider = new Mock<IDateTimeProvider>();
        provider.SetupGet(d => d.Now).Returns(Now);
        provider.SetupGet(d => d.Today).Returns(Today);
        return provider.Object;
    }

    private async Task<ProductTeam> SeedTeam(TeamType type = TeamType.Team, string? name = null)
    {
        await using var context = _fixture.CreateContext();

        var id = Guid.CreateVersion7();
        var code = new TeamCode(Convert.ToHexString(Guid.NewGuid().ToByteArray())[..10]);
        var team = new ProductTeam(new TeamSource(id, Random.Shared.Next(1_000_000, int.MaxValue), name ?? $"Team {code.Value}", code, type, true), Now);

        context.ProductTeams.Add(team);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return team;
    }

    private async Task<Product> SeedProduct(string name, Guid? parentId = null)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();

        ProductWorkflowOwners.Register();

        var workflow = await context.StatusWorkflows
            .Include(w => w.Statuses)
            .FirstOrDefaultAsync(w => w.OwnerType == ProductWorkflowOwners.Product.Key && w.IsSystem, ct);

        if (workflow is null)
        {
            await new ProductManagementWorkflowSeeder().Initialize(context, Clock(), ct);

            workflow = await context.StatusWorkflows
                .Include(w => w.Statuses)
                .FirstAsync(w => w.OwnerType == ProductWorkflowOwners.Product.Key && w.IsSystem, ct);
        }

        var productTypeId = await context.ProductTypes.Select(t => t.Id).FirstOrDefaultAsync(ct);
        if (productTypeId == Guid.Empty)
        {
            await new ProductTypeSeeder().Initialize(context, Clock(), ct);
            productTypeId = await context.ProductTypes.Select(t => t.Id).FirstAsync(ct);
        }

        var product = Product.Create(
            name, null, productTypeId, parentId, null,
            StatusRef.From(workflow.Statuses.OrderBy(s => s.Order).First()),
            EventActor.System, Now.Minus(Duration.FromDays(365)));

        context.Products.Add(product);
        await context.SaveChangesAsync(ct);

        return product;
    }
}
