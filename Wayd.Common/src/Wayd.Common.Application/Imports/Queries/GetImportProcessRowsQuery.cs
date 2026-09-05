using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Imports.Dtos;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Enums.Imports;

namespace Wayd.Common.Application.Imports.Queries;

/// <summary>
/// A page of row outcomes for one run, optionally narrowed to one status.
/// </summary>
/// <remarks>
/// Paged rather than whole because a run can hold fifty thousand rows, and the answer a person wants is
/// almost always the failed ones — which the status filter gets them without reading the rest.
/// </remarks>
public sealed record GetImportProcessRowsQuery(
    Guid ImportProcessId,
    ImportRowStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 50) : IQuery<Result<ImportProcessRowPageDto>>;

public sealed class GetImportProcessRowsQueryHandler(
    IImportDbContext importDbContext,
    IImportDefinitionRegistry registry,
    ICurrentPrincipal currentPrincipal) : IQueryHandler<GetImportProcessRowsQuery, Result<ImportProcessRowPageDto>>
{
    private const int MaxPageSize = 500;

    private readonly IImportDbContext _importDbContext = importDbContext;
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<Result<ImportProcessRowPageDto>> Handle(
        GetImportProcessRowsQuery query, CancellationToken cancellationToken)
    {
        var importType = await _importDbContext.ImportProcesses
            .AsNoTracking()
            .Where(p => p.Id == query.ImportProcessId)
            .Select(p => p.ImportType)
            .FirstOrDefaultAsync(cancellationToken);

        if (importType is null)
            return Result.Failure<ImportProcessRowPageDto>($"Import process '{query.ImportProcessId}' was not found.");

        var definition = await ImportAuthorization.ResolveFor(
            _registry, _currentPrincipal, importType, cancellationToken);

        if (definition.IsFailure)
            return Result.Failure<ImportProcessRowPageDto>(definition.Error);

        var pageNumber = Math.Max(query.PageNumber, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var rows = _importDbContext.ImportProcesses
            .AsNoTracking()
            .Where(p => p.Id == query.ImportProcessId)
            .SelectMany(p => p.Rows)
            .Where(r => query.Status == null || r.Status == query.Status);

        var totalCount = await rows.CountAsync(cancellationToken);

        var page = await rows
            .OrderBy(r => r.RowNumber)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ImportProcessRowDto(
                r.Id, r.ImportId, r.RowNumber, r.Status, r.CreatedEntityId, r.Error, r.Warning, r.AttemptedOn))
            .ToListAsync(cancellationToken);

        return Result.Success(new ImportProcessRowPageDto(page, totalCount, pageNumber, pageSize));
    }
}
