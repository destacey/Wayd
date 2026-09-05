using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;

namespace Wayd.Common.Application.Imports.Commands;

/// <summary>
/// Stops a run that is still going. Rows already applied stay applied.
/// </summary>
/// <remarks>
/// A run in progress is only asked to stop: this request runs on a different connection than the worker, so
/// it cannot end a run mid-chunk without leaving records written against rows still marked Pending. The
/// worker reads the request at its next chunk boundary and finishes the job itself.
/// </remarks>
public sealed record CancelImportProcessCommand(Guid ImportProcessId) : ICommand;

public sealed class CancelImportProcessCommandHandler(
    IImportDbContext importDbContext,
    IImportDefinitionRegistry registry,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider,
    ILogger<CancelImportProcessCommandHandler> logger) : ICommandHandler<CancelImportProcessCommand>
{
    private readonly IImportDbContext _importDbContext = importDbContext;
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<CancelImportProcessCommandHandler> _logger = logger;

    public async Task<Result> Handle(CancelImportProcessCommand command, CancellationToken cancellationToken)
    {
        var process = await _importDbContext.ImportProcesses
            .Include(p => p.Rows)
            .FirstOrDefaultAsync(p => p.Id == command.ImportProcessId, cancellationToken);

        if (process is null)
            return Result.Failure($"Import process '{command.ImportProcessId}' was not found.");

        var definition = await ImportAuthorization.ResolveFor(
            _registry, _currentPrincipal, process.ImportType, cancellationToken);

        if (definition.IsFailure)
            return Result.Failure(definition.Error);

        var now = _dateTimeProvider.Now;

        // A run no worker has claimed has nobody to act on a request, and asking would strand it in
        // Cancelling. Nothing has been applied either, so it can be ended here and now.
        var result = process.Status == ImportProcessStatus.Queued
            ? CancelOutright(process, now)
            : process.RequestCancellation(now);

        if (result.IsFailure)
            return result;

        await _importDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Import {ImportProcessId} is now {Status} at the caller's request.", process.Id, process.Status);

        return Result.Success();
    }

    private static Result CancelOutright(ImportProcess process, Instant now)
    {
        foreach (var row in process.Rows)
        {
            row.MarkCancelled(now);
        }

        return process.Cancel(now);
    }
}
