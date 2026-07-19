using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Employees.Commands;
using Wayd.Common.Application.Identity;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Models;
using Wayd.Infrastructure.Auth;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Reproduces the Hangfire job identity scenario: a DI scope where <see cref="ICurrentUserInitializer"/>
/// has seeded a user id (as <c>WaydJobActivator</c> does from the job's UserId parameter), then dispatches
/// a command through <see cref="IDispatcher"/>. The audited entity written by the handler must carry that
/// user id in its <c>SystemCreatedBy</c> column — otherwise Wolverine's fresh-per-message scope has dropped
/// the identity (the exact silent regression <c>UserIdentityMiddleware</c> exists to prevent).
/// </summary>
[Trait("Category", "Docker")]
public sealed class HangfireIdentityPropagationTests(WaydSqlServerApiFactory factory)
    : IClassFixture<WaydSqlServerApiFactory>
{
    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public async Task Dispatch_FromJobScopeWithUserId_StampsUserIdOntoAuditColumns()
    {
        // Arrange - mimic WaydJobActivator: a fresh scope with the acting user id seeded, no HttpContext.
        _ = _factory.CreateClient();
        const string jobUserId = "job-user-abc-123";

        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId(jobUserId);

        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var employeeNumber = $"E2E-{Guid.NewGuid():N}"[..12];
        var command = new CreateEmployeeCommand(
            name: new PersonName("Ada", null, "Mensah"),
            employeeNumber: employeeNumber,
            hireDate: null,
            email: new EmailAddress($"{employeeNumber}@acme.example"),
            jobTitle: null,
            department: null,
            officeLocation: null,
            managerId: null);

        // Act
        var result = await dispatcher.Send(command, TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);

        // Assert - project the SystemCreatedBy shadow property directly from the database.
        using var readScope = _factory.Services.CreateScope();
        var dbContext = readScope.ServiceProvider.GetRequiredService<IWaydDbContext>();
        var systemCreatedBy = await dbContext.Employees
            .Where(e => e.Id == result.Value.Id)
            .Select(e => EF.Property<string?>(e, "SystemCreatedBy"))
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(jobUserId, systemCreatedBy);
    }

    [Fact]
    public async Task Dispatch_FromScopeWithoutUser_StampsSystemActorOnAuditColumns()
    {
        // Arrange - a background scope with NO acting user (scheduled job, startup work): the platform
        // itself is acting, and the audit columns must say so explicitly rather than stay empty.
        _ = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var employeeNumber = $"SYS-{Guid.NewGuid():N}"[..12];
        var command = new CreateEmployeeCommand(
            name: new PersonName("Sam", null, "Okafor"),
            employeeNumber: employeeNumber,
            hireDate: null,
            email: new EmailAddress($"{employeeNumber}@acme.example"),
            jobTitle: null,
            department: null,
            officeLocation: null,
            managerId: null);

        // Act
        var result = await dispatcher.Send(command, TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);

        // Assert
        using var readScope = _factory.Services.CreateScope();
        var dbContext = readScope.ServiceProvider.GetRequiredService<IWaydDbContext>();
        var systemCreatedBy = await dbContext.Employees
            .Where(e => e.Id == result.Value.Id)
            .Select(e => EF.Property<string?>(e, "SystemCreatedBy"))
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SystemIdentity.UserId, systemCreatedBy);
    }
}
