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
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Web.Api.IntegrationTests.Infrastructure;
using Wayd.Work.Application.Persistence;
using Wolverine;
using Wolverine.Persistence.Durability;
using Wolverine.Persistence.Durability.DeadLetterManagement;
using Wolverine.Runtime;
using Wolverine.Runtime.Routing;

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

        // No audit-attribution assertion here on purpose: replication projections (WorkProject et al.)
        // deliberately do not implement ISystemAuditable, so a durable delivery writes no audit columns.
        // System-actor attribution for auditable writes from an HTTP-less scope is pinned by
        // HangfireIdentityPropagationTests.Dispatch_FromScopeWithoutUser_StampsSystemActorOnAuditColumns,
        // which exercises the same CurrentUser/BaseDbContext path a durable handler scope uses.
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

    [Fact]
    public void DurableEventChains_RouteToDurableLocalQueues()
    {
        // Arrange — the outbox only persists envelopes destined for DURABLE endpoints. Without
        // UseDurableLocalQueues (WolverineConfiguration) the per-message-type local queues default to
        // BufferedInMemory and the "durable" events are never written to the envelope store at all — a
        // crash between commit and dispatch silently loses the event. This pins the queue mode so that
        // regression can't return silently (it is invisible to delivery-based tests: in-memory delivery
        // also "arrives eventually").
        _ = _factory.CreateClient();
        var runtime = (WolverineRuntime)_factory.Services.GetRequiredService<IWolverineRuntime>();

        Type[] durableEventTypes =
        [
            typeof(ProjectCreatedEvent),
            typeof(IterationCreatedEvent),
            typeof(StrategicThemeCreatedEvent),
            typeof(IntegrationStateChangedEvent<Guid>),
            typeof(TeamCreatedEvent),
        ];

        foreach (var durableEventType in durableEventTypes)
        {
            // Act — resolve the routes exactly the way a publish does.
            var routes = runtime.RoutingFor(durableEventType).Routes;

            // Assert
            Assert.NotEmpty(routes);
            foreach (var route in routes)
            {
                var messageRoute = Assert.IsType<MessageRoute>(route, exactMatch: false);
                Assert.True(
                    messageRoute.Sender.IsDurable,
                    $"{durableEventType.Name} routes to non-durable endpoint {messageRoute.Sender.Destination} — its envelopes would never be persisted");
            }
        }
    }

    [Fact]
    public async Task DurableEvent_WhoseHandlerFailsPersistently_LandsInDeadLetterStore()
    {
        // Arrange — a TeamCreatedEvent whose Name exceeds the PlanningTeam projection's 128-char column,
        // so the replication handler's SaveChanges fails deterministically on every attempt (a
        // non-transient failure). Per DurableEventFailurePolicy the chain retries with cooldowns
        // (1s/5s/15s) and then dead-letters — this proves a real handler failure ends up in the durable
        // dead letter store, where the messaging dashboard (and replay) can see it.
        _ = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var poisonedId = Guid.NewGuid();
        var poisonedEvent = new TeamCreatedEvent(
            id: poisonedId,
            key: 999999,
            code: new TeamCode("DLQTEST"),
            name: new string('x', 200),
            description: "Poisoned event for the failure-to-dead-letter pipeline test.",
            type: TeamType.Team,
            activeDate: new LocalDate(2026, 1, 1),
            inactiveDate: null,
            isActive: true,
            timestamp: Now);

        using (var scope = _factory.Services.CreateScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

            // Act — publish through Wolverine so the envelope takes the real durable local queue path.
            await bus.PublishAsync(poisonedEvent);
        }

        // Assert — the envelope lands in the dead letter store after the retry schedule (~21s of
        // cooldowns), so poll generously. Query by the poisoned envelope's message type and match on Id.
        var store = _factory.Services.GetRequiredService<IMessageStore>();
        DeadLetterEnvelope? deadLetter = null;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(90);
        while (DateTime.UtcNow < deadline)
        {
            var results = await store.DeadLetters.QueryAsync(
                new DeadLetterEnvelopeQuery { MessageType = typeof(TeamCreatedEvent).FullName },
                ct);
            // This test class owns its SQL container, so any TeamCreatedEvent dead letter is ours; the
            // serialized body containing the poisoned id double-checks that when the body is available.
            deadLetter = results.Envelopes.FirstOrDefault(e =>
                e.Envelope.Data is null
                || System.Text.Encoding.UTF8.GetString(e.Envelope.Data).Contains(poisonedId.ToString(), StringComparison.OrdinalIgnoreCase));
            if (deadLetter is not null)
            {
                break;
            }

            await Task.Delay(1000, ct);
        }

        Assert.True(deadLetter is not null, "Poisoned TeamCreatedEvent should have been moved to the durable dead letter store after exhausting retries");
        Assert.False(deadLetter!.Replayable);
        Assert.NotNull(deadLetter.ExceptionType);

        // Cleanup — discard so the poisoned envelope can't bleed into other assertions on this store.
        await store.DeadLetters.DiscardAsync(new DeadLetterEnvelopeQuery([deadLetter.Id]), ct);
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
