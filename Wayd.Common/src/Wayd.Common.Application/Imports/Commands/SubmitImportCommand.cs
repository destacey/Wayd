using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Imports;

namespace Wayd.Common.Application.Imports.Commands;

/// <summary>One parsed row on its way in: the caller's key for it, and its serialized data.</summary>
/// <param name="ImportId">
/// The caller's own key for this row. Any value they like — a row number, an employee number, a key from
/// the system the file came from — provided it is unique within this one file. Nothing compares it across
/// imports, and nothing writes it onto the records the import creates — it identifies a row of the file,
/// not a thing in the domain.
/// <para>
/// Uniqueness is <em>case-insensitive</em>, matching the collation of the unique index behind it: a file
/// carrying both <c>abc</c> and <c>ABC</c> is rejected. Checking it any more strictly here would let the
/// pair past validation and into a constraint violation at save.
/// </para>
/// <para>Falls back to the row's position when absent, so a hand-authored file works without the column.</para>
/// </param>
/// <param name="Payload">The row, serialized by the definition that will apply it.</param>
public sealed record SubmittedImportRow(string? ImportId, string Payload);

/// <summary>
/// The same row before the definition has serialized it: the caller's key, and the parsed data.
/// </summary>
/// <remarks>
/// What an import's own submission command carries, so a controller hands over typed rows and never has
/// to know the definition or how a payload is stored. The application layer turns these into the
/// serialized <see cref="SubmittedImportRow"/> the run persists.
/// </remarks>
/// <typeparam name="TRow">The row type the definition applies.</typeparam>
public sealed record SubmittedImportRow<TRow>(string? ImportId, TRow Data);

/// <summary>
/// Accepts a parsed file and either applies it now or queues it.
/// </summary>
/// <remarks>
/// Small files run inline so a person importing forty rows gets their answer in the response rather than a
/// job, a poll and a page. Either way the run is recorded and the caller gets an id, so there is one
/// contract and no branch in the client — the only difference is whether the status is already terminal.
/// </remarks>
public sealed record SubmitImportCommand(
    string ImportType,
    IReadOnlyList<SubmittedImportRow> Rows,
    Guid? SubmissionGroupId = null) : ICommand<Guid>;

public sealed class SubmitImportCommandHandler(
    IImportDbContext importDbContext,
    IImportDefinitionRegistry registry,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    IDispatcher dispatcher,
    ILogger<SubmitImportCommandHandler> logger) : ICommandHandler<SubmitImportCommand, Guid>
{
    private readonly IImportDbContext _importDbContext = importDbContext;
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ILogger<SubmitImportCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(SubmitImportCommand command, CancellationToken cancellationToken)
    {
        var definitionResult = _registry.Find(command.ImportType);
        if (definitionResult.IsFailure)
            return Result.Failure<Guid>(definitionResult.Error);

        var definition = definitionResult.Value;

        if (command.Rows.Count == 0)
            return Result.Failure<Guid>("The file contains no rows.");

        // Rejected before anything is persisted, so an absurd file never reaches the queue at all.
        if (command.Rows.Count > definition.MaxRows)
            return Result.Failure<Guid>(
                $"This file has {command.Rows.Count:N0} rows; {definition.DisplayName} accepts at most {definition.MaxRows:N0} at a time.");

        var rows = BuildRows(command.Rows);

        // Caught here rather than at SaveChanges, where the storage bound would surface as a 500 naming a
        // column instead of the row the caller has to fix.
        var overlong = rows.Find(r => r.ImportId.Length > ImportProcessRow.MaxImportIdLength);
        if (overlong is not null)
            return Result.Failure<Guid>(
                $"Row {overlong.RowNumber} has an import id of {overlong.ImportId.Length} characters; the most allowed is {ImportProcessRow.MaxImportIdLength}.");

        var duplicate = rows.GroupBy(r => r.ImportId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicate is not null)
            return Result.Failure<Guid>($"The import id '{duplicate.Key}' appears on more than one row; each must be unique within a file.");

        var process = ImportProcess.Create(
            definition.Key, _currentUser.GetUserId(), command.SubmissionGroupId, rows, _dateTimeProvider.Now);

        await _importDbContext.ImportProcesses.AddAsync(process, cancellationToken);
        await _importDbContext.SaveChangesAsync(cancellationToken);

        var run = new RunImportProcessCommand(process.Id);

        if (rows.Count <= definition.InlineThreshold)
        {
            // Small enough to answer in the request. Any failure is already recorded on the run, so the
            // result is discarded here rather than turned into an error the caller cannot act on.
            await _dispatcher.Send(run, cancellationToken);
        }
        else
        {
            // Passed explicitly even though the caller is the submitter here, so every path that queues a
            // run attributes it the same way rather than two of them relying on ambient identity.
            await _dispatcher.Publish(run, process.SubmittedByUserId, cancellationToken);
            _logger.LogInformation(
                "Queued import {ImportProcessId} ({ImportType}, {RowCount} rows).", process.Id, definition.Key, rows.Count);
        }

        return Result.Success(process.Id);
    }

    /// <summary>
    /// Falls back to the row's position when the caller supplied no key, so a hand-authored file works
    /// without one while a tool still gets to choose its own.
    /// </summary>
    private static List<ImportProcessRow> BuildRows(IReadOnlyList<SubmittedImportRow> submitted) =>
        [.. submitted.Select((row, index) =>
            ImportProcessRow.Create(
                string.IsNullOrWhiteSpace(row.ImportId) ? (index + 1).ToString() : row.ImportId.Trim(),
                index + 1,
                row.Payload))];
}
