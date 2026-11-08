using Microsoft.EntityFrameworkCore;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Migrators.MSSQL.Migrations;
using Wayd.Work.Domain;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves the Remove-Work-Status-Tables migration keeps each shared work status's description, by name, before it
/// drops the shared statuses.
/// </summary>
/// <remarks>
/// The SQL is hand-written and reads <c>Work.WorkStatuses</c>, which the migration then drops, so each test runs it
/// against a stand-in of that table created and dropped in the same batch.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class RemoveWorkStatusTablesMigrationTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task KeepDescriptions_ShouldKeepEachDescribedStatus_Trimmed()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var review = UniqueName("Review");
        var breaks = UniqueName("Breaks");

        // Act
        await RunAgainst([(review, "  Waiting on a reviewer  ", false), (breaks, "\r\n\tReady for review \r\n", false)], ct);

        // Assert
        (await KeptDescriptions([review, breaks], ct)).Should().BeEquivalentTo(new Dictionary<string, string>
        {
            [review] = "Waiting on a reviewer",
            [breaks] = "Ready for review",
        });
    }

    [Fact]
    public async Task KeepDescriptions_ShouldSkipADeletedUndescribedOrBlankStatus()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var deleted = UniqueName("Deleted");
        var undescribed = UniqueName("Undescribed");
        var blank = UniqueName("Blank");

        // Act
        await RunAgainst([(deleted, "Gone", true), (undescribed, null, false), (blank, "\r\n\t ", false)], ct);

        // Assert
        (await KeptDescriptions([deleted, undescribed, blank], ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task KeepDescriptions_ShouldCutALongDescriptionToWhatAStatusHolds()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var name = UniqueName("Long");

        // Act
        await RunAgainst([(name, new string('x', 511) + " " + new string('y', 400), false)], ct);

        // Assert
        (await KeptDescriptions([name], ct)).Should().Equal(new Dictionary<string, string> { [name] = new string('x', 511) },
            "the cut is trimmed too");
    }

    [Fact]
    public async Task FormerNames_ShouldReadAsNone_ForAStatusThatPredatesTheColumn()
    {
        // Arrange — a status saved before the column existed holds its default
        var ct = TestContext.Current.CancellationToken;
        WorkWorkflowOwners.Register();
        var key = Guid.NewGuid().ToString("N");
        var workflow = StatusWorkflow.CreateManaged($"Former {key}", WorkWorkflowOwners.WorkItem.Key, $"former:{key}",
            [new SourceStatus("s1", "New", StatusCategory.Proposed)], EventActor.System, Instant.FromUtc(2026, 11, 8, 12, 0)).Value;
        await using (var context = _fixture.CreateContext())
        {
            context.StatusWorkflows.Add(workflow);
            await context.SaveChangesAsync(ct);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [StatusWorkflows].[WorkflowStatuses] SET [FormerNames] = DEFAULT WHERE [WorkflowId] = {workflow.Id};", ct);
        }

        // Act
        await using var verify = _fixture.CreateContext();
        var saved = await verify.StatusWorkflows.AsNoTracking().Include(w => w.Statuses).SingleAsync(w => w.Id == workflow.Id, ct);

        // Assert
        saved.Statuses.Single().FormerNames.Should().BeEmpty();
    }

    private static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    /// <summary>
    /// Runs the migration's SQL against a stand-in of Work's shared statuses holding <paramref name="statuses"/>,
    /// shaped as the columns it reads, then drops the stand-in.
    /// </summary>
    private async Task RunAgainst(IReadOnlyList<(string Name, string? Description, bool IsDeleted)> statuses, CancellationToken ct)
    {
        var rows = string.Join(",\n", statuses.Select(s =>
            $"(N'{s.Name}', {(s.Description is null ? "NULL" : $"N'{s.Description.Replace("'", "''")}'")}, {(s.IsDeleted ? 1 : 0)})"));

        await using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync(
            $"""
            CREATE TABLE [Work].[WorkStatuses] ([Name] nvarchar(64) NOT NULL, [Description] nvarchar(1024) NULL, [IsDeleted] bit NOT NULL);
            BEGIN TRY
                INSERT INTO [Work].[WorkStatuses] ([Name], [Description], [IsDeleted]) VALUES {rows};
                {RemoveWorkStatusTables.KeepDescriptionsSql}
                DROP TABLE [Work].[WorkStatuses];
            END TRY
            BEGIN CATCH
                DROP TABLE [Work].[WorkStatuses];
                THROW;
            END CATCH
            """,
            ct);
    }

    private async Task<Dictionary<string, string>> KeptDescriptions(IReadOnlyCollection<string> names, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        return await context.LegacyWorkStatusDescriptions.AsNoTracking()
            .Where(d => names.Contains(d.Name))
            .ToDictionaryAsync(d => d.Name, d => d.Description, ct);
    }
}
