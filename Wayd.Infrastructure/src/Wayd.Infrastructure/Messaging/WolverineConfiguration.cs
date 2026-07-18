using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wayd.Common.Application.Behaviors;
using Wayd.Common.Application.Validation;
using Wolverine;
using Wolverine.FluentValidation;

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
    public static TBuilder AddWaydWolverine<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.UseWolverine(opts => opts.ConfigureWayd(builder.Environment));
        return builder;
    }

    private static WolverineOptions ConfigureWayd(this WolverineOptions opts, IHostEnvironment environment)
    {
        // Discovery: only the assemblies we own that actually contain handlers. Wolverine already scans
        // the entry assembly (Wayd.Web.Api); include the rest explicitly.
        foreach (var marker in HandlerAssemblyMarkers)
        {
            opts.Discovery.IncludeAssembly(marker.Assembly);
        }

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

        // Cold-start codegen. WolverineFx core no longer ships the Roslyn compiler, so dynamic/auto
        // modes need WolverineFx.RuntimeCompilation registered (referenced by this project) via
        // UseRuntimeCompilation(); without it the host refuses to start (GH-2876). Auto tries any
        // pre-generated types first and falls back to runtime compilation, so it is safe everywhere.
        // Pre-generating types for CI/prod (TypeLoadMode.Static + `dotnet run -- codegen write`, which
        // then lets this compiler reference drop) is a follow-up build-pipeline task, not a parity
        // requirement for this phase.
        opts.UseRuntimeCompilation();
        opts.CodeGeneration.TypeLoadMode = environment.IsDevelopment()
            ? TypeLoadMode.Dynamic
            : TypeLoadMode.Auto;

        return opts;
    }
}
