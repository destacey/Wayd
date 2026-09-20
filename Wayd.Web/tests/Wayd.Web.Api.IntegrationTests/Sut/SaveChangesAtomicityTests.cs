using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Infrastructure.Auth;
using Wayd.ProjectPortfolioManagement.Application;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Command;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Proves a save commits its rows together with what records them, or commits nothing.
/// </summary>
/// <remarks>
/// One <c>SaveChangesAsync</c> writes the entities, then the audit trail for anything carrying a
/// database-generated key, then the activity log and outbox envelope rows. Those used to be separate
/// transactions, so a failure after the first left the rows in place with every event they raised silently
/// discarded — no activity entry, no envelope, no dead letter. A real <c>large-tech</c> seed ended with
/// 2,000 projects and 1,500 <c>ProjectCreatedEvent</c> entries and no way to tell which 500 were missing.
/// <para>
/// Only a real provider can show this: the in-memory store has no transactions, so a rollback there proves
/// nothing, and the unit fakes never reach a database at all.
/// </para>
/// </remarks>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class SaveChangesAtomicityTests(WaydSqlServerApiFactory factory)
{
    private const string UserId = "save-atomicity-test";

    private readonly WaydSqlServerApiFactory _factory = factory;

    /// <summary>A name no other test in the shared database can collide with.</summary>
    private static string UniqueName() => $"Atomicity {Guid.NewGuid():N}"[..24];

    [Theory]
    // The audit trail, which is where production failed: the entities are committed by then, the activity
    // row is not yet staged.
    [InlineData(2)]
    // The activity log and outbox envelopes. The one that proves the most, because by then the entities and
    // their audit trail are both written and both have to go back.
    [InlineData(3)]
    public async Task SaveChanges_WritesNoRow_WhenWhatRecordsItFails(int failingSave)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var name = UniqueName();

        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId(UserId);
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        // Act — however the pipeline reports the fault, whether by throwing or by a failed Result, what must
        // not happen is a success: the row and the entry recording it are one fact.
        using (var fault = _factory.SaveFaults.FailOn(failingSave))
        {
            var reportedSuccess = false;

            try
            {
                var created = await dispatcher.Send(
                    new CreateProjectPortfolioCommand(name, "Rolled back by an injected fault.", null, null, null), ct);

                reportedSuccess = created.IsSuccess;
            }
            catch (Exception)
            {
                // The fault reached the caller, which is one of the two acceptable outcomes.
            }

            Assert.True(fault.WasReached, "the fault should have been reached, or the test proves nothing");
            Assert.False(reportedSuccess, "a save whose audit trail failed must not report success");
        }

        // Assert — the portfolio and its activity entry are one fact, so neither may survive alone
        using var reader = _factory.Services.CreateScope();
        var ppm = reader.ServiceProvider.GetRequiredService<IProjectPortfolioManagementDbContext>();
        var activity = reader.ServiceProvider.GetRequiredService<IActivityLogDbContext>();

        var portfolioId = await ppm.Portfolios
            .AsNoTracking()
            .Where(p => p.Name == name)
            .Select(p => (Guid?)p.Id)
            .SingleOrDefaultAsync(ct);

        Assert.Null(portfolioId);

        // Matched on the name inside the payload, not on the id: the row is gone, so there is no id to look
        // an entry up by, and comparing against a null one would match nothing whatever had survived.
        var strayActivity = await activity.ActivityLogs
            .AsNoTracking()
            .CountAsync(
                a => a.EventType == "ProjectPortfolioCreatedEvent" && a.Payload.Contains(name),
                ct);

        Assert.Equal(0, strayActivity);
    }

    [Fact]
    public async Task SaveChanges_WritesTheRowAndItsActivityEntry_Together()
    {
        // Arrange — the guarantee is worth nothing if the ordinary path stopped recording anything
        var ct = TestContext.Current.CancellationToken;
        var name = UniqueName();

        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId(UserId);
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        // Act
        var created = await dispatcher.Send(
            new CreateProjectPortfolioCommand(name, "Committed with its activity entry.", null, null, null), ct);

        // Assert
        Assert.True(created.IsSuccess, created.IsFailure ? created.Error : null);

        using var reader = _factory.Services.CreateScope();
        var ppm = reader.ServiceProvider.GetRequiredService<IProjectPortfolioManagementDbContext>();
        var activity = reader.ServiceProvider.GetRequiredService<IActivityLogDbContext>();

        var exists = await ppm.Portfolios.AsNoTracking().AnyAsync(p => p.Id == created.Value.Id, ct);
        Assert.True(exists);

        var entries = await activity.ActivityLogs
            .AsNoTracking()
            .CountAsync(a => a.AggregateId == created.Value.Id, ct);

        Assert.True(entries > 0, "a committed portfolio should carry the activity its creation raised");
    }
}
