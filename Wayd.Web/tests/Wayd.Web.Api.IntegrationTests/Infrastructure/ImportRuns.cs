using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Imports;

namespace Wayd.Web.Api.IntegrationTests.Infrastructure;

/// <summary>Submits an import's command and waits for the queued run to finish.</summary>
public static class ImportRuns
{
    private static readonly TimeSpan RunTimeout = TimeSpan.FromSeconds(120);

    public static async Task<ImportProcess> SubmitAndWait(IServiceScope scope, ICommand<Guid> command) =>
        await WaitFor(scope.ServiceProvider, await Submit(scope, command));

    /// <summary>Submits an import's command, asserting it was accepted, and answers with the run's id.</summary>
    public static async Task<Guid> Submit(IServiceScope scope, ICommand<Guid> command)
    {
        var submitted = await scope.ServiceProvider.GetRequiredService<IDispatcher>()
            .Send(command, TestContext.Current.CancellationToken);

        Assert.True(submitted.IsSuccess, submitted.IsFailure ? submitted.Error : null);
        return submitted.Value;
    }

    /// <summary>Polls a run until it reaches a terminal status, and answers with it and its rows.</summary>
    /// <remarks>Each poll reads through a scope of its own, so no change tracker holds on to an earlier read.</remarks>
    public static async Task<ImportProcess> WaitFor(IServiceProvider services, Guid runId)
    {
        var ct = TestContext.Current.CancellationToken;
        var deadline = DateTime.UtcNow + RunTimeout;
        ImportProcess? run = null;

        while (DateTime.UtcNow < deadline)
        {
            using var scope = services.CreateScope();

            run = await scope.ServiceProvider.GetRequiredService<IImportDbContext>().ImportProcesses
                .AsNoTracking()
                .Include(p => p.Rows)
                .SingleAsync(p => p.Id == runId, ct);

            if (run.IsTerminal)
                return run;

            await Task.Delay(200, ct);
        }

        throw new TimeoutException($"Import {runId} was still {run?.Status} after {RunTimeout}.");
    }
}
