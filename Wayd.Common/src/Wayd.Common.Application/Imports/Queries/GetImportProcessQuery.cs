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
    IWaydDbContext waydDbContext,
    IImportDefinitionRegistry registry,
    ICurrentPrincipal currentPrincipal) : IQueryHandler<GetImportProcessQuery, Result<ImportProcessDto>>
{
    private readonly IImportDbContext _importDbContext = importDbContext;
    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly IImportDefinitionRegistry _registry = registry;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<Result<ImportProcessDto>> Handle(GetImportProcessQuery query, CancellationToken cancellationToken)
    {
        var process = await _importDbContext.ImportProcesses
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == query.ImportProcessId, cancellationToken);

        if (process is null)
            return Result.Failure<ImportProcessDto>($"Import process '{query.ImportProcessId}' was not found.");

        var definition = await ImportAuthorization.ResolveForRead(
            _registry, _currentPrincipal, process.ImportType, cancellationToken);

        if (definition.IsFailure)
            return Result.Failure<ImportProcessDto>(definition.Error);

        var submittedByName = await _waydDbContext.WaydUsers
            .AsNoTracking()
            .Where(u => u.Id == process.SubmittedByUserId)
            .Select(u => u.DisplayName ?? u.UserName)
            .FirstOrDefaultAsync(cancellationToken);

        var canManage = await ImportAuthorization.CanSubmit(
            definition.Value, _currentPrincipal, cancellationToken);

        return Result.Success(
            GetImportProcessesQueryHandler.Map(process, definition.Value, submittedByName, canManage));
    }
}
