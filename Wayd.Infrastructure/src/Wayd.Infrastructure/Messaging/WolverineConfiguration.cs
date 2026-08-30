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
/// Central Wolverine host configuration: Wolverine is the command/query/event mediator for the whole
/// application. Lives in Infrastructure alongside the other plumbing concerns (persistence, auth, Hangfire,
/// OpenTelemetry) so the host stays a thin orchestrator. Handler discovery is restricted to an explicit
/// allow-list of Application assemblies (plus Infrastructure, which owns two OIDC command handlers);
/// nothing is scanned from the wider dependency tree.
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
        typeof(Wayd.ProductManagement.Application.ConfigureServices),
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
    /// <c>Wayd.Web.Api</c>). Must be passed explicitly rather than inferred: <see cref="TypeLoadMode.Static"/>
    /// loads the generated <c>HandlerRegistry</c> from this exact assembly, and neither of Wolverine's
    /// inference strategies yields it — the caller of <c>UseWolverine</c> is <c>Wayd.Infrastructure</c> (this
    /// assembly), and <c>Assembly.GetEntryAssembly()</c> is the test-runner assembly under an integration-test
    /// host. Both are wrong and both fail Static type loading. Callers pass <c>typeof(Program).Assembly</c>.
    /// </param>
    public static TBuilder AddWaydWolverine<TBuilder>(this TBuilder builder, System.Reflection.Assembly applicationAssembly)
        where TBuilder : IHostApplicationBuilder
    {
        // The durable outbox stores its envelopes in the application database, read from the same config key
        // as AddPersistence (DatabaseSettings:ConnectionString). This read is EAGER: PersistMessagesWithSqlServer
        // (below) needs the connection string synchronously, before the host is built and before any deferred
        // ConfigureAppConfiguration runs. In a normal host that is fine — AddConfigurations() has already loaded
        // database.json and env vars into builder.Configuration. Integration-test hosts must therefore inject
        // their container connection string via an environment variable (visible to this eager read), not a
        // deferred override — see WaydSqlServerApiFactory.
        var connectionString = builder.Configuration["DatabaseSettings:ConnectionString"]
            ?? throw new InvalidOperationException(
                "DatabaseSettings:ConnectionString is required to configure the Wolverine durable outbox.");

        var typeLoadMode = ResolveTypeLoadMode(builder.Configuration, builder.Environment);

        // An in-memory / introspection boot with no reachable database (the WaydApiFactory config-validity host)
        // sets Wolverine:DisableDurableOutbox=true. The SQL message-store provisioning below (AutoBuild +
        // AddResourceSetupOnStartup) opens a SqlConnection at startup, which fails when there is no DB — killing
        // the host. This flag replaces that with MediatorOnly (no store provisioning, no durability agent),
        // leaving handler discovery + codegen + dispatch intact. Production and the Testcontainers factory leave
        // it false so the real durable outbox is provisioned. Note RunWolverineInSoloMode alone does NOT prevent
        // the store provisioning — only skipping AddResourceSetupOnStartup + AutoBuild does.
        var disableDurableOutbox = string.Equals(
            builder.Configuration["Wolverine:DisableDurableOutbox"], "true", StringComparison.OrdinalIgnoreCase);

        builder.UseWolverine(opts => opts.ConfigureWayd(applicationAssembly, connectionString, typeLoadMode, disableDurableOutbox));
        return builder;
    }

    /// <summary>
    /// Resolves the Wolverine codegen <see cref="TypeLoadMode"/> from configuration. The handler tree is NOT
    /// committed to the repo; it is generated by <c>codegen write</c> at pipeline time and compiled into the
    /// shipped artifact, so the mode is environment-driven rather than hardcoded:
    /// <list type="bullet">
    ///   <item><b>Static (default)</b> — loads the pre-generated <c>Internal/Generated/WolverineHandlers</c>
    ///   tree with no runtime Roslyn compiler. This is what CI-tested builds and every published artifact run,
    ///   giving fast cold start and no runtime compilation. It is the default so any context that forgets to
    ///   configure a mode fails safe onto the prod-faithful path.</item>
    ///   <item><b>Auto</b> — Wolverine compiles handlers at runtime via <c>WolverineFx.RuntimeCompilation</c>
    ///   (loading a pre-generated type if present, else compiling). Used ONLY for the local developer inner
    ///   loop (<c>appsettings.Development.json</c> sets <c>Wolverine:CodegenMode = Auto</c>) so a plain
    ///   <c>dotnet run</c> needs no committed tree and no codegen step.</item>
    /// </list>
    /// <para>
    /// Guard: Production MUST run Static. Roslyn runtime compilation in production would silently regress cold
    /// start (the reason the tree is generated ahead of time at all), so a Production host resolving to anything
    /// other than Static throws at boot rather than degrading quietly. <c>WolverineFx.RuntimeCompilation</c> is
    /// referenced unconditionally (so Auto works in dev/tests) but is never invoked on the Static path.
    /// </para>
    /// </summary>
    private static TypeLoadMode ResolveTypeLoadMode(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration["Wolverine:CodegenMode"];

        var mode = string.Equals(configured, "Auto", StringComparison.OrdinalIgnoreCase)
            ? TypeLoadMode.Auto
            : TypeLoadMode.Static;

        if (environment.IsProduction() && mode != TypeLoadMode.Static)
        {
            throw new InvalidOperationException(
                $"Wolverine:CodegenMode resolved to '{mode}' in the Production environment, but Production must run "
                + $"{nameof(TypeLoadMode.Static)} (pre-generated handlers, no runtime Roslyn) to preserve cold-start "
                + "performance. Remove the Auto override from this environment's configuration.");
        }

        return mode;
    }

    private static WolverineOptions ConfigureWayd(this WolverineOptions opts, System.Reflection.Assembly applicationAssembly, string connectionString, TypeLoadMode typeLoadMode, bool disableDurableOutbox)
    {
        // The assembly holding the pre-generated handler tree (Internal/Generated/WolverineHandlers), which
        // TypeLoadMode.Static loads the HandlerRegistry and per-handler types from. Must be Wayd.Web.Api; see
        // the AddWaydWolverine param docs for why it cannot be inferred. A wrong value fails only at first
        // dispatch (ExpectedTypeMissingException) or silently degrades to a runtime scan — never at build time.
        opts.ApplicationAssembly = applicationAssembly;

        // Discovery: only the assemblies we own that actually contain handlers. Wolverine already scans
        // the entry assembly (Wayd.Web.Api); include the rest explicitly.
        foreach (var marker in HandlerAssemblyMarkers)
        {
            opts.Discovery.IncludeAssembly(marker.Assembly);
        }

        // DURABLE TRANSACTIONAL OUTBOX. PersistMessagesWithSqlServer stores message envelopes in a dedicated
        // "wolverine" schema (never dbo), provisioned by Weasel at startup parallel to our EF migrations.
        // UseEntityFrameworkCoreTransactions lets outgoing messages enlist in a WaydDbContext SaveChanges
        // transaction so the envelope is durably persisted and delivered post-commit by the durability agent.
        // (BaseDbContext stages durable events into the change tracker after the entity save — because the
        // post-persistence events capture DB-generated Keys — and commits them with a second save; see there
        // for the ordering.) The DbContext half — AddDbContextWithWolverineIntegration<WaydDbContext> — is
        // wired in AddPersistence (an IServiceCollection call; these are WolverineOptions calls).
        //
        // Which events use it is decided by DurableEventRoutes, consumed in BaseDbContext: durable events
        // enlist here; everything else dispatches inline via EventPublisher.InvokeAsync (the outbox is
        // post-commit/async only — there is no "durable + inline" mode). Inline dispatch preserves
        // read-your-writes for the cross-domain replication projections, guarded by CrossDomainReplicationTests.
        if (disableDurableOutbox)
        {
            // In-memory / introspection boot with no reachable database (the WaydApiFactory config-validity host;
            // see AddWaydWolverine). Register NO SQL message store: PersistMessagesWithSqlServer would make
            // WolverineRuntime.StartAsync → tryMigrateStorage() open a SqlConnection and run migrations against the
            // (absent) database at startup — which times out and tears the host down (every test then fails with
            // ObjectDisposedException at CreateClient). Neither MediatorOnly nor skipping AddResourceSetupOnStartup
            // prevents that migration once the store is registered — the store must simply not exist. MediatorOnly
            // also turns off the durability agent. Handler discovery, codegen, and in-process dispatch (all this
            // guard checks) work without any persistence. Production and the Testcontainers WaydSqlServerApiFactory
            // leave the flag false and get the real durable outbox below.
            opts.Durability.Mode = DurabilityMode.MediatorOnly;
        }
        else
        {
            // DURABLE TRANSACTIONAL OUTBOX. PersistMessagesWithSqlServer stores message envelopes in a dedicated
            // "wolverine" schema (never dbo), provisioned by Weasel at startup parallel to our EF migrations.
            // UseEntityFrameworkCoreTransactions lets outgoing messages enlist in a WaydDbContext SaveChanges
            // transaction so the envelope is durably persisted and delivered post-commit by the durability agent.
            opts.PersistMessagesWithSqlServer(connectionString, Persistence.ConfigureServices.WolverineSchemaName);
            opts.UseEntityFrameworkCoreTransactions();

            // REQUIRED for the outbox to actually be durable: Wolverine's outbox only persists envelopes destined
            // for DURABLE endpoints. The durable events route to per-message-type LOCAL queues, which default to
            // BufferedInMemory — in that mode the "outbox" merely defers an in-memory publish until post-commit,
            // and a crash between commit and dispatch silently loses the event (verified 2026-07-19: zero envelope
            // rows were ever written). This policy flips every local queue to durable so the envelope is written to
            // wolverine_incoming_envelopes inside the same transaction as the entity change and recovered by the
            // durability agent after a crash. Wolverine's internal system queues (agents) are not affected.
            // Consequence: durable delivery is at-least-once for real now — durable-event handlers must stay
            // idempotent (see DurableEventRoutes), and handled envelopes remain visible to the messaging dashboard
            // for DurabilitySettings.KeepAfterMessageHandling (default 5 minutes) before cleanup.
            opts.Policies.UseDurableLocalQueues();

            // Provision the envelope tables on startup. BOTH settings are required, and each is silently a no-op
            // without the other:
            //   - AutoBuildMessageStorageOnStartup must be set explicitly. JasperFx overrides Wolverine's own
            //     CreateOrUpdate default from the active runtime profile (Development's default is AutoCreate.None),
            //     so without this the migrate path does nothing in dev/tests.
            //   - AddResourceSetupOnStartup registers the hosted service that actually runs resource setup at boot.
            //     Without it the tables are never created even with AutoBuild on.
            // CreateOrUpdate adds missing resources without wiping existing data (never ResetState, which would
            // clear the store every boot). This keeps a plain `dotnet run` / Testcontainers boot self-sufficient;
            // under the Aspire AppHost the equivalent path is its `resources setup` startup gate.
            opts.AutoBuildMessageStorageOnStartup = JasperFx.AutoCreate.CreateOrUpdate;
            opts.Services.AddResourceSetupOnStartup();
        }

        // Durable envelopes are serialized with System.Text.Json; our domain events carry NodaTime types
        // (Instant, LocalDate, LocalDateRange) and value objects, so register the same NodaTime converters
        // the API's controllers use (Program.cs) so payloads round-trip cleanly through the outbox store.
        opts.UseSystemTextJsonForSerialization(json => json.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb));

        // FluentValidation on the handler pipeline. ExplicitRegistration means Wolverine does NOT scan for
        // validators — we keep our own per-module AddValidatorsFromAssembly registrations, and the default
        // scanning mode would double-register and duplicate every failure. The custom failure action throws
        // our ValidationException so the ExceptionMiddleware HTTP contract (422 + problem details) holds.
        opts.UseFluentValidation(RegistrationBehavior.ExplicitRegistration);
        opts.Services.AddSingleton(typeof(IFailureAction<>), typeof(WaydValidationFailureAction<>));

        // Restore the acting user id into each handler's fresh DI scope (Wolverine runs every message in a
        // new scope). Required so Hangfire-originated sends keep audit attribution — see the middleware's
        // remarks.
        opts.Policies.AddMiddleware(typeof(UserIdentityMiddleware));

        // Rejects IRequireLinkedEmployee messages from callers with no employee link. Registered AFTER
        // UserIdentityMiddleware because it resolves the link for the acting user, and on a non-HTTP
        // dispatch that user id only exists once the identity middleware has seeded it from the envelope.
        opts.Policies.AddMiddleware(typeof(LinkedEmployeeMiddleware));

        // Warns on long-running requests.
        opts.Policies.AddMiddleware(typeof(PerformanceBehavior));

        // Failure policy for the durable event chains: retry-with-cooldown → dead-letter, scoped to the
        // durable event types only (see DurableEventFailurePolicy for why it must not be global). Durable
        // handlers run outside the request, so their failures are governed here rather than by
        // ExceptionMiddleware.
        opts.Policies.Add<DurableEventFailurePolicy>();

        // Wolverine 6 codegen constructor-injects handler dependencies and, at the NotAllowed default, throws
        // when a dependency has a DI registration it cannot "see through". This used to be impossible here:
        // CurrentUser injected raw IServiceProvider (its old lazy-IUserService cycle-breaker), and that single
        // registration poisoned every handler's transitive graph, forcing AlwaysAllowed. With the cycle now
        // broken properly (ICurrentPrincipal), codegen inline-constructs the full EF graph — DbContextOptions,
        // WaydDbContext, and the IXxxDbContext → WaydDbContext facades are all plain type-mapped registrations
        // it sees through; none of them needs service location. What remains genuinely opaque are the internal
        // implementation types below (public interface, impl not visible to the generated assembly), which the
        // allow-list opts in to scoped service location. A NEW internal-impl handler dependency will fail
        // `codegen write` (and therefore the local Debug build's regen target + CI staleness check) with an
        // InvalidServiceLocationException naming the type — add it here, or make the implementation public.
        opts.ServiceLocationPolicy = ServiceLocationPolicy.NotAllowed;
        opts.CodeGeneration.AlwaysUseServiceLocationFor<Wayd.Common.Application.Interfaces.IDispatcher>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<Wayd.Common.Application.Interfaces.ICurrentPrincipal>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<Wayd.Common.Application.Interfaces.IAzureDevOpsService>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<Wayd.Common.Application.Interfaces.ExternalPeople.IWorkdayConnectionInitializer>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<Wayd.Planning.Application.PokerSessions.Interfaces.IPokerSessionNotifier>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<Wayd.Planning.Application.StoryMaps.Interfaces.IStoryMapNotifier>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<Identity.IUserIdentityStore>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<Wayd.Common.Application.Identity.Users.IUserService>();
        opts.CodeGeneration.AlwaysUseServiceLocationFor<Wayd.Common.Application.Identity.Roles.IRoleService>();

        // AmbientUserId is allow-listed for CORRECTNESS, not opaqueness (it's a plain public scoped class):
        // UserIdentityMiddleware.Before writes the acting user id to it, and every consumer in the same message
        // scope must read that same instance — the inline-constructed CurrentUser/WaydDbContext AND any
        // scope-resolved service above (WolverineDispatcher stamps outgoing envelopes from ICurrentUser;
        // UserService audits through it). Without this, codegen `new`s a private AmbientUserId for the inline
        // graph while scope-resolved services get the scope's untouched instance, silently dropping audit
        // attribution on non-HTTP (Hangfire-originated) dispatch. Resolving it from the scope makes the
        // middleware's write visible to both graphs.
        opts.CodeGeneration.AlwaysUseServiceLocationFor<Auth.AmbientUserId>();

        // Codegen mode is resolved from configuration (see ResolveTypeLoadMode): Static for CI-tested builds and
        // every published artifact (pre-generated HandlerRegistry under Wayd.Web.Api/Internal/Generated/
        // WolverineHandlers, no runtime Roslyn, fast cold start), Auto for the local developer inner loop only.
        //
        // The handler tree is NOT committed to the repo — it is git-ignored and generated by `codegen write` at
        // pipeline time (CI generates it once and shares it as an artifact to both the Static integration tests
        // and the Docker image build, so the tested tree is byte-identical to the shipped tree). Codegen output
        // is sensitive to DI registration order and so is not reproducible across environments; generating it
        // exactly once per pipeline run is what keeps tested == shipped. None of this is visible to `dotnet build`
        // or unit tests (neither boots the host); WolverineConfigurationValidityTests is the host-boot guard.
        opts.CodeGeneration.TypeLoadMode = typeLoadMode;

        return opts;
    }
}
