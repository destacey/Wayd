using System.Reflection;
using FluentAssertions;
using Wayd.ArchitectureTests.Helpers;
using Wayd.Common.Application.Dispatching;
using Wayd.Common.Application.Events;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Application.Interfaces;
using Wayd.Infrastructure.Common.Services;
using Wayd.Infrastructure.Identity;
using Wayd.Integrations.Abstractions;

namespace Wayd.ArchitectureTests.Sut;

/// <summary>
/// Enforces the one-way dependency flow between the application tiers.
/// </summary>
/// <remarks>
/// <code>
/// saga  →  handlers  →  service  →  DbContext / domain
/// </code>
/// A saga may dispatch handlers. A handler may inject several services. A service may reach the
/// database, the domain, and framework primitives — a clock, the current user, a cache — and nothing
/// else. Both prohibitions below exist to keep this a tree: the moment a service can reach a peer or
/// the dispatcher, a cycle becomes constructible, and cycles among services are what made the pattern
/// worth avoiding in the first place.
/// </remarks>
public class ServiceLayeringTests
{
    private static readonly Type[] ServiceMarkers = [typeof(IScopedService), typeof(ITransientService)];

    /// <summary>
    /// Abstractions that carry a service marker but are framework primitives, not peers.
    /// </summary>
    /// <remarks>
    /// The markers do double duty in this codebase: they drive DI registration, so a clock and a
    /// factory wear one just to be registered. Depending on these is explicitly allowed — they hold no
    /// business rules, so they cannot take part in the cycles the peer rule exists to prevent.
    /// <para>
    /// Held as types rather than names. Matching on <c>Type.Name</c> exempted any interface sharing a
    /// name in any namespace, so a genuine peer could be waved through by coincidence — and an entry
    /// for a type that no longer exists sat here unnoticed, because a name that matches nothing fails
    /// silently. A <c>typeof</c> makes that a compile error.
    /// </para>
    /// </remarks>
    private static readonly Type[] AllowedPrimitives =
    [
        typeof(IDateTimeProvider),
        typeof(IUserService),
        typeof(ICurrentUser),
        typeof(IEventPublisher),
    ];

    /// <summary>
    /// Narrow seams over persistence, which the tier rule already permits a service to reach.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="AllowedPrimitives"/> rather than folded into it. A clock and a store
    /// are exempt for different reasons: the first holds no state a cycle could run through, while the
    /// second is the database by another name — "a service may reach the database" is the rule, and
    /// these exist so a write path can be tested without a full <c>WaydDbContext</c>. Naming them
    /// together would make the primitives list mean "things we decided to allow", which is precisely
    /// the vagueness that let a stale entry sit unnoticed.
    /// </remarks>
    private static readonly Type[] AllowedPersistenceSeams =
    [
        typeof(IUserIdentityStore),
    ];

    /// <summary>
    /// Violations that predate this rule being enforced, kept as a shrinking list.
    /// </summary>
    /// <remarks>
    /// These are real, not exemptions. The rule scanned no infrastructure assembly for as long as its
    /// assembly loader used a pattern matching nothing, so both edges were introduced while the test
    /// that forbids them was passing over an empty set. Fixing them is a behaviour change to identity
    /// and authentication, which does not belong in the change that turned the rule back on.
    /// <para>
    /// <c>UserService</c> dispatches <c>GetEmployeeByEmailQuery</c> to resolve an employee id, which the
    /// tiering says belongs to a saga or to the handler above it. <c>RoleService</c> asks
    /// <c>IOidcProviderDefaultRoleChecker</c> whether a role is still referenced before deleting it — a
    /// business rule, and so a genuine peer edge rather than a primitive.
    /// </para>
    /// <para>
    /// The list is asserted to be exact: fixing one without removing it here fails, and so does adding
    /// a new violation. Neither can pass quietly.
    /// </para>
    /// </remarks>
    private static readonly string[] KnownViolations =
    [
        "Wayd.Infrastructure.Identity.RoleService depends on Wayd.Common.Application.Identity.Roles.IOidcProviderDefaultRoleChecker",
        "Wayd.Infrastructure.Identity.UserService",
    ];

    /// <summary>
    /// Every concrete service: a class implementing one of the marker interfaces.
    /// </summary>
    private static List<Type> GetServiceImplementations()
    {
        var assemblies = AssemblyHelper.GetApplicationAssemblies()
            .Concat(AssemblyHelper.GetInfrastructureAssemblies())
            .Distinct()
            .ToArray();

        return assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => ServiceMarkers.Any(m => m.IsAssignableFrom(t)))
            // A saga is the tier above: it exists to dispatch, and ISaga extends ITransientService so
            // the DI scan still finds it. Both rules below are about services specifically.
            .Where(t => !typeof(ISaga).IsAssignableFrom(t))
            .ToList();
    }

    private static IEnumerable<Type> DependenciesOf(Type type) =>
        type.GetConstructors()
            .SelectMany(c => c.GetParameters().Select(p => p.ParameterType))
            .Concat(type
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Select(f => f.FieldType));

    [Fact]
    public void Services_ShouldNotDependOnOtherServices()
    {
        // Arrange
        var services = GetServiceImplementations();
        var violations = new List<string>();

        // Act
        foreach (var service in services)
        {
            // The marker sits on the service interface, so an injected peer is caught whether it comes
            // in as its abstraction (the normal case) or as a concrete type.
            var peers = DependenciesOf(service)
                .Distinct()
                .Where(d => d != service && !ServiceMarkers.Contains(d))
                .Where(d => !AllowedPrimitives.Contains(d))
                .Where(d => !AllowedPersistenceSeams.Contains(d))
                // An internal abstraction is reachable only from the assembly that declares it, so it
                // cannot be the shared edge between two modules that the peer rule exists to prevent.
                // The seams in Auth.Local are internal for exactly that reason, and cannot be named in
                // a typeof list from here at all.
                .Where(d => d.IsPublic)
                .Where(d => ServiceMarkers.Any(m => m.IsAssignableFrom(d)));

            violations.AddRange(peers.Select(peer => $"{service.FullName} depends on {peer.FullName}"));
        }

        // Assert
        violations.Should().BeEquivalentTo(
            KnownViolations.Where(v => v.Contains(" depends on ")),
            "a service must not depend on another service — that is how cycles form. Move the shared " +
            "work down into the domain, or up into the handler that needs both. Anything new here is a " +
            "regression; anything missing is fixed and should be removed from KnownViolations.");
    }

    /// <summary>
    /// Every exemption still exempts something.
    /// </summary>
    /// <remarks>
    /// Converting these lists to <c>typeof</c> made a deleted type a compile error, which is half the
    /// problem. The other half is an entry for a type that still exists but is no longer injected into
    /// any service: it compiles, it matches nothing, and it quietly widens the rule for a dependency
    /// nobody has. That is how <c>ICacheService</c> survived a rename of the thing it described.
    /// <para>
    /// It caught four on the run that introduced it. <c>IEmployeeSourceFactory</c> and
    /// <c>IWorkItemSourceFactory</c> are injected nowhere at all; <c>ICurrentPrincipal</c> and
    /// <c>IRequestCorrelationIdProvider</c> are injected into a middleware and a DbContext, neither of
    /// which is a service by the definition above — so exempting them for this rule granted nothing.
    /// A dependency that later needs one back will fail the peer rule and say so.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryExemption_IsStillInjectedSomewhere()
    {
        // Arrange
        var services = GetServiceImplementations();
        var injected = services.SelectMany(DependenciesOf).Distinct().ToHashSet();

        // Act
        var unused = AllowedPrimitives
            .Concat(AllowedPersistenceSeams)
            .Where(t => !injected.Contains(t))
            .Select(t => t.Name)
            .ToList();

        // Assert
        unused.Should().BeEmpty(
            "an exemption for a dependency nothing has widens the rule for nothing. Remove the entry, " +
            "or if the type is genuinely coming back, say so here. Unused: {0}",
            string.Join(", ", unused));
    }

    [Fact]
    public void Services_ShouldNotDependOnTheDispatcher()
    {
        // Arrange
        var services = GetServiceImplementations();
        var violations = new List<string>();

        // Act
        foreach (var service in services)
        {
            if (DependenciesOf(service).Any(d => d == typeof(IDispatcher)))
            {
                violations.Add(service.FullName!);
            }
        }

        // Assert
        violations.Should().BeEquivalentTo(
            KnownViolations.Where(v => !v.Contains(" depends on ")),
            "a service must not dispatch — that inverts the tiering and lets a service re-enter the " +
            "handlers above it. Orchestration across handlers belongs in a saga. Anything new here is a " +
            "regression; anything missing is fixed and should be removed from KnownViolations.");
    }

}
