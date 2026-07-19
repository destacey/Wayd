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

        builder.UseWolverine(opts => opts.ConfigureWayd(applicationAssembly, connectionString));
        return builder;
    }

    private static WolverineOptions ConfigureWayd(this WolverineOptions opts, System.Reflection.Assembly applicationAssembly, string connectionString)
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
        // UseEntityFrameworkCoreTransactions lets outgoing messages enlist in the WaydDbContext SaveChanges
        // transaction, so an envelope commits atomically with the entity change and is delivered post-commit
        // by the durability agent. The DbContext half — AddDbContextWithWolverineIntegration<WaydDbContext> —
        // is wired in AddPersistence (an IServiceCollection call; these are WolverineOptions calls).
        //
        // Which events use it is decided by DurableEventRoutes, consumed in BaseDbContext: durable events
        // enlist here; everything else dispatches inline via EventPublisher.InvokeAsync (the outbox is
        // post-commit/async only — there is no "durable + inline" mode). Inline dispatch preserves
        // read-your-writes for the cross-domain replication projections, guarded by CrossDomainReplicationTests.
        opts.PersistMessagesWithSqlServer(connectionString, Persistence.ConfigureServices.WolverineSchemaName);
        opts.UseEntityFrameworkCoreTransactions();

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

        // Warns on long-running requests.
        opts.Policies.AddMiddleware(typeof(PerformanceBehavior));

        // Failure policy for the durable event chains: retry-with-cooldown → dead-letter, scoped to the
        // durable event types only (see DurableEventFailurePolicy for why it must not be global). Durable
        // handlers run outside the request, so their failures are governed here rather than by
        // ExceptionMiddleware.
        opts.Policies.Add<DurableEventFailurePolicy>();

        // Wolverine 6 codegen constructor-injects handler dependencies and, at the NotAllowed default, throws
        // when a dependency has a DI registration it cannot "see through". Nearly every handler transitively
        // hits one: EF's DbContextOptions lambda factory, the ten IXxxDbContext → WaydDbContext interface
        // factories, and CurrentUser's raw IServiceProvider (lazy IUserService, which breaks the genuine
        // CurrentUser↔UserService cycle). AlwaysAllowed resolves those from the scope without the per-handler
        // warning AllowedButWarn emits. A per-type NotAllowed + AlwaysUseServiceLocationFor<T>() allow-list is
        // impractical — the transitive opaque graph is large and grows with each new DbContext facade.
        opts.ServiceLocationPolicy = ServiceLocationPolicy.AlwaysAllowed;

        // Codegen is STATIC in every environment: Wolverine loads compiled handler types via the
        // HandlerRegistry pre-generated under Wayd.Web.Api/Internal/Generated/WolverineHandlers (produced by
        // `dotnet run -- codegen write`) and never invokes Roslyn. This is why WolverineFx.RuntimeCompilation
        // is not referenced — WolverineFx core dropped the Roslyn runtime compiler (GH-2876), so a host under
        // Dynamic/Auto with no IAssemblyGenerator refuses to start.
        //
        // Consequence: the committed tree is the source of truth and must be regenerated whenever a handler
        // (or the shape of a handler dependency) changes, or the host dispatches stale/missing code. The
        // RegenerateWolverineHandlers pre-build target (Debug) keeps the local tree current on build, and CI
        // runs `codegen write` + fails on any `git diff`. None of this is visible to `dotnet build` or unit
        // tests (neither boots the host); WolverineConfigurationValidityTests is the host-boot regression guard.
        opts.CodeGeneration.TypeLoadMode = TypeLoadMode.Static;

        return opts;
    }
}
