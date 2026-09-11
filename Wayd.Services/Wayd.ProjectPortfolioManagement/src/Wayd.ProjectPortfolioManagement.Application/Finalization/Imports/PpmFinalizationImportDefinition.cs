using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Imports;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.ProjectPortfolioManagement.Application.Finalization.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Finalization.Imports;

/// <summary>
/// Closes out programs and portfolios once their contents have been imported — the last step of a PPM
/// import, and the only one that is not additive.
/// </summary>
/// <remarks>
/// It exists because the domain's guards run in opposite directions: things can only be added to an
/// <i>active</i> program or portfolio, but one can only be closed when everything inside it is already
/// closed. Historical work is therefore imported active and finished here.
/// <para>
/// <see cref="ImportPassScope.WholeSet"/> because rows are not independent: program rows are applied
/// before portfolio rows whatever order the file lists them in, since a portfolio cannot close while one
/// of its programs is still open. Atomic for the same reason — a file that half applies leaves the
/// portfolio open with some of its programs closed, and no record of which.
/// </para>
/// </remarks>
public sealed class PpmFinalizationImportDefinition(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    IDateTimeProvider dateTimeProvider,
    IImportPayloadSerializer serializer) : ImportDefinition<FinalizePpmItemDto>(serializer)
{
    public const string ImportKey = "ppm.finalizations";

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public override string Key => ImportKey;
    public override string DisplayName => "PPM Finalizations";
    public override string PermissionAction => ApplicationAction.Import;
    public override string PermissionResource => ApplicationResource.ProjectPortfolios;

    public override ImportAtomicity Atomicity => ImportAtomicity.Atomic;

    // An atomic import cannot be split, so the row cap is what actually bounds one run.
    public override int MaxRows => 10_000;

    protected override IReadOnlyList<ImportPass<FinalizePpmItemDto>> Steps =>
    [
        new("Finalize", ImportPassScope.WholeSet, Finalize),
    ];

    private async Task<Result> Finalize(ImportPassContext<FinalizePpmItemDto> context, CancellationToken cancellationToken)
    {
        var timestamp = _dateTimeProvider.Now;

        var portfoliosById = await ResolvePortfolios(context, cancellationToken);
        var programsById = portfoliosById.Values
            .SelectMany(p => p.Programs)
            .ToDictionary(p => p.Id, p => p);

        // Programs first: a portfolio cannot close while one of its programs is still open, so the order
        // rows happen to appear in must not decide the outcome.
        foreach (var row in context.Accepted.Where(r => r.Data.Type is FinalizePpmItemType.Program).ToList())
        {
            if (!programsById.TryGetValue(row.Data.Id, out var program))
            {
                row.Failed($"No program was found with id '{row.Data.Id}'.");
                continue;
            }

            var result = FinalizeProgram(program, row.Data, timestamp);
            if (result.IsFailure)
            {
                row.Failed(result.Error);
                continue;
            }

            row.Created(program.Id);
        }

        foreach (var row in context.Accepted.Where(r => r.Data.Type is FinalizePpmItemType.Portfolio).ToList())
        {
            if (!portfoliosById.TryGetValue(row.Data.Id, out var portfolio))
            {
                row.Failed($"No portfolio was found with id '{row.Data.Id}'.");
                continue;
            }

            var result = FinalizePortfolio(portfolio, row.Data, timestamp);
            if (result.IsFailure)
            {
                row.Failed(result.Error);
                continue;
            }

            row.Created(portfolio.Id);
        }

        return Result.Success();
    }

    /// <remarks>
    /// Runs as <see cref="PpmActor.System"/>: finalization is a bulk administrative operation authorized
    /// by the caller's Permissions.ProjectPortfolios.Import claim, not by delivery-leadership membership.
    /// </remarks>
    private static Result FinalizeProgram(Program program, FinalizePpmItemDto data, Instant timestamp)
    {
        var result = data.Status is FinalizePpmItemStatus.Canceled
            ? program.Cancel(PpmActor.System, ProgramAncestryRoles.None, timestamp)
            : program.Complete(PpmActor.System, ProgramAncestryRoles.None, timestamp);

        return result.IsFailure
            ? Result.Failure($"Could not finalize program '{program.Name}' as {data.Status}: {result.Error}")
            : Result.Success();
    }

    /// <remarks>Runs as <see cref="PpmActor.System"/> — see <see cref="FinalizeProgram"/>.</remarks>
    private static Result FinalizePortfolio(ProjectPortfolio portfolio, FinalizePpmItemDto data, Instant timestamp)
    {
        var close = portfolio.Close(PpmActor.System, data.EndDate!.Value, timestamp);
        if (close.IsFailure)
            return Result.Failure($"Could not close portfolio '{portfolio.Name}': {close.Error}");

        if (data.Status is not FinalizePpmItemStatus.Archived)
            return Result.Success();

        var archive = portfolio.Archive(PpmActor.System, timestamp);

        return archive.IsFailure
            ? Result.Failure($"Could not archive portfolio '{portfolio.Name}': {archive.Error}")
            : Result.Success();
    }

    /// <summary>
    /// Loads every portfolio the file touches with the programs and projects the closing guards read —
    /// whether the portfolio is being closed itself or merely owns a program that is.
    /// </summary>
    /// <remarks>
    /// A program row names only the program, so the portfolios are found through the programs rather than
    /// the other way round; both lookups are folded into one query over the same set.
    /// </remarks>
    private async Task<Dictionary<Guid, ProjectPortfolio>> ResolvePortfolios(
        ImportPassContext<FinalizePpmItemDto> context, CancellationToken cancellationToken)
    {
        var portfolioIds = context.Rows
            .Where(r => r.Data.Type is FinalizePpmItemType.Portfolio)
            .Select(r => r.Data.Id)
            .ToList();

        var programIds = context.Rows
            .Where(r => r.Data.Type is FinalizePpmItemType.Program)
            .Select(r => r.Data.Id)
            .ToList();

        return await _projectPortfolioManagementDbContext.Portfolios
            .Include(p => p.Programs)
                .ThenInclude(p => p.Projects)
            .Include(p => p.Projects)
            .Where(p => portfolioIds.Contains(p.Id) || p.Programs.Any(pr => programIds.Contains(pr.Id)))
            .ToDictionaryAsync(p => p.Id, p => p, cancellationToken);
    }
}
