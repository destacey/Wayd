using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Imports.Commands;

/// <summary>
/// Submits the file a preflight checked, this time for real, and answers with the id of the new run.
/// </summary>
/// <remarks>
/// Built from the rows the preflight stored rather than a second upload, so what is applied is exactly what
/// was checked. The module's own import command is not run again: it only checks the shape of the file, and
/// these rows already passed it.
/// <para>
/// Every row is submitted, including the ones the preflight rejected — the same as uploading the file again.
/// The data may have changed since, so the new run decides for itself.
/// </para>
/// </remarks>
public sealed record ApplyImportPreflightCommand(Guid ImportProcessId, Guid? SubmissionGroupId = null) : ICommand<Guid>;

public sealed class ApplyImportPreflightCommandHandler(
    IImportDbContext importDbContext,
    IImportDefinitionRegistry registry,
    ICurrentPrincipal currentPrincipal,
    IDispatcher dispatcher) : ICommandHandler<ApplyImportPreflightCommand, Guid>
{
    private readonly IImportDbContext _importDbContext = importDbContext;
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<Result<Guid>> Handle(ApplyImportPreflightCommand command, CancellationToken cancellationToken)
    {
        var preflight = await _importDbContext.ImportProcesses
            .FirstOrDefaultAsync(p => p.Id == command.ImportProcessId, cancellationToken);

        if (preflight is null)
            return Result.Failure<Guid>($"Import process '{command.ImportProcessId}' was not found.");

        // The submit permission, not oversight: applying creates records.
        var definition = await ImportAuthorization.ResolveForManage(
            _registry, _currentPrincipal, preflight.ImportType, cancellationToken);

        if (definition.IsFailure)
            return Result.Failure<Guid>(definition.Error);

        if (!preflight.IsPreflight)
            return Result.Failure<Guid>("Only a preflight can be applied. This import has already run for real.");

        if (!preflight.IsTerminal)
            return Result.Failure<Guid>($"This preflight is {preflight.Status}; wait for it to finish before applying it.");

        var rows = await _importDbContext.ImportProcesses
            .Where(p => p.Id == command.ImportProcessId)
            .SelectMany(p => p.Rows)
            .OrderBy(r => r.RowNumber)
            .Select(r => new { r.ImportId, r.Payload })
            .ToListAsync(cancellationToken);

        if (rows.Any(r => r.Payload is null))
            return Result.Failure<Guid>("This preflight has passed its retention window and its rows are no longer stored. Submit the file again.");

        var submitted = await _dispatcher.Send(
            new SubmitImportCommand(
                preflight.ImportType,
                [.. rows.Select(r => new SubmittedImportRow(r.ImportId, r.Payload!))],
                command.SubmissionGroupId),
            cancellationToken);

        if (submitted.IsFailure)
            return submitted;

        // Recorded after the run exists, so the link never points at a run that was refused. The checks
        // above are the ones RecordApplied repeats, and a finished run cannot stop being finished.
        preflight.RecordApplied(submitted.Value);
        await _importDbContext.SaveChangesAsync(cancellationToken);

        return submitted;
    }
}
