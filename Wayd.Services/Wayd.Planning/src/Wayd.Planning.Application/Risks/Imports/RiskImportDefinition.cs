using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Imports;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Planning.Application.Risks.Dtos;

namespace Wayd.Planning.Application.Risks.Imports;

/// <summary>
/// Imports risks against the teams that own them.
/// </summary>
/// <remarks>
/// One pass, because a risk depends on nothing else in the file — no row references another, and every
/// record it points at has to exist already.
/// <para>
/// Atomic, which is what the command this replaces did: it added every risk and saved once, so a file with
/// one bad row wrote nothing. Whether that is the right answer for risks is worth revisiting, but not
/// while converting it.
/// </para>
/// </remarks>
public sealed class RiskImportDefinition(
    IPlanningDbContext planningDbContext,
    IImportPayloadSerializer serializer) : ImportDefinition<ImportRiskDto>(serializer)
{
    public const string ImportKey = "planning.risks";

    // Employees come from the same context: IPlanningDbContext extends IWaydDbContext.
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;

    public override string Key => ImportKey;
    public override string DisplayName => "Risks";
    public override string PermissionAction => ApplicationAction.Import;
    public override string PermissionResource => ApplicationResource.Risks;

    public override ImportAtomicity Atomicity => ImportAtomicity.Atomic;

    /// <summary>
    /// Below the general cap: an atomic import is never split, so the whole file is held at once.
    /// </summary>
    public override int MaxRows => 10_000;

    protected override IReadOnlyList<ImportPass<ImportRiskDto>> Steps =>
    [
        new("CreateRisks", ImportPassScope.Chunked, CreateRisks),
    ];

    /// <summary>
    /// Rejects a row naming a team or a person that does not exist, then creates what is left.
    /// </summary>
    /// <remarks>
    /// The references are checked here rather than left to the database. Reporter and assignee are real
    /// foreign keys, so an unknown one used to surface as a constraint violation when the whole batch
    /// saved — an exception naming a column, taking every good row down with it, instead of a rejection
    /// naming the row to fix.
    /// </remarks>
    private async Task<Result> CreateRisks(ImportPassContext<ImportRiskDto> context, CancellationToken cancellationToken)
    {
        var teamIds = context.Rows.Select(r => r.Data.TeamId).Distinct().ToList();

        var employeeIds = context.Rows
            .SelectMany(r => new[] { (Guid?)r.Data.ReportedById, r.Data.AssigneeId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var knownTeamIds = await _planningDbContext.PlanningTeams
            .AsNoTracking()
            .Where(t => teamIds.Contains(t.Id))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var knownEmployeeIds = await _planningDbContext.Employees
            .AsNoTracking()
            .Where(e => employeeIds.Contains(e.Id))
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        var teams = knownTeamIds.ToHashSet();
        var employees = knownEmployeeIds.ToHashSet();

        foreach (var row in context.Accepted)
        {
            var risk = row.Data;

            if (!teams.Contains(risk.TeamId))
            {
                row.Failed($"No team was found with id '{risk.TeamId}'.");
                continue;
            }

            if (!employees.Contains(risk.ReportedById))
            {
                row.Failed($"No employee was found with id '{risk.ReportedById}' to report this risk.");
                continue;
            }

            if (risk.AssigneeId is { } assigneeId && !employees.Contains(assigneeId))
            {
                row.Failed($"No employee was found with id '{assigneeId}' to assign this risk to.");
                continue;
            }

            var created = Risk.Import(
                risk.Summary,
                risk.Description,
                risk.TeamId,
                risk.ReportedOn,
                risk.ReportedById,
                risk.Status,
                risk.Category,
                risk.Impact,
                risk.Likelihood,
                risk.AssigneeId,
                risk.FollowUpDate,
                risk.Response,
                risk.ClosedDate);

            await _planningDbContext.Risks.AddAsync(created, cancellationToken);
            row.Created(created.Id);
        }

        return Result.Success();
    }
}
