using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Persistence;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Organization.Application.Persistence;
using Wayd.Planning.Application.Persistence;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Every module's DbContext interface must resolve to the same <see cref="WaydDbContext"/> within one
/// scope.
/// </summary>
/// <remarks>
/// The import runner depends on this outright. A definition adds the records it creates through its own
/// module's interface — <c>IOrganizationDbContext</c>, <c>IPlanningDbContext</c> — while the runner saves
/// through <see cref="IImportDbContext"/>. Two instances would mean the runner saves the row outcomes and
/// nothing else: the import would report every row applied and create none of them.
/// <para>
/// Asserted against the booted container rather than reasoned about, because
/// <c>AddScoped&lt;TService, TImplementation&gt;</c> registers a descriptor per interface, and whether
/// those descriptors share an instance is a property of the registration, not something the type system
/// or a unit test with hand-wired fakes can show.
/// </para>
/// </remarks>
// The SQL Server factory, not WaydApiFactory: that one's UseInternalServiceProvider leaves the context
// unable to construct outside a request, which is what this test does.
[Collection(SqlServerApiTestCollection.Name)]
public sealed class DbContextScopeSharingTests(WaydSqlServerApiFactory factory)
{
    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public void EveryModuleInterface_ResolvesToOneContextWithinAScope()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider;

        // Act
        var context = provider.GetRequiredService<WaydDbContext>();

        // Assert
        provider.GetRequiredService<IImportDbContext>().Should().BeSameAs(context);
        provider.GetRequiredService<IWaydDbContext>().Should().BeSameAs(context);
        provider.GetRequiredService<IOrganizationDbContext>().Should().BeSameAs(context);
        provider.GetRequiredService<IPlanningDbContext>().Should().BeSameAs(context);
    }

    [Fact]
    public void ADefinitionsWritesAndTheRunnersSave_ShareAChangeTracker()
    {
        // Arrange — the arrangement the import runner actually relies on
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider;

        // Act
        var definitionContext = provider.GetRequiredService<IOrganizationDbContext>();
        var runnerContext = provider.GetRequiredService<IImportDbContext>();

        // Assert
        runnerContext.ChangeTracker.Should().BeSameAs(((WaydDbContext)definitionContext).ChangeTracker);
    }

    [Fact]
    public void SeparateScopes_GetSeparateContexts()
    {
        // Arrange — the other half of the contract: sharing is within a scope, not across the process
        using var first = _factory.Services.CreateScope();
        using var second = _factory.Services.CreateScope();

        // Act
        var a = first.ServiceProvider.GetRequiredService<IImportDbContext>();
        var b = second.ServiceProvider.GetRequiredService<IImportDbContext>();

        // Assert
        a.Should().NotBeSameAs(b);
    }
}
