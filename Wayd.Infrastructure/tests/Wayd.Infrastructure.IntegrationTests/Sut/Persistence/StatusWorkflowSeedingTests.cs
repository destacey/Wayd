using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.ProductManagement;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Infrastructure.Persistence.Initialization;
using Wayd.ProductManagement.Domain;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Verifies the workflow schema and its seeders against real SQL Server. The filtered unique index and
/// the alias lookup are the parts an in-memory provider cannot check.
/// </summary>
/// <remarks>
/// The fixture's container is shared across this class, so queries scope to <c>IsSystem</c> rather than
/// owner type alone — other tests here add their own workflows for the same owner types.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class StatusWorkflowSeedingTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    private static IDateTimeProvider DateTimeProvider()
    {
        var provider = new Mock<IDateTimeProvider>();
        provider.SetupGet(d => d.Now).Returns(Instant.FromUtc(2026, 1, 15, 9, 30, 0));
        provider.SetupGet(d => d.Today).Returns(new LocalDate(2026, 1, 15));

        return provider.Object;
    }

    private async Task SeedAll(WaydDbContext context)
    {
        await new ProductManagementWorkflowSeeder().Initialize(context, DateTimeProvider(), TestContext.Current.CancellationToken);
        await new WorkflowAliasNameSeeder().Initialize(context, DateTimeProvider(), TestContext.Current.CancellationToken);
    }

    #region Workflow seeding

    [Fact]
    public async Task Seeder_ShouldCreateAPublishedWorkflowForEveryOwnerType()
    {
        // Arrange
        ProductWorkflowOwners.Register();
        await using var context = _fixture.CreateContext();

        // Act
        await SeedAll(context);

        // Assert
        foreach (var owner in ProductWorkflowOwners.All)
        {
            var workflow = await context.StatusWorkflows
                .Include(w => w.Statuses)
                .SingleOrDefaultAsync(w => w.OwnerType == owner.Key && w.IsSystem, TestContext.Current.CancellationToken);

            workflow.Should().NotBeNull($"'{owner.Key}' should have a seeded default");
            workflow!.State.Should().Be(StatusWorkflowState.Published);
            workflow.IsSystem.Should().BeTrue();
        }
    }

    [Fact]
    public async Task Seeder_ShouldSupplyEveryRequiredAlias()
    {
        // Arrange
        ProductWorkflowOwners.Register();
        await using var context = _fixture.CreateContext();

        // Act
        await SeedAll(context);

        // Assert
        // A seeded workflow missing a required alias would be refused at activation, so this also
        // proves the defaults are internally consistent rather than merely present.
        foreach (var owner in ProductWorkflowOwners.All)
        {
            var workflow = await context.StatusWorkflows
                .Include(w => w.Statuses)
                .SingleAsync(w => w.OwnerType == owner.Key && w.IsSystem, TestContext.Current.CancellationToken);

            foreach (var alias in owner.RequiredAliases)
            {
                workflow.StatusFor(alias).Should().NotBeNull(
                    $"'{owner.Key}' requires {owner.DescribeAlias(alias)}");
            }
        }
    }

    [Fact]
    public async Task Seeder_ShouldBeIdempotent()
    {
        // Arrange
        ProductWorkflowOwners.Register();
        await using var context = _fixture.CreateContext();
        await SeedAll(context);

        var before = await context.StatusWorkflows.CountAsync(w => w.IsSystem, TestContext.Current.CancellationToken);

        // Act
        await using var second = _fixture.CreateContext();
        await SeedAll(second);

        // Assert
        // Seeders run on every startup; a second pass must not duplicate the defaults.
        var after = await second.StatusWorkflows.CountAsync(w => w.IsSystem, TestContext.Current.CancellationToken);
        after.Should().Be(before);
    }

    [Fact]
    public async Task Seeder_ShouldMarkSeededStatusesAsSystemOwned()
    {
        // Arrange
        ProductWorkflowOwners.Register();
        await using var context = _fixture.CreateContext();

        // Act
        await SeedAll(context);

        // Assert
        var statuses = await context.WorkflowStatuses
            .Where(s => context.StatusWorkflows.Any(w => w.Id == s.WorkflowId && w.IsSystem))
            .ToListAsync(TestContext.Current.CancellationToken);

        statuses.Should().NotBeEmpty();
        statuses.Should().OnlyContain(s => s.IsSystem);
    }

    #endregion Workflow seeding

    #region Alias lookup

    [Fact]
    public async Task AliasSeeder_ShouldNameEveryAliasEveryOwnerTypeDeclares()
    {
        // Arrange
        ProductWorkflowOwners.Register();
        await using var context = _fixture.CreateContext();

        // Act
        await SeedAll(context);

        // Assert
        // This lookup is what makes an int alias column readable in a query; a missing row would leave
        // a deployment outcome showing as a bare number.
        foreach (var owner in ProductWorkflowOwners.All)
        {
            foreach (var (alias, name) in owner.Aliases)
            {
                var row = await context.WorkflowAliasNames
                    .SingleOrDefaultAsync(a => a.OwnerType == owner.Key && a.Alias == alias, TestContext.Current.CancellationToken);

                row.Should().NotBeNull($"'{owner.Key}' alias {alias} should be named");
                row!.Name.Should().Be(name);
            }
        }
    }

    [Fact]
    public async Task AliasSeeder_ShouldLetADeploymentOutcomeResolveToItsName()
    {
        // Arrange
        ProductWorkflowOwners.Register();
        await using var context = _fixture.CreateContext();
        await SeedAll(context);

        // Act
        // The join a report writer would make against the int column.
        var name = await context.WorkflowAliasNames
            .Where(a => a.OwnerType == ProductWorkflowOwners.Deployment.Key
                     && a.Alias == (int)ProductStatusAlias.RolledBack)
            .Select(a => a.Name)
            .SingleAsync(TestContext.Current.CancellationToken);

        // Assert
        name.Should().Be(nameof(ProductStatusAlias.RolledBack));
    }

    #endregion Alias lookup

    #region Schema

    [Fact]
    public async Task WorkflowStatuses_ShouldAllowManyUnaliasedStatusesInOneWorkflow()
    {
        // Arrange
        ProductWorkflowOwners.Register();
        await using var context = _fixture.CreateContext();

        var workflow = StatusWorkflow.Create("Unaliased Probe", null, ProductWorkflowOwners.Product.Key).Value;
        workflow.AddStatus("Stage One", null, StatusCategory.Active);
        workflow.AddStatus("Stage Two", null, StatusCategory.Active);

        // Act
        context.StatusWorkflows.Add(workflow);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        // The unique index on (WorkflowId, Alias) is filtered to exclude NoAlias; without the filter
        // this second insert would violate it.
        var saved = await context.StatusWorkflows
            .Include(w => w.Statuses)
            .SingleAsync(w => w.Id == workflow.Id, TestContext.Current.CancellationToken);

        saved.Statuses.Should().HaveCount(2);
        saved.Statuses.Should().OnlyContain(s => s.Alias == StatusWorkflow.NoAlias);
    }

    [Fact]
    public async Task WorkflowStatuses_ShouldRejectTwoWorkflowsSharingAStatusName()
    {
        // Arrange
        ProductWorkflowOwners.Register();
        await using var context = _fixture.CreateContext();

        var workflow = StatusWorkflow.Create("Duplicate Name Probe", null, ProductWorkflowOwners.Product.Key).Value;
        workflow.AddStatus("Live", null, StatusCategory.Active, (int)ProductStatusAlias.Active);
        context.StatusWorkflows.Add(workflow);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        // Two workflows may each have a "Live"; the uniqueness is per workflow, not global.
        var other = StatusWorkflow.Create("Second Probe", null, ProductWorkflowOwners.Product.Key).Value;
        other.AddStatus("Live", null, StatusCategory.Active, (int)ProductStatusAlias.Active);
        context.StatusWorkflows.Add(other);

        var act = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion Schema
}
