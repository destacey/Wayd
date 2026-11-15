using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Imports.Queries;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Organization.Domain.Models;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Imports;

/// <summary>
/// Runs the import rows field map, the first list sorted and filtered on the server, against SQL Server.
/// </summary>
/// <remarks>
/// EF Core throws on a predicate it cannot translate rather than evaluating it in memory, so a filter that
/// runs here runs in SQL. The unit tests prove what each operator matches over in-memory rows, where a text
/// operator fails on a null value and an enum stored as text sorts by number; neither is true here.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class ImportRowListTranslationIntegrationTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Start = Instant.FromUtc(2026, 3, 1, 0, 0);

    private static readonly ListFieldMap<ImportProcessRow> Fields = GetImportProcessRowsQuery.Fields;

    /// <summary>
    /// Twelve rows: lines 1–4 rejected (line 3 with a `%` in its reason), 5–8 applied an hour apart from the
    /// start, 9–12 never attempted.
    /// </summary>
    private async Task<Guid> SeedRun(CancellationToken cancellationToken)
    {
        await fixture.ResetImportData(cancellationToken);
        await using var context = fixture.CreateContext();

        var rows = Enumerable.Range(1, 12).Select(i => ImportProcessRow.Create($"r{i:00}", i, "{}"));
        var process = ImportProcess.Create("test-import", "user-1", null, rows, Start);
        process.Start("trace-1", Start);

        foreach (var row in process.Rows)
        {
            if (row.RowNumber <= 4)
                row.MarkFailed(row.RowNumber == 3 ? "Over 100% of budget." : $"Duplicate key {row.RowNumber}.", Start);
            else if (row.RowNumber <= 8)
                row.MarkSucceeded(Guid.CreateVersion7(), Start.Plus(Duration.FromHours(row.RowNumber - 5)));
        }

        await context.ImportProcesses.AddAsync(process, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return process.Id;
    }

    private static IQueryable<ImportProcessRow> Rows(WaydDbContext context, Guid id) =>
        context.ImportProcesses.AsNoTracking().Where(p => p.Id == id).SelectMany(p => p.Rows);

    private static async Task<List<int>> Lines(WaydDbContext context, Guid id, string? filter, string? sort = null) =>
        await Fields.ApplySort(Fields.ApplyFilter(Rows(context, id), filter), sort)
            .Select(r => r.RowNumber)
            .ToListAsync(TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("error=*duplicate", new[] { 1, 2, 4 })]
    [InlineData("error!*duplicate", new[] { 3, 5, 6, 7, 8, 9, 10, 11, 12 })]
    [InlineData("error^over", new[] { 3 })]
    [InlineData("error$4.", new[] { 4 })]
    [InlineData("error=", new[] { 5, 6, 7, 8, 9, 10, 11, 12 })]
    [InlineData("error!=", new[] { 1, 2, 3, 4 })]
    [InlineData("error=*%", new[] { 3 })]
    [InlineData("(status=Failed|status=Pending),rowNumber>=4", new[] { 4, 9, 10, 11, 12 })]
    [InlineData("(rowNumber>=2,rowNumber<=3)|importId=r12", new[] { 2, 3, 12 })]
    [InlineData("(attemptedOn>=2026-03-01T01:00:00Z,attemptedOn<2026-03-01T03:00:00.000Z)", new[] { 6, 7 })]
    [InlineData("attemptedOn=", new[] { 9, 10, 11, 12 })]
    public async Task ApplyFilter_TranslatesEveryOperatorTheGridSends(string filter, int[] expected)
    {
        // Arrange
        var id = await SeedRun(TestContext.Current.CancellationToken);
        await using var context = fixture.CreateContext();

        // Act
        var lines = await Lines(context, id, filter);

        // Assert
        lines.Should().BeEquivalentTo(expected);
    }

    /// <summary>
    /// Shapes the grid never sends but the validator accepts, since it runs a filter in memory: each must
    /// still translate, or a caller's request fails as a 500 rather than a 422.
    /// </summary>
    [Theory]
    [InlineData("rowNumber=*1", new[] { 1, 10, 11, 12 })]
    [InlineData("importId>r10", new[] { 11, 12 })]
    [InlineData("error<e", new[] { 1, 2, 4 })]
    [InlineData("status=", new int[0])]
    [InlineData("status!=", new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 })]
    public async Task ApplyFilter_TranslatesShapesOnlyAnApiCallerSends(string filter, int[] expected)
    {
        // Arrange
        var id = await SeedRun(TestContext.Current.CancellationToken);
        await using var context = fixture.CreateContext();

        // Act
        var lines = await Lines(context, id, filter);

        // Assert
        lines.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task ApplyFilter_SendsValuesAsParameters()
    {
        // Arrange
        await using var context = fixture.CreateContext();

        // Act
        var sql = Fields.ApplyFilter(Rows(context, Guid.NewGuid()), "error=*vendor").ToQueryString();

        // Assert — a literal would give each value its own plan, and leave a typed `%` as a wildcard
        sql.Should().Contain("LIKE @").And.NotContain("LIKE N'");
    }

    [Fact]
    public async Task ApplySort_SortsAStatusByItsStoredCode_ThenByTheRequestedFields()
    {
        // Arrange
        var id = await SeedRun(TestContext.Current.CancellationToken);
        await using var context = fixture.CreateContext();

        // Act — the status column holds the code, so it sorts Failed, Pending, Succeeded
        var lines = await Lines(context, id, null, "status asc, rowNumber desc");

        // Assert
        lines.Should().Equal(4, 3, 2, 1, 12, 11, 10, 9, 8, 7, 6, 5);
    }

    [Fact]
    public async Task ApplySort_PagesATiedSortWithoutRepeatingOrMissingARow()
    {
        // Arrange — every page boundary falls inside a run of tied statuses
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = await SeedRun(cancellationToken);
        await using var context = fixture.CreateContext();
        var sorted = Fields.ApplySort(Rows(context, id), "status desc");

        // Act
        var seen = new List<Guid>();
        for (var page = 1; page <= 5; page++)
        {
            var request = new PageRequest(page, 3);
            seen.AddRange(await sorted.Skip(request.Skip).Take(request.PageSize).Select(r => r.Id).ToListAsync(cancellationToken));
        }

        // Assert
        seen.Should().OnlyHaveUniqueItems().And.HaveCount(12);
    }

    [Fact]
    public async Task ApplyFilter_TranslatesALocalDateRange()
    {
        // Arrange — import rows hold no calendar date, so a holiday's stands in for one
        await using var context = fixture.CreateContext();
        var holidays = ListFieldMap<Holiday>.Create("date", h => h.Id).Map("date", h => h.Date);
        var query = holidays.ApplySort(holidays.ApplyFilter(context.Set<Holiday>(), "(date>=2026-03-01,date<2026-03-02)|date="), null);

        // Act
        var act = () => query.ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
        query.ToQueryString().Should().Contain("[h].[Date] >= @");
    }
}
