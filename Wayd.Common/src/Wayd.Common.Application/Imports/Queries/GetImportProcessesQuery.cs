using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Imports.Dtos;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;

namespace Wayd.Common.Application.Imports.Queries;

/// <summary>
/// A page of import runs, newest first, narrowed to the types this caller may see.
/// </summary>
public sealed record GetImportProcessesQuery(
    PageRequest Paging,
    ImportProcessStatus? Status = null,
    string? ImportType = null,
    string? SubmittedByUserId = null,
    Guid? SubmissionGroupId = null) : IQuery<Result<PagedResponse<ImportProcessDto>>>, IPagedQuery;

/// <summary>Bounds the page a caller may ask for.</summary>
public sealed class GetImportProcessesQueryValidator : PagedQueryValidator<GetImportProcessesQuery>;

public sealed class GetImportProcessesQueryHandler(
    IImportDbContext importDbContext,
    IWaydDbContext waydDbContext,
    IImportDefinitionRegistry registry,
    ICurrentPrincipal currentPrincipal) : IQueryHandler<GetImportProcessesQuery, Result<PagedResponse<ImportProcessDto>>>
{
    private readonly IImportDbContext _importDbContext = importDbContext;
    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<Result<PagedResponse<ImportProcessDto>>> Handle(
        GetImportProcessesQuery query, CancellationToken cancellationToken)
    {
        var paging = query.Paging;

        var viewable = await ImportAuthorization.Viewable(_registry, _currentPrincipal, cancellationToken);

        if (query.ImportType is not null)
        {
            viewable = [.. viewable.Where(d => string.Equals(d.Key, query.ImportType, StringComparison.OrdinalIgnoreCase))];

            // An unknown type and one the caller cannot see are the same answer on purpose: telling them
            // apart would report which import types exist to someone not allowed to know.
            if (viewable.Count == 0)
                return Result.Failure<PagedResponse<ImportProcessDto>>($"No import type '{query.ImportType}' is available to you.");
        }

        var byKey = viewable.ToDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);

        // Read once for the whole page rather than per run: oversight widens what is listed, but acting
        // on a run still needs that import's own permission, and the UI has to know which is which.
        var manageable = await ImportAuthorization.Submittable(_registry, _currentPrincipal, cancellationToken);
        var manageableKeys = manageable.Select(d => d.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (byKey.Count == 0)
            return Result.Success(new PagedResponse<ImportProcessDto>([], 0, paging.Page, paging.PageSize));

        var keys = byKey.Keys.ToList();

        var runs = _importDbContext.ImportProcesses
            .AsNoTracking()
            .Where(p => keys.Contains(p.ImportType))
            .Where(p => query.Status == null || p.Status == query.Status)
            .Where(p => query.SubmittedByUserId == null || p.SubmittedByUserId == query.SubmittedByUserId)
            .Where(p => query.SubmissionGroupId == null || p.SubmissionGroupId == query.SubmissionGroupId);

        var totalCount = await runs.CountAsync(cancellationToken);

        var page = await runs
            .OrderByDescending(p => p.SubmittedOn)
            .ThenByDescending(p => p.Id)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken);

        var names = await ResolveSubmitterNames(page, cancellationToken);

        return Result.Success(new PagedResponse<ImportProcessDto>(
            [.. page.Select(p => Map(
                p,
                byKey[p.ImportType],
                names.GetValueOrDefault(p.SubmittedByUserId),
                manageableKeys.Contains(p.ImportType)))],
            totalCount,
            paging.Page,
            paging.PageSize));
    }

    /// <summary>
    /// Looks up only the submitters on this page. The column would otherwise show a raw account id, which
    /// nobody can act on.
    /// </summary>
    private async Task<Dictionary<string, string>> ResolveSubmitterNames(
        List<ImportProcess> page, CancellationToken cancellationToken)
    {
        var userIds = page.Select(p => p.SubmittedByUserId).Distinct().ToList();

        var users = await _waydDbContext.WaydUsers
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.UserName })
            .ToListAsync(cancellationToken);

        return users.ToDictionary(u => u.Id, u => u.DisplayName ?? u.UserName, StringComparer.Ordinal);
    }

    internal static ImportProcessDto Map(
        ImportProcess process, IImportDefinition definition, string? submittedByName, bool canManage) =>
        new(process.Id,
            process.ImportType,
            definition.DisplayName,
            definition.Atomicity,
            definition.GroupNoun,
            process.Status,
            process.IsPreflight,
            process.AppliedImportProcessId,
            process.SubmissionGroupId,
            process.SubmittedByUserId,
            submittedByName,
            process.SubmittedOn,
            process.StartedOn,
            process.CompletedOn,
            process.LastProgressOn,
            process.TotalRowCount,
            process.SucceededRowCount,
            process.FailedRowCount,
            process.Error,
            canManage);
}
