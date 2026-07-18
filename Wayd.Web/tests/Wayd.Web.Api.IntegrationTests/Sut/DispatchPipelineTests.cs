using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Employees.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Models;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// End-to-end proof that a command dispatched through <see cref="IDispatcher"/> actually executes its
/// Wolverine-generated handler and persists — against the real SQL Server schema. Complements the
/// in-memory config-validity check (which only proves the generated code compiles) by proving the
/// pipeline runs: dispatch → validation middleware → handler → SaveChanges.
/// </summary>
[Trait("Category", "Docker")]
public sealed class DispatchPipelineTests(WaydSqlServerApiFactory factory)
    : IClassFixture<WaydSqlServerApiFactory>
{
    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public async Task Dispatch_CreateEmployeeCommand_RunsHandlerAndPersists()
    {
        // Arrange - fictional data (RFC-reserved acme.example domain), invented identifiers.
        _ = _factory.CreateClient();
        var employeeNumber = $"E2E-{Guid.NewGuid():N}"[..12];
        var command = new CreateEmployeeCommand(
            name: new PersonName("Dana", null, "Okoro"),
            employeeNumber: employeeNumber,
            hireDate: null,
            email: new EmailAddress($"{employeeNumber}@acme.example"),
            jobTitle: "Engineer",
            department: "Delivery",
            officeLocation: null,
            managerId: null);

        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IWaydDbContext>();

        // Act
        var result = await dispatcher.Send(command, TestContext.Current.CancellationToken);

        // Assert - the handler ran, returned success, and the row is in the real database.
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);

        var persisted = await dbContext.Employees
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == result.Value.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(persisted);
        Assert.Equal(employeeNumber, persisted!.EmployeeNumber);
    }

    [Fact]
    public async Task Dispatch_InvalidCreateEmployeeCommand_ThrowsValidationException()
    {
        // Arrange - empty employee number violates the FluentValidation rule, so the Wolverine
        // FluentValidation middleware must throw our ValidationException before the handler runs.
        _ = _factory.CreateClient();
        var command = new CreateEmployeeCommand(
            name: new PersonName("Dana", null, "Okoro"),
            employeeNumber: string.Empty,
            hireDate: null,
            email: new EmailAddress("valid@acme.example"),
            jobTitle: null,
            department: null,
            officeLocation: null,
            managerId: null);

        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        // Act / Assert
        await Assert.ThrowsAsync<Wayd.Common.Application.Exceptions.ValidationException>(
            () => dispatcher.Send(command, TestContext.Current.CancellationToken));
    }
}
