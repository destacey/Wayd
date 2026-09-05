using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Imports.Dtos;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Imports.Queries;

/// <summary>Status and counts for one run — what a caller polls after submitting a file.</summary>
public sealed record GetImportProcessQuery(Guid ImportProcessId) : IQuery<Result<ImportProcessDto>>;

public sealed class GetImportProcessQueryHandler(
    IImportDbContext importDbContext,
    IImportDefinitionRegistry registry,
    ICurrentPrincipal currentPrincipal) : IQueryHandler<GetImportProcessQuery, Result<ImportProcessDto>>
{
    private readonly IImportDbContext _importDbContext = importDbContext;
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<Result<ImportProcessDto>> Handle(GetImportProcessQuery query, CancellationToken cancellationToken)
    {
        var process = await _importDbContext.ImportProcesses
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == query.ImportProcessId, cancellationToken);

        if (process is null)
            return Result.Failure<ImportProcessDto>($"Import process '{query.ImportProcessId}' was not found.");

        var definition = await ImportAuthorization.ResolveFor(
            _registry, _currentPrincipal, process.ImportType, cancellationToken);

        if (definition.IsFailure)
            return Result.Failure<ImportProcessDto>(definition.Error);

        return Result.Success(new ImportProcessDto(
            process.Id,
            process.ImportType,
            definition.Value.DisplayName,
            definition.Value.Atomicity,
            process.Status,
            process.SubmissionGroupId,
            process.SubmittedByUserId,
            process.SubmittedOn,
            process.StartedOn,
            process.CompletedOn,
            process.LastProgressOn,
            process.TotalRowCount,
            process.SucceededRowCount,
            process.FailedRowCount,
            process.Error));
    }
}
