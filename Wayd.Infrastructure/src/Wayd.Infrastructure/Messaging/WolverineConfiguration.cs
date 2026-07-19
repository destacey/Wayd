using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Model;
using JasperFx.Resources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Wayd.Common.Application.Behaviors;
using Wayd.Common.Application.Validation;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.FluentValidation;
using Wolverine.SqlServer;

namespace Wayd.Infrastructure.Messaging;

/// <summary>
/// Central Wolverine host configuration — the single replacement for the per-module <c>AddMediatR</c>
/// calls that Phase 1 left in place. Lives in Infrastructure alongside the other plumbing concerns
/// (persistence, auth, Hangfire, OpenTelemetry) so the host stays a thin orchestrator. Handler
/// discovery is restricted to an explicit allow-list of Application assemblies (plus Infrastructure,
/// which owns two OIDC command handlers); nothing is scanned from the wider dependency tree.
/// </summary>
public static class WolverineConfiguration
{
    /// <summary>
    /// Marker types used only to reach each handler-bearing assembly for Wolverine discovery. Kept as
    /// <c>typeof(...)</c> references (not string names) so a moved/renamed assembly is a compile error
    /// rather than a silent discovery gap.
    /// </summary>
    private static readonly Type[] HandlerAssemblyMarkers =
    [
        typeof(Wayd.Common.Application.ConfigureServices),
        typeof(Wayd.AppIntegration.Application.ConfigureServices),
        typeof(Wayd.Goals.Application.ConfigureServices),
        typeof(Wayd.Organization.Application.ConfigureServices),
        typeof(Wayd.Planning.Application.ConfigureServices),
        typeof(Wayd.ProjectPortfolioManagement.Application.ConfigureServices),
        typeof(Wayd.StrategicManagement.Application.ConfigureServices),
        typeof(Wayd.Work.Application.ConfigureServices),
        typeof(Wayd.Links.ConfigureServices),
        // Infrastructure hosts DeleteOidcProviderCommandHandler / TestOidcProviderDiscoveryCommandHandler.
        typeof(Wayd.Infrastructure.ConfigureServices),
    ];

    /// <summary>
    /// Registers Wolverine as the command/query/event mediator on the host builder. This is a
    /// host-builder call (not an <see cref="IServiceCollection"/> one) because handler discovery and
    /// code generation are host-level concerns; <c>AddInfrastructure</c> cannot own it. <c>IDispatcher</c>
    /// (the only dispatch seam call sites use) is registered separately in <c>AddCommonApplication</c>.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="applicationAssembly">
    /// The assembly that owns the pre-generated Wolverine handler tree (the API entry assembly,
    /// <c>Wayd.Web.Api</c>). Passed explicitly rather than inferred because <see cref="TypeLoadMode.Static"/>
    /// loads the generated <c>HandlerRegistry</c> from this exact assembly, and neither of Wolverine's
    /// inference strategies yields it: the caller of <c>UseWolverine</c> is <c>Wayd.Infrastructure</c> (this
    /// assembly), and <c>Assembly.GetEntryAssembly()</c> is the TEST RUNNER assembly under an integration-test
    /// host — both wrong, and both fail Static type loading. Callers pass <c>typeof(Program).Assembly</c>.
    /// </param>
    public static TBuilder AddWaydWolverine<TBuilder>(this TBuilder builder, System.Reflection.Assembly applicationAssembly)
        where TBuilder : IHostApplicationBuilder
    {
        // The durable outbox stores its envelopes in the same database as the application, read from the
        // same config key AddPersistence uses (DatabaseSettings:ConnectionString). This read is EAGER —
        // Wolverine's PersistMessagesWithSqlServer (below) needs the connection string synchronously here,
        // before the host is built. In a normal host that is fine: AddConfigurations() has already loaded
        // database.json (and env vars) into builder.Configuration. Integration-test hosts therefore inject
        // their container connection string via an environment variable rather than a deferred
        // ConfigureAppConfiguration override, which would not be visible yet at this point — see
        // WaydSqlServerApiFactory.
        var connectionString = builder.Configuration["DatabaseSettings:ConnectionString"]
            ?? throw new InvalidOperationException(
                "DatabaseSettings:ConnectionString is required to configure the Wolverine durable outbox.");

        builder.UseWolverine(opts => opts.ConfigureWayd(applicationAssembly, connectionString));
        return builder;
    }

    private static WolverineOptions ConfigureWayd(this WolverineOptions opts, System.Reflection.Assembly applicationAssembly, string connectionString)
    {
        // Pin the application assembly to Wayd.Web.Api (passed in from Program.cs as typeof(Program).Assembly).
        // This is the assembly the pre-generated handler tree (Internal/Generated/WolverineHandlers) is emitted
        // into and compiled as part of, so it is where TypeLoadMode.Static must look for the generated
        // HandlerRegistry and per-handler types. Neither of Wolverine's inference strategies yields it:
        //   - the caller of UseWolverine is THIS assembly (Wayd.Infrastructure), since AddWaydWolverine lives
        //     here — Static would then not find the registry and silently fall back to a runtime scan;
        //   - Assembly.GetEntryAssembly() is the TEST RUNNER assembly under an integration-test host
        //     (Wayd.Web.Api.IntegrationTests), which fails Static loading with ExpectedTypeMissingException at
        //     first dispatch (invisible to build + host-boot; only the dispatch tests catch it).
        // Passing it explicitly is exactly what Wolverine's docs prescribe for test-harness scenarios.
        opts.ApplicationAssembly = applicationAssembly;

        // Discovery: only the assemblies we own that actually contain handlers. Wolverine already scans
        // the entry assembly (Wayd.Web.Api); include the rest explicitly.
        foreach (var marker in HandlerAssemblyMarkers)
        {
            opts.Discovery.IncludeAssembly(marker.Assembly);
        }

        // DURABLE TRANSACTIONAL OUTBOX (plumbing only — no event is routed durably yet).
        //
        // PersistMessagesWithSqlServer stores message envelopes in a dedicated "wolverine" schema (never
        // dbo), provisioned by Weasel at startup parallel to our EF migrations. UseEntityFrameworkCoreTransactions
        // lets a handler's outgoing messages enlist in the WaydDbContext SaveChanges transaction so they
        // would commit atomically with the entity changes and deliver post-commit via the durability
        // agent. The DbContext half — AddDbContextWithWolverineIntegration<WaydDbContext> — is wired in
        // AddPersistence (it is an IServiceCollection call; these are WolverineOptions calls).
        //
        // Why this changes NO behaviour today: Wolverine's outbox is post-commit and asynchronous by
        // design — outbox messages are never delivered inline before the calling method returns (a
        // deliberate guard against a downstream handler running before the DB change is visible), and
        // there is no "durable + inline" mode. But every domain event we raise today is an inline
        // cross-domain replication projection (same-Id copies that in-request reads and follow-up commands
        // depend on), so they MUST stay inline via EventPublisher's InvokeAsync to preserve read-your-writes.
        // With no genuinely fire-and-forget event yet, nothing is routed through this outbox. It exists so
        // the first such event (Stage C) can opt in with a one-line PublishAsync + failure policy, against
        // already-proven infrastructure, without touching the replication path. The guardrail that this
        // stayed true is CrossDomainReplicationTests.
        opts.PersistMessagesWithSqlServer(connectionString, Persistence.ConfigureServices.WolverineSchemaName);
        opts.UseEntityFrameworkCoreTransactions();

        // Provision the envelope tables on startup. Two settings are needed, and this was the subtle part:
        //   - AutoBuildMessageStorageOnStartup: Wolverine's own default is CreateOrUpdate, but JasperFx
        //     overrides it from the active runtime profile — whose Development default is AutoCreate.None —
        //     so it must be set explicitly or the runtime's migrate path is a no-op in dev/tests.
        //   - AddResourceSetupOnStartup: registers the hosted service that actually RUNS resource setup at
        //     boot. Without it the tables are silently NOT created even with AutoBuild on — verified against
        //     a real SQL Server container (RebuildAsync succeeded yet produced zero tables until this was
        //     added). This mirrors Wolverine's own end-to-end EF Core persistence fixture.
        // SetupOnly creates missing resources without wiping existing data (never ResetState — that clears
        // the store on every boot). This keeps a plain `dotnet run` / Testcontainers boot self-sufficient;
        // when JasperFx.Aspire lands as a follow-up, its `resources setup` startup gate is the equivalent
        // provisioning path for orchestrated environments.
        opts.AutoBuildMessageStorageOnStartup = JasperFx.AutoCreate.CreateOrUpdate;
        opts.Services.AddResourceSetupOnStartup();

        // Durable envelopes are serialized with System.Text.Json; our domain events carry NodaTime types
        // (Instant, LocalDate, LocalDateRange) and value objects, so register the same NodaTime converters
        // the API's controllers use (Program.cs) so payloads round-trip cleanly through the outbox store.
        opts.UseSystemTextJsonForSerialization(json => json.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb));

        // Validation parity with the old MediatR ValidationBehavior. ExplicitRegistration means
        // Wolverine does NOT scan for validators — we keep our own AddValidatorsFromAssembly
        // registrations (per module), so the default scanning mode would double-register and duplicate
        // every failure. The custom failure action throws our ValidationException so the
        // ExceptionMiddleware HTTP contract (422 + problem details) is unchanged.
        opts.UseFluentValidation(RegistrationBehavior.ExplicitRegistration);
        opts.Services.AddSingleton(typeof(IFailureAction<>), typeof(WaydValidationFailureAction<>));

        // Restore the acting user id into each handler's fresh DI scope (Wolverine runs every message
        // in a new scope). Required so Hangfire-originated sends keep audit attribution — see the
        // middleware's remarks.
        opts.Policies.AddMiddleware(typeof(UserIdentityMiddleware));

        // Long-running-request performance warning, ported from the MediatR PerformanceBehavior.
        opts.Policies.AddMiddleware(typeof(PerformanceBehavior));

        // Wolverine 6 codegen constructor-injects handler dependencies and, at the NotAllowed default,
        // throws when a dependency has a DI registration it cannot "see through". Nearly every handler
        // transitively hits one: EF's DbContextOptions lambda factory, the ten IXxxDbContext →
        // WaydDbContext interface factories, and CurrentUser's raw IServiceProvider (lazy IUserService,
        // breaking the genuine CurrentUser↔UserService cycle). AlwaysAllowed resolves those from the scope
        // exactly as MediatR did (behaviour parity), without the per-handler warning AllowedButWarn emits.
        // A per-type NotAllowed + AlwaysUseServiceLocationFor<T>() allow-list is impractical — the
        // transitive opaque graph is large and grows with each new DbContext facade.
        opts.ServiceLocationPolicy = ServiceLocationPolicy.AlwaysAllowed;

        // Codegen: STATIC everywhere, from the pre-generated handler tree committed under
        // Wayd.Web.Api/Internal/Generated/WolverineHandlers (produced by `dotnet run -- codegen write`).
        // Static means Wolverine loads those compiled types via the generated HandlerRegistry and NEVER
        // invokes Roslyn — which is why WolverineFx.RuntimeCompilation is no longer referenced at all.
        // WolverineFx core dropped the Roslyn runtime compiler (GH-2876), so under Dynamic/Auto a host
        // with no IAssemblyGenerator refuses to start; Static sidesteps that requirement entirely.
        //
        // The trade-off Static imposes: the committed tree is the source of truth, so it must be
        // regenerated whenever a handler (or a handler dependency's shape) changes, or the host will
        // dispatch stale/missing handler code. Two things keep it honest:
        //   - the RegenerateWolverineHandlers pre-build target in Wayd.Web.Api.csproj regenerates the
        //     tree locally on build (Debug), so a developer's inner loop stays current automatically;
        //   - CI runs `codegen write` and fails on any `git diff`, catching a tree that was committed
        //     stale (see WolverineCodegenFreshnessTests / the CI step).
        // This is invisible to `dotnet build` and unit tests (neither boots the host), so the
        // WolverineConfigurationValidityTests host-boot check is the regression guard that Static is
        // wired correctly.
        opts.CodeGeneration.TypeLoadMode = TypeLoadMode.Static;

        return opts;
    }
}
