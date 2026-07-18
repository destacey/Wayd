using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Interfaces;
using Wayd.Web.Api.IntegrationTests.Infrastructure;
using Wolverine.Runtime;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Boots the real host so Wolverine fully initializes — handler discovery and runtime code generation
/// for every discovered handler chain. This is the regression guard for the class of failure a plain
/// <c>dotnet build</c> and the unit-test suite miss entirely (they never boot the host): e.g. a missing
/// runtime code generator (the WolverineFx.RuntimeCompilation dependency), a handler with an
/// unresolvable dependency, or an ambiguous route. If Wolverine cannot build its runtime, resolving
/// <see cref="IWolverineRuntime"/> throws and this test fails.
/// </summary>
public sealed class WolverineConfigurationValidityTests(WaydApiFactory factory)
    : IClassFixture<WaydApiFactory>
{
    private readonly WaydApiFactory _factory = factory;

    [Fact]
    public void Host_StartsAndWolverineRuntimeResolves()
    {
        // Arrange - creating the client forces the host (and thus Wolverine) to fully start.
        _ = _factory.CreateClient();

        // Act - resolving the Wolverine runtime forces full initialization (discovery + codegen).
        var runtime = _factory.Services.GetRequiredService<IWolverineRuntime>();

        // Assert
        Assert.NotNull(runtime);
    }

    [Fact]
    public void Dispatcher_ResolvesFromRequestScope()
    {
        // Arrange
        _ = _factory.CreateClient();

        // Act - the dispatch seam every call site depends on must resolve from a request scope
        // (IDispatcher is scoped, matching how controllers/jobs consume it).
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetService<IDispatcher>();

        // Assert
        Assert.NotNull(dispatcher);
    }
}
