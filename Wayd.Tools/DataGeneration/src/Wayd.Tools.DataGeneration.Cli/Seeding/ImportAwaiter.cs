using Wayd.Tools.DataGeneration.Cli.Client;

namespace Wayd.Tools.DataGeneration.Cli.Seeding;

/// <summary>
/// Waits for a submitted import to finish, then reads back what each row created.
/// </summary>
/// <remarks>
/// A submission answers <c>202 Accepted</c> with the id of the run, which means the file was taken, not
/// that anything exists yet — the runner picks it up off a queue. A seed depends on each stage finishing
/// before the next writes its file, so this is where that ordering is actually enforced.
/// </remarks>
public sealed class ImportAwaiter(IImportsClient imports, TimeSpan pollInterval, TimeSpan timeout)
{
    private readonly IImportsClient _imports = imports;
    private readonly TimeSpan _pollInterval = pollInterval;
    private readonly TimeSpan _timeout = timeout;

    /// <summary>The API pages row outcomes; a seed wants all of them, so it asks for the largest page.</summary>
    private const int RowPageSize = 500;

    public async Task<ImportRun> Await(Guid processId, string label, CancellationToken cancellationToken)
    {
        var process = await Poll(processId, label, cancellationToken);

        // Abort on anything short of a clean run. A seed builds each stage on the one before it, so a
        // partial result would leave later stages referencing records that were never created — the
        // failure would surface far from its cause.
        if (process.Status != ImportProcessStatus.Succeeded)
            throw new SeedException(await Describe(process, label, cancellationToken));

        return new ImportRun(processId, await ReadCreatedIds(processId, cancellationToken));
    }

    private async Task<ImportProcessDto> Poll(Guid processId, string label, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + _timeout;

        while (true)
        {
            var process = await _imports.GetByIdAsync(processId, cancellationToken);

            if (process.Status is not (ImportProcessStatus.Queued or ImportProcessStatus.Processing
                or ImportProcessStatus.Cancelling))
            {
                return process;
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new SeedException(
                    $"The {label} import ({processId}) was still {process.Status} after {_timeout.TotalSeconds:N0}s. "
                    + "The import runner may not be processing the queue.");
            }

            await Task.Delay(_pollInterval, cancellationToken);
        }
    }

    /// <summary>
    /// Explains a run that did not succeed, naming the rows rather than the count.
    /// </summary>
    /// <remarks>
    /// Failures are reported by import id, which is the generator's own handle for the row — so a message
    /// names the portfolio or project that could not be created rather than a line number in a file the
    /// operator never saw.
    /// </remarks>
    private async Task<string> Describe(ImportProcessDto process, string label, CancellationToken cancellationToken)
    {
        var summary =
            $"The {label} import ({process.Id}) finished as {process.Status}: "
            + $"{process.SucceededRowCount} applied, {process.FailedRowCount} rejected.";

        if (!string.IsNullOrWhiteSpace(process.Error))
            summary += $" {process.Error}";

        if (process.FailedRowCount == 0)
            return summary;

        var failed = await _imports.GetRowsAsync(
            process.Id, ImportRowStatus.Failed, pageNumber: 1, pageSize: 10, cancellationToken);

        var lines = failed.Rows.Select(r => $"  {r.ImportId}: {r.Error}");
        var more = process.FailedRowCount > failed.Rows.Count
            ? $"{Environment.NewLine}  … and {process.FailedRowCount - failed.Rows.Count} more."
            : string.Empty;

        return $"{summary}{Environment.NewLine}{string.Join(Environment.NewLine, lines)}{more}";
    }

    private async Task<IReadOnlyDictionary<string, Guid>> ReadCreatedIds(Guid processId, CancellationToken cancellationToken)
    {
        Dictionary<string, Guid> created = new(StringComparer.OrdinalIgnoreCase);

        var page = 1;
        while (true)
        {
            var rows = await _imports.GetRowsAsync(processId, null, page, RowPageSize, cancellationToken);

            foreach (var row in rows.Rows.Where(r => r.CreatedEntityId is not null))
                created[row.ImportId] = row.CreatedEntityId!.Value;

            if (rows.Rows.Count == 0 || page * RowPageSize >= rows.TotalCount)
                return created;

            page++;
        }
    }
}
