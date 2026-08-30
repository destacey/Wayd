using System.Reflection;
using FluentAssertions;
using Wayd.ArchitectureTests.Helpers;
using Wayd.Common.Application.Dispatching;
using Wayd.Common.Application.Interfaces;

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
    /// </remarks>
    private static readonly string[] AllowedPrimitives =
    [
        "IDateTimeProvider",
        "IUserService",
        "ICurrentUser",
        "ICurrentPrincipal",
        "IEmployeeSourceFactory",
        "IWorkItemSourceFactory",
        "ICacheService",
        "IRequestCorrelationIdProvider",
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
                .Where(d => !AllowedPrimitives.Contains(d.Name))
                .Where(d => ServiceMarkers.Any(m => m.IsAssignableFrom(d)));

            violations.AddRange(peers.Select(peer => $"{service.FullName} depends on {peer.FullName}"));
        }

        // Assert
        violations.Should().BeEmpty(
            "a service must not depend on another service — that is how cycles form. Move the shared work down into the domain, or up into the handler that needs both. Violations: {0}",
            string.Join("; ", violations));
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
        violations.Should().BeEmpty(
            "a service must not dispatch — that inverts the tiering and lets a service re-enter the handlers above it. Orchestration across handlers belongs in a saga. Violations: {0}",
            string.Join("; ", violations));
    }

}
