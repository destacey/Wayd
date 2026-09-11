using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.Common.Domain.Imports;
using Wayd.Infrastructure.Auth;
using Wayd.StrategicManagement.Application.StrategicThemes.Commands;
using Wayd.StrategicManagement.Application.StrategicThemes.Dtos;
using Wayd.Web.Api.IntegrationTests.Infrastructure;
using Wayd.Web.Api.Services;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Proves a small import submitted through the real pipeline is finished by the queue inside the time an
/// import endpoint waits before answering.
/// </summary>
/// <remarks>
/// Every run is queued, and the endpoint waits on it after the submission handler returns. Unit tests can
/// show the waiting; only the booted host shows that the message is actually released once the handler
/// completes and that a worker picks it up quickly enough for the wait to be worth having.
/// </remarks>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class ImportSubmissionTests(WaydSqlServerApiFactory factory)
{
    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public async Task Submit_SmallFile_IsAppliedByTheQueueWithinTheResponseWait()
    {
        // Arrange
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId("import-submission-test");
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var importDbContext = scope.ServiceProvider.GetRequiredService<IImportDbContext>();
        var budget = scope.ServiceProvider.GetRequiredService<ImportResponseTiming>().Budget;

        var name = $"Theme {Guid.NewGuid():N}"[..24];
        var rows = new[]
        {
            new SubmittedImportRow<ImportStrategicThemeDto>(
                "t1", new ImportStrategicThemeDto(name, "Submitted by the import submission test.", StrategicThemeState.Active)),
        };

        // Act
        var submitted = await dispatcher.Send(new ImportStrategicThemesCommand(rows), TestContext.Current.CancellationToken);
        Assert.True(submitted.IsSuccess, submitted.IsFailure ? submitted.Error : null);

        var elapsed = Stopwatch.StartNew();
        var status = ImportProcessStatus.Queued;
        while (!ImportProcess.IsTerminalStatus(status) && elapsed.Elapsed < budget)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
            status = await importDbContext.ImportProcesses
                .AsNoTracking()
                .Where(p => p.Id == submitted.Value)
                .Select(p => p.Status)
                .SingleAsync(TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Equal(ImportProcessStatus.Succeeded, status);
    }
}
