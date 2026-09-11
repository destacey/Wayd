using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Proves an import run's status is checked on every write, so two requests acting on the same run cannot
/// both win.
/// </summary>
/// <remarks>
/// The runner, a person stopping or resuming the run, and the stall sweep all write the status from
/// different requests. The unit fakes never conflict, so only a real provider shows the check is there.
/// </remarks>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class ImportProcessConcurrencyTests(WaydSqlServerApiFactory factory)
{
    private static readonly Instant _now = Instant.FromUtc(2026, 9, 11, 9, 0);

    private readonly WaydSqlServerApiFactory _factory = factory;

    private async Task<Guid> SaveRun(bool claimed)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IImportDbContext>();

        var process = ImportProcess.Create(
            "concurrency-test", "import-concurrency-test", null, [ImportProcessRow.Create("r1", 1, "{}")], _now);
        if (claimed)
            process.Start("trace-1", _now);

        await db.ImportProcesses.AddAsync(process, TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return process.Id;
    }

    private static Task<ImportProcess> Load(IImportDbContext db, Guid id) =>
        db.ImportProcesses.SingleAsync(p => p.Id == id, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Release_CannotOverwriteAStopThatLandedAfterTheRunWasRead()
    {
        // Arrange — the worker reads the failed run, then the stop commits
        _ = _factory.CreateClient();
        var id = await SaveRun(claimed: true);

        using var worker = _factory.Services.CreateScope();
        var workerDb = worker.ServiceProvider.GetRequiredService<IImportDbContext>();
        var workerView = await Load(workerDb, id);

        using (var person = _factory.Services.CreateScope())
        {
            var personDb = person.ServiceProvider.GetRequiredService<IImportDbContext>();
            (await Load(personDb, id)).RequestCancellation(_now);
            await personDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        workerView.Release(_now);

        // Act
        var act = () => workerDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert — refused, and the stop stands
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(act);

        using var check = _factory.Services.CreateScope();
        var status = await check.ServiceProvider.GetRequiredService<IImportDbContext>().ImportProcesses
            .AsNoTracking().Where(p => p.Id == id).Select(p => p.Status).SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ImportProcessStatus.Cancelling, status);
    }

    [Fact]
    public async Task Start_LetsOnlyOneOfTwoDeliveriesClaimTheRun()
    {
        // Arrange — both deliveries read the run while it is still queued
        _ = _factory.CreateClient();
        var id = await SaveRun(claimed: false);

        using var first = _factory.Services.CreateScope();
        using var second = _factory.Services.CreateScope();
        var firstDb = first.ServiceProvider.GetRequiredService<IImportDbContext>();
        var secondDb = second.ServiceProvider.GetRequiredService<IImportDbContext>();

        (await Load(firstDb, id)).Start("trace-first", _now);
        (await Load(secondDb, id)).Start("trace-second", _now);
        await firstDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var act = () => secondDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(act);
    }
}
