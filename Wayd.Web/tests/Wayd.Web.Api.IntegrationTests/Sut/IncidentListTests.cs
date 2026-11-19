using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Employees.Commands;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Models;
using Wayd.Infrastructure.Auth;
using Wayd.ProductManagement.Application;
using Wayd.ProductManagement.Application.Incidents.Commands;
using Wayd.ProductManagement.Application.Incidents.Dtos;
using Wayd.ProductManagement.Application.Incidents.Queries;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Releases.Commands;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Proves every field of the incidents list filters, sorts and lists its values on SQL Server. In-memory LINQ
/// accepts expressions EF cannot translate, so only a real provider shows that the field map's member access,
/// collection matches and option projections reach SQL.
/// </summary>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class IncidentListTests(WaydSqlServerApiFactory factory)
{
    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public async Task Fields_FilterSortAndListTheirValues()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];

        await IncidentFeatureFlags.Set(_factory, true);
        try
        {
            using var scope = _factory.Services.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            var dbContext = scope.ServiceProvider.GetRequiredService<IProductManagementDbContext>();

            var grace = await CreateEmployee(dispatcher, new PersonName("Grace", "Brewster", $"Hopper{suffix}", "Jr.", "Rear Admiral"), suffix);
            var alan = await CreateEmployee(dispatcher, new PersonName("Alan", null, $"Turing{suffix}"), suffix);

            var user = await _factory.CreateAuthenticatedClient(
                grace,
                ApplicationPermission.NameFor(ApplicationAction.Import, ApplicationResource.Incidents));
            scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId(user.UserId);

            var productTypeId = await dbContext.ProductTypes
                .Where(t => t.IsActive)
                .Select(t => t.Id)
                .FirstAsync(TestContext.Current.CancellationToken);
            var product = await dispatcher.Send(
                new CreateProductCommand($"Checkout {suffix}", null, productTypeId, null, null),
                TestContext.Current.CancellationToken);
            Assert.True(product.IsSuccess, product.IsFailure ? product.Error : null);
            var release = await dispatcher.Send(
                new PlanReleaseCommand(null, $"v{suffix}", null, null, null),
                TestContext.Current.CancellationToken);
            Assert.True(release.IsSuccess, release.IsFailure ? release.Error : null);

            var commandedTitle = $"Commanded {suffix}";
            var unassignedTitle = $"Unassigned {suffix}";
            var run = await ImportRuns.SubmitAndWait(scope, new ImportIncidentsCommand(
            [
                new SubmittedImportRow<ImportIncidentDto>("commanded", new ImportIncidentDto(
                    $"row-commanded-{suffix}", commandedTitle, null, null, null, "SEV-1", ProductStatusAlias.Open,
                    alan, grace, [product.Value.Id], [release.Value.Id], null, null, null)),
                new SubmittedImportRow<ImportIncidentDto>("unassigned", new ImportIncidentDto(
                    $"row-unassigned-{suffix}", unassignedTitle, null, null, null, "SEV-2", ProductStatusAlias.Open,
                    null, alan, [], [], null, null, null)),
            ]));
            Assert.True(
                run.Status == ImportProcessStatus.Succeeded,
                string.Join("; ", run.Rows.Where(r => r.Error != null).Select(r => $"{r.ImportId}: {r.Error}").Append(run.Error ?? "")));

            var ours = $"title=*{suffix}";

            // Act
            var byReporter = await Titles(dispatcher, $"{ours},reportedBy={grace}");
            var byCommander = await Titles(dispatcher, $"{ours},commander={alan}");
            var withoutCommander = await Titles(dispatcher, $"{ours},commander=");
            var byProduct = await Titles(dispatcher, $"{ours},products={product.Value.Id}");
            var byRelease = await Titles(dispatcher, $"{ours},releases={release.Value.Id}");
            var withAnyRelease = await Titles(dispatcher, $"{ours},releases!=");
            var byNoticed = await Titles(dispatcher, $"{ours},noticedAt>=2000-01-01T00:00:00Z");

            var reporterDescending = await Titles(dispatcher, ours, "reportedBy desc");
            var commanderAscending = await Titles(dispatcher, ours, "commander asc");
            var severityDescending = await Titles(dispatcher, ours, "severity desc");
            var otherSorts = await Task.WhenAll(
                new[] { "key asc", "title asc", "state asc", "noticedAt desc" }
                    .Select(sort => Titles(dispatcher, ours, sort)));

            var reporters = await Options(dispatcher, "reportedBy", suffix);
            var commanders = await Options(dispatcher, "commander", suffix);
            var products = await Options(dispatcher, "products", suffix);
            var releases = await Options(dispatcher, "releases", suffix);
            var severities = await Options(dispatcher, "severity", null);
            var states = await Options(dispatcher, "state", null);

            // Assert
            Assert.Equal([commandedTitle], byReporter);
            Assert.Equal([commandedTitle], byCommander);
            Assert.Equal([unassignedTitle], withoutCommander);
            Assert.Equal([commandedTitle], byProduct);
            Assert.Equal([commandedTitle], byRelease);
            Assert.Equal([commandedTitle], withAnyRelease);
            Assert.Equal(2, byNoticed.Count);

            // Grace sorts after Alan by name, though her incident's key is the lower one
            Assert.Equal([commandedTitle, unassignedTitle], reporterDescending);
            Assert.Equal([unassignedTitle, commandedTitle], commanderAscending);
            Assert.Equal([unassignedTitle, commandedTitle], severityDescending);
            Assert.All(otherSorts, titles => Assert.Equal(2, titles.Count));

            Assert.Equal(
                [new ListFilterOptionDto { Value = alan.ToString(), Label = $"Alan Turing{suffix}" },
                 new ListFilterOptionDto { Value = grace.ToString(), Label = $"Grace Hopper{suffix}" }],
                reporters.Options);
            Assert.Equal(
                [new ListFilterOptionDto { Value = alan.ToString(), Label = $"Alan Turing{suffix}" }],
                commanders.Options);
            Assert.Equal(
                [new ListFilterOptionDto { Value = product.Value.Id.ToString(), Label = $"Checkout {suffix}" }],
                products.Options);
            Assert.Equal(
                [new ListFilterOptionDto { Value = release.Value.Id.ToString(), Label = $"v{suffix}" }],
                releases.Options);
            Assert.Equal(
                severities.Options.Select(o => o.Label).Take(2),
                ["SEV-1", "SEV-2"]);
            Assert.Contains(states.Options, o => o.Value == "Open" && o.Label == "Open");
        }
        finally
        {
            await IncidentFeatureFlags.Set(_factory, false);
        }
    }

    private static async Task<Guid> CreateEmployee(IDispatcher dispatcher, PersonName name, string suffix)
    {
        var number = $"{name.FirstName[..1]}-{suffix}";
        var employee = await dispatcher.Send(
            new CreateEmployeeCommand(
                name,
                number,
                null,
                new EmailAddress($"{number}@integration.test"),
                null,
                null,
                null,
                null),
            TestContext.Current.CancellationToken);
        Assert.True(employee.IsSuccess, employee.IsFailure ? employee.Error : null);
        return employee.Value.Id;
    }

    private static async Task<List<string>> Titles(IDispatcher dispatcher, string filter, string? sort = null)
    {
        var page = await dispatcher.Send(
            new GetIncidentsQuery(new PageRequest(1, 50), Filter: filter, Sort: sort),
            TestContext.Current.CancellationToken);
        return [.. page.Items.Select(i => i.Title)];
    }

    private static Task<ListFilterOptionsDto> Options(IDispatcher dispatcher, string field, string? search) =>
        dispatcher.Send(new GetIncidentFilterOptionsQuery(field, search), TestContext.Current.CancellationToken);
}
