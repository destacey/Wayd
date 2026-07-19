using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.Events.StrategicManagement;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.ProjectPortfolioManagement.Application;
using Wayd.ProjectPortfolioManagement.Application.Projects.Commands;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.Web.Api.IntegrationTests.Infrastructure;
using Wayd.Work.Application.Persistence;
using Wolverine.Runtime;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Selective async event routing. The <c>Project*</c> → <c>WorkProject</c> replication family is routed
/// durably (see <c>DurableEventRoutes</c>): its handler is enlisted in the Wolverine EF Core outbox,
/// committed atomically with the entity change, and delivered on a background thread AFTER the originating
/// command returns — the opposite of the inline replication contract that <c>CrossDomainReplicationTests</c>
/// pins.
/// </summary>
[Trait("Category", "Docker")]
public sealed class DurableEventRoutingTests(WaydSqlServerApiFactory factory)
    : IClassFixture<WaydSqlServerApiFactory>
{
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public async Task CreateProject_ReplicatesToWorkProject_AsynchronouslyAfterCommandReturns()
    {
        // Arrange — seed the two prerequisites CreateProjectCommand needs (an active ExpenditureCategory and
        // a Portfolio), directly through the real DbContext. Neither raises a durable event.
        _ = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var (expenditureCategoryId, portfolioId) = await SeedProjectPrerequisites(ct);

        var key = $"P{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        Guid projectId;

        using (var dispatchScope = _factory.Services.CreateScope())
        {
            var dispatcher = dispatchScope.ServiceProvider.GetRequiredService<IDispatcher>();

            // Act — CreateProjectCommand raises ProjectCreatedEvent (durable). Its WorkProject projection is
            // NOT written inline; the envelope is committed with the Project and delivered in the background.
            var result = await dispatcher.Send(
                new CreateProjectCommand(
                    Name: "Durable Routing Project",
                    Description: "Exercises async Project → WorkProject replication.",
                    BusinessCase: null,
                    ExpectedBenefits: null,
                    Key: new ProjectKey(key),
                    ExpenditureCategoryId: expenditureCategoryId,
                    DateRange: null,
                    PortfolioId: portfolioId,
                    ProgramId: null,
                    ProjectLifecycleId: null,
                    SponsorIds: null,
                    OwnerIds: null,
                    ManagerIds: null,
                    MemberIds: null,
                    StrategicThemeIds: null),
                ct);

            Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);
            projectId = result.Value.Id;
        }

        // Assert — the projection arrives eventually (background delivery), not synchronously. We only assert
        // eventual arrival: asserting its ABSENCE right after the command returns would be an inherent race
        // (the background agent can be quick), so the meaningful, non-flaky guarantee is that it does land.
        var replicated = await WaitForWorkProject(projectId, TimeSpan.FromSeconds(30), ct);
        Assert.True(replicated, "WorkProject projection should be delivered asynchronously after CreateProject returns");
    }

    [Fact]
    public void DurableEventChains_HaveRetryThenDeadLetterPolicy_OtherChainsDoNot()
    {
        // Arrange — full host start so Wolverine has applied every IHandlerPolicy, including
        // DurableEventFailurePolicy, to the discovered handler chains. The HandlerGraph is on the concrete
        // WolverineRuntime (not the IWolverineRuntime interface), so resolve and cast.
        _ = _factory.CreateClient();
        var runtime = (WolverineRuntime)_factory.Services.GetRequiredService<IWolverineRuntime>();

        // A representative event type from every durable family (see DurableEventRoutes). Each of these
        // chains must carry the scoped failure policy.
        Type[] durableEventTypes =
        [
            typeof(ProjectCreatedEvent),
            typeof(IterationCreatedEvent),
            typeof(StrategicThemeCreatedEvent),
            typeof(IntegrationStateChangedEvent<Guid>),
            typeof(TeamCreatedEvent),
        ];

        // Act / Assert — every durable chain carries the failure policy: three retry-with-cooldown slots
        // (1s / 5s / 15s) plus a terminal dead-letter slot = four.
        foreach (var durableEventType in durableEventTypes)
        {
            var chain = runtime.Handlers.ChainFor(durableEventType);
            Assert.True(chain is not null, $"No handler chain found for durable event {durableEventType.Name}");
            var rule = Assert.Single(chain!.Failures);
            Assert.Equal(4, rule.Count()); // 3 cooldown attempts + MoveToErrorQueue
        }

        // A non-durable chain (a regular command handler) keeps Wolverine's default — no explicit failure
        // rules — proving the policy is scoped to the durable event types, not applied globally.
        var controlChain = runtime.Handlers.ChainFor(typeof(CreateTeamCommand));
        Assert.NotNull(controlChain);
        Assert.Empty(controlChain!.Failures);
    }

    private async Task<(int ExpenditureCategoryId, Guid PortfolioId)> SeedProjectPrerequisites(CancellationToken ct)
    {
        using var scope = _factory.Services.CreateScope();
        var ppm = scope.ServiceProvider.GetRequiredService<IProjectPortfolioManagementDbContext>();

        var expenditureCategory = ExpenditureCategory.Create(
            name: $"Cat {Guid.NewGuid():N}"[..12],
            description: "Durable routing test category",
            isCapitalizable: false,
            requiresDepreciation: false);
        var activate = expenditureCategory.Activate();
        Assert.True(activate.IsSuccess, activate.IsFailure ? activate.Error : null);

        var portfolio = ProjectPortfolio.Create(
            name: $"Portfolio {Guid.NewGuid():N}"[..16],
            description: "Durable routing test portfolio");
        // A project can only be created in an active (or on-hold) portfolio.
        var activatePortfolio = portfolio.Activate(new LocalDate(2026, 1, 1));
        Assert.True(activatePortfolio.IsSuccess, activatePortfolio.IsFailure ? activatePortfolio.Error : null);

        ppm.ExpenditureCategories.Add(expenditureCategory);
        ppm.Portfolios.Add(portfolio);
        await ppm.SaveChangesAsync(ct);

        return (expenditureCategory.Id, portfolio.Id);
    }

    private async Task<bool> WaitForWorkProject(Guid projectId, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            using var scope = _factory.Services.CreateScope();
            var work = scope.ServiceProvider.GetRequiredService<IWorkDbContext>();
            if (await work.WorkProjects.AsNoTracking().AnyAsync(p => p.Id == projectId, ct))
            {
                return true;
            }

            await Task.Delay(250, ct);
        }

        return false;
    }
}
