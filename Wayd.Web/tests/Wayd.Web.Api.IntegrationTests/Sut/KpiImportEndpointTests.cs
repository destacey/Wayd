using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.FeatureManagement.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// The KPI import over HTTP: the files it reads, how a measurement or checkpoint finds its KPI's row, and the
/// submission it refuses before anything is stored.
/// </summary>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class KpiImportEndpointTests(WaydSqlServerApiFactory factory)
{
    private const string ImportRoute = "/api/kpis/import";

    private const string KpiHeader =
        "ImportId,SubjectType,SubjectId,Name,Description,CategoryId,Prefix,Suffix,ExpressedAs,Direction,BaselineValue,BaselineWindowStart,BaselineWindowEnd,TargetKind,TargetValue,AtRiskKind,AtRiskValue,IsClosed,WindowKind,WindowAnchorDate,WindowRollingCount,WindowRollingUnit,RollingAverageOf";

    private const string MeasurementHeader = "KpiImportId,PeriodStart,PeriodEnd,AsOf,MeasuredAt,Value,MeasuredOn,SampleSize,Note";

    private static readonly string _administerPermission =
        ApplicationPermission.NameFor(ApplicationAction.Administer, ApplicationResource.Kpis);

    private readonly WaydSqlServerApiFactory _factory = factory;

    /// <param name="window">The four window cells: kind, anchor date, rolling count, rolling unit.</param>
    private static string KpiRow(string importId, Guid teamId, string name, string window = ",,,") =>
        $"{importId},organization.team,{teamId},{name},,,,days,Value,Decrease,20,,,Absolute,10,,,false,{window},";

    private const string CheckpointHeader = "KpiImportId,Date,Label,PlannedValue,AtRiskValue";

    private static MultipartFormDataContent Files(string kpis, string measurements, string? checkpoints = null)
    {
        static ByteArrayContent Csv(string text)
        {
            var content = new ByteArrayContent(Encoding.UTF8.GetBytes(text));
            content.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            return content;
        }

        var files = new MultipartFormDataContent
        {
            { Csv(kpis), "file", "kpis.csv" },
            { Csv(measurements), "measurementsFile", "measurements.csv" },
        };

        if (checkpoints is not null)
            files.Add(Csv(checkpoints), "checkpointsFile", "checkpoints.csv");

        return files;
    }

    private static async Task<JsonElement> ReadBody(HttpResponseMessage response) =>
        (await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken)).RootElement;

    /// <summary>A new team to import onto, with the KPI feature turned on.</summary>
    private async Task<Guid> PrepareTeam()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ActAsHarnessAdministrator();

        var flagId = await scope.ServiceProvider.GetRequiredService<IFeatureManagementDbContext>().FeatureFlags
            .Where(f => f.Name == FeatureFlags.Names.Kpis)
            .Select(f => f.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var toggled = await dispatcher.Send(new ToggleFeatureFlagCommand(flagId, true), TestContext.Current.CancellationToken);
        Assert.True(toggled.IsSuccess, toggled.IsFailure ? toggled.Error : null);

        var code = $"K{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var team = await dispatcher.Send(
            new CreateTeamCommand($"KPI Import {code}", new TeamCode(code), null, new LocalDate(2026, 1, 1)),
            TestContext.Current.CancellationToken);
        Assert.True(team.IsSuccess, team.IsFailure ? team.Error : null);
        return team.Value.Id;
    }

    private async Task<List<(string Name, int Measurements)>> KpisOn(Guid teamId)
    {
        using var scope = _factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<IKpisDbContext>().Kpis
                .AsNoTracking()
                .Where(k => k.SubjectId == teamId)
                .Select(k => new { k.Name, Count = k.Measurements.Count })
                .ToListAsync(TestContext.Current.CancellationToken))
            .Select(k => (k.Name, k.Count))
            .ToList();
    }

    [Fact]
    public async Task Import_GivesEachKpiTheMeasurementsNamingIt_IncludingARowKnownByItsPosition()
    {
        // Arrange — the second KPI has no ImportId, so its measurements name it by its row number
        var teamId = await PrepareTeam();
        var user = await _factory.CreateAuthenticatedClient(_administerPermission);
        var kpis = $"{KpiHeader}\n{KpiRow("cycle", teamId, "Cycle time")}\n{KpiRow("", teamId, "Lead time")}\n";
        var measurements = $"{MeasurementHeader}\ncycle,2026-01-01,2026-01-31,,,18,,,\n2,2026-01-01,2026-01-31,,,30,,,\n2,2026-02-01,2026-02-28,,,28,,,\n";

        // Act
        var response = await user.Client.PostAsync(ImportRoute, Files(kpis, measurements), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(nameof(ImportProcessStatus.Succeeded), (await ReadBody(response)).GetProperty("status").GetString());
        Assert.Equal([("Cycle time", 1), ("Lead time", 2)], (await KpisOn(teamId)).OrderBy(k => k.Name));
    }

    [Fact]
    public async Task Import_AcceptsAMeasurementsFileWithOnlyItsHeader()
    {
        // Arrange
        var teamId = await PrepareTeam();
        var user = await _factory.CreateAuthenticatedClient(_administerPermission);
        var kpis = $"{KpiHeader}\n{KpiRow("cycle", teamId, "Cycle time")}\n";

        // Act
        var response = await user.Client.PostAsync(ImportRoute, Files(kpis, $"{MeasurementHeader}\n"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([("Cycle time", 0)], await KpisOn(teamId));
    }

    [Fact]
    public async Task Import_ReadsPointInTimeMomentsWithTheirOffsets()
    {
        // Arrange — the same afternoon written once in UTC and once with Chicago's offset, a morning reading beside it
        var teamId = await PrepareTeam();
        var user = await _factory.CreateAuthenticatedClient(_administerPermission);
        var kpis = $"{KpiHeader}\n{KpiRow("bugs", teamId, "Open bugs", "PointInTime,,,")}\n";
        var measurements = $"{MeasurementHeader}\nbugs,,,,2026-03-06T09:00:00-06:00,45,,,\nbugs,,,,2026-03-06T21:00:00Z,42,,,\n";

        // Act
        var response = await user.Client.PostAsync(ImportRoute, Files(kpis, measurements), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(nameof(ImportProcessStatus.Succeeded), (await ReadBody(response)).GetProperty("status").GetString());
        Assert.Equal([("Open bugs", 2)], await KpisOn(teamId));
    }

    [Fact]
    public async Task Import_RefusesAMomentWithoutAnOffset()
    {
        // Arrange
        var teamId = await PrepareTeam();
        var user = await _factory.CreateAuthenticatedClient(_administerPermission);
        var kpis = $"{KpiHeader}\n{KpiRow("bugs", teamId, "Open bugs", "PointInTime,,,")}\n";
        var measurements = $"{MeasurementHeader}\nbugs,,,,2026-03-06T15:00:00,42,,,\n";

        // Act
        var response = await user.Client.PostAsync(ImportRoute, Files(kpis, measurements), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("MeasuredAt", (await ReadBody(response)).GetRawText());
        Assert.Empty(await KpisOn(teamId));
    }

    [Fact]
    public async Task Import_RefusesTheSubmission_WhenAMeasurementNamesAKpiNotInTheFile()
    {
        // Arrange
        var teamId = await PrepareTeam();
        var user = await _factory.CreateAuthenticatedClient(_administerPermission);
        var kpis = $"{KpiHeader}\n{KpiRow("cycle", teamId, "Cycle time")}\n";
        var measurements = $"{MeasurementHeader}\ncycel,2026-01-01,2026-01-31,,,18,,,\n";

        // Act
        var response = await user.Client.PostAsync(ImportRoute, Files(kpis, measurements), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("'cycel'", (await ReadBody(response)).GetRawText());
        Assert.Empty(await KpisOn(teamId));
    }

    [Fact]
    public async Task Import_GivesEachKpiTheCheckpointsNamingIt()
    {
        // Arrange — a Decrease KPI, so each at-risk value sits above its planned value
        var teamId = await PrepareTeam();
        var user = await _factory.CreateAuthenticatedClient(_administerPermission);
        var kpis = $"{KpiHeader}\n{KpiRow("cycle", teamId, "Cycle time")}\n";
        var checkpoints = $"{CheckpointHeader}\ncycle,2026-06-30,Q2,14,16\ncycle,2026-03-31,Q1,17,\n";

        // Act
        var response = await user.Client.PostAsync(ImportRoute, Files(kpis, $"{MeasurementHeader}\n", checkpoints), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(nameof(ImportProcessStatus.Succeeded), (await ReadBody(response)).GetProperty("status").GetString());

        using var scope = _factory.Services.CreateScope();
        var labels = await scope.ServiceProvider.GetRequiredService<IKpisDbContext>().Kpis
            .AsNoTracking()
            .Where(k => k.SubjectId == teamId)
            .SelectMany(k => k.Checkpoints)
            .OrderBy(c => c.Date)
            .Select(c => c.Label)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["Q1", "Q2"], labels);
    }

    [Fact]
    public async Task Import_RefusesTheSubmission_WhenACheckpointNamesAKpiNotInTheFile()
    {
        // Arrange
        var teamId = await PrepareTeam();
        var user = await _factory.CreateAuthenticatedClient(_administerPermission);
        var kpis = $"{KpiHeader}\n{KpiRow("cycle", teamId, "Cycle time")}\n";
        var checkpoints = $"{CheckpointHeader}\ncycel,2026-03-31,Q1,17,\n";

        // Act
        var response = await user.Client.PostAsync(ImportRoute, Files(kpis, $"{MeasurementHeader}\n", checkpoints), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("'cycel'", (await ReadBody(response)).GetRawText());
        Assert.Empty(await KpisOn(teamId));
    }

    [Fact]
    public async Task Import_RefusesTheSubmission_WhenACheckpointRowCannotBeRead()
    {
        // Arrange
        var teamId = await PrepareTeam();
        var user = await _factory.CreateAuthenticatedClient(_administerPermission);
        var kpis = $"{KpiHeader}\n{KpiRow("cycle", teamId, "Cycle time")}\n";
        var checkpoints = $"{CheckpointHeader}\ncycle,2026-03-31,,17,\n";

        // Act
        var response = await user.Client.PostAsync(ImportRoute, Files(kpis, $"{MeasurementHeader}\n", checkpoints), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("Label", (await ReadBody(response)).GetRawText());
        Assert.Empty(await KpisOn(teamId));
    }

    [Fact]
    public async Task Import_RejectsOnlyTheKpi_WhosePlanDoesNotHold()
    {
        // Arrange — Cycle time is a Decrease KPI, so an at-risk value below plan is on the wrong side
        var teamId = await PrepareTeam();
        var user = await _factory.CreateAuthenticatedClient(_administerPermission);
        var kpis = $"{KpiHeader}\n{KpiRow("cycle", teamId, "Cycle time")}\n{KpiRow("lead", teamId, "Lead time")}\n";
        var checkpoints = $"{CheckpointHeader}\ncycle,2026-03-31,Q1,17,15\nlead,2026-03-31,Q1,17,19\n";

        // Act
        var response = await user.Client.PostAsync(ImportRoute, Files(kpis, $"{MeasurementHeader}\n", checkpoints), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([("Lead time", 0)], await KpisOn(teamId));
    }
}
