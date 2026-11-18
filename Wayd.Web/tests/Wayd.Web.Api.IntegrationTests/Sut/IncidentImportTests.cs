using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Employees.Commands;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.FeatureManagement.Commands;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Models;
using Wayd.Infrastructure.Auth;
using Wayd.ProductManagement.Application;
using Wayd.ProductManagement.Application.Incidents.Commands;
using Wayd.ProductManagement.Application.Incidents.Dtos;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Proves an incident file applies row by row on a real provider, and that a preflight of one leaves no row behind.
/// </summary>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class IncidentImportTests(WaydSqlServerApiFactory factory)
{
    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public async Task Import_CreatesOpenAndClosed_AndAPreflightLeavesNothing()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var openTitle = $"Open {suffix}";
        var closedTitle = $"Closed {suffix}";
        var preflightTitle = $"Preflight {suffix}";

        await SetIncidentFlags(true);
        try
        {
            using var scope = _factory.Services.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            var employee = await dispatcher.Send(
                new CreateEmployeeCommand(
                    new PersonName("Ira", null, "Importer"),
                    $"II-{suffix}",
                    null,
                    new EmailAddress($"ii-{suffix}@integration.test"),
                    null,
                    null,
                    null,
                    null),
                TestContext.Current.CancellationToken);
            Assert.True(employee.IsSuccess, employee.IsFailure ? employee.Error : null);

            var user = await _factory.CreateAuthenticatedClient(
                employee.Value.Id,
                ApplicationPermission.NameFor(ApplicationAction.Import, ApplicationResource.Incidents));
            scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId(user.UserId);

            var dbContext = scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>();

            // Act
            var run = await ImportRuns.SubmitAndWait(scope, new ImportIncidentsCommand(
            [
                new SubmittedImportRow<ImportIncidentDto>("open", Row(openTitle, ProductStatusAlias.Open)),
                new SubmittedImportRow<ImportIncidentDto>("closed", Row(closedTitle, ProductStatusAlias.Closed)),
            ]));

            var preflight = await ImportRuns.SubmitAndWait(scope, new ImportIncidentsCommand(
                [new SubmittedImportRow<ImportIncidentDto>("check", Row(preflightTitle, ProductStatusAlias.Open))],
                ValidateOnly: true));

            // Assert
            Assert.Equal(ImportProcessStatus.Succeeded, run.Status);
            Assert.Equal(ImportProcessStatus.Succeeded, preflight.Status);

            var saved = await dbContext.Incidents
                .AsNoTracking()
                .Where(i => i.Title == openTitle || i.Title == closedTitle || i.Title == preflightTitle)
                .Select(i => new { i.Title, i.StatusAlias })
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(ProductStatusAlias.Open, saved.Single(i => i.Title == openTitle).StatusAlias);
            Assert.Equal(ProductStatusAlias.Closed, saved.Single(i => i.Title == closedTitle).StatusAlias);
            Assert.DoesNotContain(saved, i => i.Title == preflightTitle);
        }
        finally
        {
            await SetIncidentFlags(false);
        }
    }

    private static ImportIncidentDto Row(string title, ProductStatusAlias status) =>
        new($"row-{title}",
            title,
            null,
            null,
            null,
            "SEV-1",
            status,
            null,
            null,
            [],
            [],
            null,
            null,
            null);

    private async Task SetIncidentFlags(bool enabled)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ActAsHarnessAdministrator();
        var ids = await scope.ServiceProvider.GetRequiredService<IFeatureManagementDbContext>().FeatureFlags
            .Where(f => f.Name == FeatureFlags.Names.ProductManagement || f.Name == FeatureFlags.Names.IncidentManagement)
            .Select(f => f.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        foreach (var id in ids)
        {
            var toggled = await dispatcher.Send(new ToggleFeatureFlagCommand(id, enabled), TestContext.Current.CancellationToken);
            Assert.True(toggled.IsSuccess, toggled.IsFailure ? toggled.Error : null);
        }
    }
}
