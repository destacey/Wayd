using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Wayd.Common.Application.Employees.Commands;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Activities;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Models;
using Wayd.Infrastructure.Auth;
using Wayd.Organization.Application.Persistence;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Organization.Application.Teams.Dtos;
using Wayd.Organization.Domain.Models;
using Wayd.StrategicManagement.Application;
using Wayd.StrategicManagement.Application.StrategicThemes.Commands;
using Wayd.StrategicManagement.Application.StrategicThemes.Dtos;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Proves a preflight puts rows through an import's real passes and leaves nothing behind: no records, no
/// activity entries, no delivered events, and no identity values spent by the passes it never saves.
/// </summary>
/// <remarks>
/// The unit tests drive the runner against a fake with no transaction, so what a rollback undoes — and
/// what escapes one — is only observable here.
/// </remarks>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class ImportPreflightTests(WaydSqlServerApiFactory factory)
{
    private static readonly TimeSpan _runTimeout = TimeSpan.FromSeconds(60);

    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public async Task Preflight_OfAMultiPassImport_ResolvesALaterPassAgainstAnEarlierOneAndAppliesNothing()
    {
        // Arrange — the manager is created by the first pass and linked by the second, which finds them only
        // if the first pass's work is visible to it
        using var scope = CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var managerNumber = $"PFM-{suffix}";
        var reportNumber = $"PFR-{suffix}";
        var leaverNumber = $"PFL-{suffix}";
        var numbers = new[] { managerNumber, reportNumber, leaverNumber };

        var rows = new[]
        {
            Employee("e1", managerNumber, managerNumber: null),
            Employee("e2", reportNumber, managerNumber: managerNumber),
            Employee("e3", leaverNumber, managerNumber: managerNumber, isActive: false),
        };

        // Act
        ImportProcess run;
        List<Guid> stagedEmployeeIds;
        using (var recording = _factory.Saves.Record())
        {
            var submitted = await dispatcher.Send(
                new ImportEmployeesCommand(rows, ValidateOnly: true), TestContext.Current.CancellationToken);
            Assert.True(submitted.IsSuccess, submitted.IsFailure ? submitted.Error : null);

            run = await WaitForRun(submitted.Value);
            stagedEmployeeIds = [.. recording.AddedOf<Common.Domain.Employees.Employee>()
                .Where(e => numbers.Contains(e.EmployeeNumber))
                .Select(e => e.Id)];
        }

        // Assert — every row passed without the "manager could not be resolved" warning
        Assert.True(run.IsPreflight);
        Assert.Equal(ImportProcessStatus.Succeeded, run.Status);
        Assert.All(run.Rows, row =>
        {
            Assert.Equal(ImportRowStatus.Succeeded, row.Status);
            Assert.Null(row.Warning);
            Assert.Null(row.CreatedEntityId);
            Assert.NotNull(row.Payload);
        });

        // The employees were saved inside the transaction for the later passes to find, then rolled back
        Assert.Equal(3, stagedEmployeeIds.Count);
        var waydDbContext = scope.ServiceProvider.GetRequiredService<IWaydDbContext>();
        Assert.False(await waydDbContext.Employees.AnyAsync(e => numbers.Contains(e.EmployeeNumber), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Preflight_OfAnAtomicImportThatRejectsARow_ReportsEveryRowAndDeliversNoEvents()
    {
        // Arrange — one team exists already, applied for real. Its run is the control for the envelope
        // check below: it shows a durable team event is visible to the recorder when one is sent.
        using var scope = CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var existing = UniqueTeamCode();

        using (var control = _factory.Saves.Record())
        {
            var real = await dispatcher.Send(
                new ImportTeamsCommand([Team("t1", existing, TeamType.Team)]), TestContext.Current.CancellationToken);
            Assert.True(real.IsSuccess, real.IsFailure ? real.Error : null);
            Assert.Equal(ImportProcessStatus.Succeeded, (await WaitForRun(real.Value)).Status);
            Assert.Contains(control.EnvelopeMessageTypes(), t => t.Contains("TeamCreated", StringComparison.Ordinal));
        }

        var newTeam = UniqueTeamCode();
        var newTeamOfTeams = UniqueTeamCode();
        var rows = new[]
        {
            Team("t1", existing, TeamType.Team),
            Team("t2", newTeam, TeamType.Team),
            Team("t3", newTeamOfTeams, TeamType.TeamOfTeams),
        };

        // Act
        ImportProcess run;
        List<string> envelopes;
        using (var recording = _factory.Saves.Record())
        {
            var submitted = await dispatcher.Send(
                new ImportTeamsCommand(rows, ValidateOnly: true), TestContext.Current.CancellationToken);
            Assert.True(submitted.IsSuccess, submitted.IsFailure ? submitted.Error : null);

            run = await WaitForRun(submitted.Value);
            envelopes = [.. recording.EnvelopeMessageTypes()];
        }

        // Assert — the rejection is reported, and the other rows were still accepted
        Assert.Equal(ImportProcessStatus.Failed, run.Status);
        Assert.Contains("none of it", run.Error);
        Assert.Equal(ImportRowStatus.Failed, run.Rows.Single(r => r.ImportId == "t1").Status);
        Assert.Contains("already exists", run.Rows.Single(r => r.ImportId == "t1").Error);
        Assert.All(run.Rows.Where(r => r.ImportId != "t1"), r => Assert.Equal(ImportRowStatus.Succeeded, r.Status));

        // Nothing it accepted was delivered; the control run above shows the recorder would have seen it
        Assert.DoesNotContain(envelopes, t => t.Contains("Team", StringComparison.Ordinal));

        var organizationDbContext = scope.ServiceProvider.GetRequiredService<IOrganizationDbContext>();
        var codes = new[] { new TeamCode(newTeam), new TeamCode(newTeamOfTeams) };
        Assert.False(await organizationDbContext.BaseTeams.AnyAsync(t => codes.Contains(t.Code), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Preflight_OfASinglePassImport_SpendsNoIdentityValues()
    {
        // Arrange — a real theme first, so the identity has been used and its current value means something
        using var scope = CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var existing = UniqueThemeName();

        var real = await dispatcher.Send(new ImportStrategicThemesCommand([Theme("s1", existing)]), TestContext.Current.CancellationToken);
        Assert.True(real.IsSuccess, real.IsFailure ? real.Error : null);
        Assert.Equal(ImportProcessStatus.Succeeded, (await WaitForRun(real.Value)).Status);

        var themes = scope.ServiceProvider.GetRequiredService<IStrategicManagementDbContext>();
        var identityBefore = await CurrentThemeIdentity(themes);
        var newTheme = UniqueThemeName();

        // Act
        var submitted = await dispatcher.Send(
            new ImportStrategicThemesCommand([Theme("s1", existing), Theme("s2", newTheme)], ValidateOnly: true),
            TestContext.Current.CancellationToken);
        Assert.True(submitted.IsSuccess, submitted.IsFailure ? submitted.Error : null);
        var run = await WaitForRun(submitted.Value);

        // Assert
        Assert.Equal(ImportProcessStatus.Failed, run.Status);
        Assert.Equal(ImportRowStatus.Failed, run.Rows.Single(r => r.ImportId == "s1").Status);
        Assert.Equal(ImportRowStatus.Succeeded, run.Rows.Single(r => r.ImportId == "s2").Status);
        Assert.False(await themes.StrategicThemes.AnyAsync(t => t.Name == newTheme, TestContext.Current.CancellationToken));
        Assert.Equal(identityBefore, await CurrentThemeIdentity(themes));
    }

    [Fact]
    public async Task Apply_ImportsThePreflightedFileForReal()
    {
        // Arrange
        var user = await _factory.CreateAuthenticatedClient(
            ApplicationPermission.NameFor(ApplicationAction.Import, ApplicationResource.StrategicThemes));
        var name = UniqueThemeName();
        var csv = $"ImportId,Name,Description,State\ns1,{name},Checked before it was applied.,Active\n";
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");

        var preflightResponse = await user.Client.PostAsync(
            "/api/strategic-management/strategic-themes/import?validateOnly=true",
            new MultipartFormDataContent { { file, "file", "themes.csv" } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, preflightResponse.StatusCode);
        var preflight = await ReadJson(preflightResponse);
        Assert.True(preflight.GetProperty("isPreflight").GetBoolean());
        Assert.Equal(nameof(ImportProcessStatus.Succeeded), preflight.GetProperty("status").GetString());

        using var scope = CreateScope();
        var themes = scope.ServiceProvider.GetRequiredService<IStrategicManagementDbContext>();
        Assert.False(await themes.StrategicThemes.AnyAsync(t => t.Name == name, TestContext.Current.CancellationToken));

        // Act
        var applyResponse = await user.Client.PostAsync(
            $"/api/imports/{preflight.GetProperty("id").GetGuid()}/apply", content: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, applyResponse.StatusCode);
        var applied = await ReadJson(applyResponse);
        Assert.False(applied.GetProperty("isPreflight").GetBoolean());
        Assert.Equal(nameof(ImportProcessStatus.Succeeded), applied.GetProperty("status").GetString());
        Assert.NotEqual(preflight.GetProperty("id").GetGuid(), applied.GetProperty("id").GetGuid());
        Assert.True(await themes.StrategicThemes.AnyAsync(t => t.Name == name, TestContext.Current.CancellationToken));

        // The preflight now points at the run its rows became
        var linked = await WaitForRun(preflight.GetProperty("id").GetGuid());
        Assert.Equal(applied.GetProperty("id").GetGuid(), linked.AppliedImportProcessId);
    }

    private IServiceScope CreateScope()
    {
        var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId("import-preflight-test");
        return scope;
    }

    /// <summary>Reads the run back in a scope of its own once it has finished, rows included.</summary>
    private async Task<ImportProcess> WaitForRun(Guid importProcessId)
    {
        var elapsed = Stopwatch.StartNew();

        while (true)
        {
            using var scope = _factory.Services.CreateScope();
            var run = await scope.ServiceProvider.GetRequiredService<IImportDbContext>().ImportProcesses
                .AsNoTracking()
                .Include(p => p.Rows)
                .SingleAsync(p => p.Id == importProcessId, TestContext.Current.CancellationToken);

            if (run.IsTerminal)
                return run;

            Assert.True(elapsed.Elapsed < _runTimeout, $"Import {importProcessId} was still {run.Status} after {_runTimeout}.");
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }

    private static async Task AssertNoneSurvived(IServiceScope scope, List<ActivityLogEntry> recorded)
    {
        var ids = recorded.Select(a => a.Id).ToList();
        var activity = scope.ServiceProvider.GetRequiredService<IActivityLogDbContext>();

        Assert.False(await activity.ActivityLogs.AnyAsync(a => ids.Contains(a.Id), TestContext.Current.CancellationToken));
    }

    private static Task<decimal> CurrentThemeIdentity(IStrategicManagementDbContext themes) =>
        themes.Database
            .SqlQuery<decimal>($"SELECT IDENT_CURRENT('[StrategicManagement].[StrategicThemes]') AS Value")
            .SingleAsync(TestContext.Current.CancellationToken);

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response) =>
        (await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken)).RootElement;

    private static SubmittedImportRow<ImportEmployeeDto> Employee(
        string importId, string number, string? managerNumber, bool isActive = true) =>
        new(importId, new ImportEmployeeDto(
            number, "Preflight", null, number, new EmailAddress($"{number.ToLowerInvariant()}@preflight.test"),
            HireDate: null, JobTitle: null, Department: null, OfficeLocation: null, managerNumber, isActive));

    private static SubmittedImportRow<ImportTeamDto> Team(string importId, string code, TeamType type) =>
        new(importId, new ImportTeamDto(type, $"Preflight {code}", new TeamCode(code), null, new LocalDate(2026, 1, 5)));

    private static SubmittedImportRow<ImportStrategicThemeDto> Theme(string importId, string name) =>
        new(importId, new ImportStrategicThemeDto(name, "Submitted by the import preflight test.", StrategicThemeState.Active));

    private static string UniqueTeamCode() => $"P{Guid.NewGuid():N}"[..10].ToUpperInvariant();

    private static string UniqueThemeName() => $"Preflight {Guid.NewGuid():N}"[..24];
}
