using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Imports.Dtos;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
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
    PageRequest Paging,
    ImportRowStatus? Status = null) : IQuery<Result<PagedResponse<ImportProcessRowDto>>>, IPagedQuery;

/// <summary>Bounds the page a caller may ask for.</summary>
public sealed class GetImportProcessRowsQueryValidator : PagedQueryValidator<GetImportProcessRowsQuery>;

public sealed class GetImportProcessRowsQueryHandler(
    IImportDbContext importDbContext,
    IImportDefinitionRegistry registry,
    ICurrentPrincipal currentPrincipal) : IQueryHandler<GetImportProcessRowsQuery, Result<PagedResponse<ImportProcessRowDto>>>
{
    private readonly IImportDbContext _importDbContext = importDbContext;
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<Result<PagedResponse<ImportProcessRowDto>>> Handle(
        GetImportProcessRowsQuery query, CancellationToken cancellationToken)
    {
        var importType = await _importDbContext.ImportProcesses
            .AsNoTracking()
            .Where(p => p.Id == query.ImportProcessId)
            .Select(p => p.ImportType)
            .FirstOrDefaultAsync(cancellationToken);

        if (importType is null)
            return Result.Failure<PagedResponse<ImportProcessRowDto>>($"Import process '{query.ImportProcessId}' was not found.");

        var definition = await ImportAuthorization.ResolveForRead(
            _registry, _currentPrincipal, importType, cancellationToken);

        if (definition.IsFailure)
            return Result.Failure<PagedResponse<ImportProcessRowDto>>(definition.Error);

        var rows = _importDbContext.ImportProcesses
            .AsNoTracking()
            .Where(p => p.Id == query.ImportProcessId)
            .SelectMany(p => p.Rows)
            .Where(r => query.Status == null || r.Status == query.Status);

        var totalCount = await rows.CountAsync(cancellationToken);

        var page = await rows
            .OrderBy(r => r.RowNumber)
            .Skip(query.Paging.Skip)
            .Take(query.Paging.PageSize)
            .Select(r => new ImportProcessRowDto(
                r.Id, r.ImportId, r.RowNumber, r.Status, r.CreatedEntityId, r.Error, r.Warning, r.AttemptedOn))
            .ToListAsync(cancellationToken);

        var createdEntityIds = page
            .Where(r => r.CreatedEntityId is not null)
            .Select(r => r.CreatedEntityId!.Value)
            .Distinct()
            .ToList();

        if (createdEntityIds.Count > 0)
        {
            var links = await definition.Value.DescribeCreatedRecords(createdEntityIds, cancellationToken);

            page = [.. page.Select(r => r.CreatedEntityId is { } id && links.TryGetValue(id, out var link)
                ? r with { CreatedRecord = link }
                : r)];
        }

        return Result.Success(new PagedResponse<ImportProcessRowDto>(page, totalCount, query.Paging.Page, query.Paging.PageSize));
    }
}
