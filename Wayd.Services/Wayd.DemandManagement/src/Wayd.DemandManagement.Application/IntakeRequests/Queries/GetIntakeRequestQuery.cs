using System.Linq.Expressions;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.DemandProjects.Dtos;
using Wayd.DemandManagement.Application.IntakeRequests.Dtos;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests.Queries;

/// <summary>
/// One intake request in the tiers the current user may read, or null when they may not see it at all.
/// </summary>
/// <remarks>
/// A request the reader may not see is reported exactly like one that does not exist, so a private desk's
/// requests cannot be discovered by probing ids or numbers.
/// </remarks>
public sealed record GetIntakeRequestQuery : IQuery<IntakeRequestDetailsDto?>
{
    public GetIntakeRequestQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<IntakeRequest>();
    }

    public Expression<Func<IntakeRequest, bool>> IdOrKeyFilter { get; }
}

public sealed class GetIntakeRequestQueryHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetIntakeRequestQuery, IntakeRequestDetailsDto?>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<IntakeRequestDetailsDto?> Handle(GetIntakeRequestQuery request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);

        var visible = _dbContext.IntakeRequests
            .AsNoTracking()
            .Where(request.IdOrKeyFilter)
            .Where(reader.CanSeeSummary(_dbContext.IntakeDesks));

        var summary = await visible
            .Select(IntakeRequestProjections.Summary(_dbContext))
            .FirstOrDefaultAsync(cancellationToken);
        if (summary is null)
            return null;

        var substance = await visible
            .Where(reader.CanSeeSubstance(_dbContext.IntakeDesks))
            .Select(IntakeRequestProjections.Substance(_dbContext))
            .FirstOrDefaultAsync(cancellationToken);

        var record = await visible.FirstAsync(cancellationToken);

        return new IntakeRequestDetailsDto
        {
            Summary = summary,
            Substance = substance,
            CanEdit = !_currentPrincipal.IsReadOnly && CanEdit(record, reader, substance is not null),
            // The remove command needs a linked employee, so the flag does too.
            CanRemoveParticipants = !_currentPrincipal.IsReadOnly && reader.EmployeeId is not null && reader.IsAgentOn(summary.Desk.Id),
            CanDelete = !_currentPrincipal.IsReadOnly && reader.IsDeskAdminOn(summary.Desk.Id) && !record.HasLeftItsInitialStatus
                && !await _dbContext.IntakeRequests.AnyAsync(r => r.DuplicateOfId == record.Id, cancellationToken),
            // The reroute command needs a linked employee, so the flag does too.
            CanReroute = !_currentPrincipal.IsReadOnly && reader.EmployeeId is not null && reader.IsAgentOn(summary.Desk.Id)
                && record.IsOpen,
            CanAddParticipants = !_currentPrincipal.IsReadOnly && await CanAddParticipants(visible, reader, cancellationToken),
            IsDeskAgent = reader.IsAgentOn(summary.Desk.Id),
            // These commands need a linked employee, so the flags do too.
            CanChangeStatus = !_currentPrincipal.IsReadOnly && reader.EmployeeId is not null && reader.IsAgentOn(summary.Desk.Id),
            CanChangeAssignee = !_currentPrincipal.IsReadOnly && reader.EmployeeId is not null && reader.IsAgentOn(summary.Desk.Id),
            CanChangeTags = !_currentPrincipal.IsReadOnly && reader.EmployeeId is not null && reader.IsAgentOn(summary.Desk.Id),
            Outcomes = await Outcomes(record.Id, cancellationToken),
            CanChangeOutcomes = !_currentPrincipal.IsReadOnly && reader.EmployeeId is not null && reader.IsAgentOn(summary.Desk.Id)
                && record.StatusCategory == StatusCategory.Active,
            CanWithdraw = !_currentPrincipal.IsReadOnly && reader.EmployeeId == record.RequesterId && record.IsOpen,
            DuplicateOf = record.DuplicateOfId is { } survivorId ? await VisibleRequest(survivorId, reader, cancellationToken) : null,
        };
    }

    /// <summary>
    /// The request's outcomes, oldest first, each project outcome with its project when the copy still has it.
    /// </summary>
    /// <remarks>
    /// A work item outcome's <see cref="IntakeRequestOutcomeDto.WorkItem"/> is left null: Demand Management
    /// keeps no copy of work items, so the API layer fills it in from Work.
    /// </remarks>
    private async Task<IReadOnlyList<IntakeRequestOutcomeDto>> Outcomes(Guid requestId, CancellationToken cancellationToken)
    {
        var employees = _dbContext.Employees;

        var outcomes = await _dbContext.IntakeRequests
            .Where(r => r.Id == requestId)
            .SelectMany(r => r.Outcomes)
            .OrderBy(o => o.AddedOn)
            .ThenBy(o => o.TargetId)
            .Select(o => new
            {
                o.Kind,
                o.TargetId,
                o.Note,
                AddedBy = employees.Where(e => e.Id == o.AddedById)
                    .Select(e => new EmployeeNavigationDto { Id = e.Id, Key = e.Key, Name = e.Name.FirstName + " " + e.Name.LastName })
                    .First(),
                o.AddedOn,
            })
            .ToListAsync(cancellationToken);
        if (outcomes.Count == 0)
            return [];

        var targetIds = outcomes.Where(o => o.Kind != IntakeRequestOutcomeKind.WorkItem).Select(o => o.TargetId).ToList();
        var projects = await _dbContext.DemandProjects
            .AsNoTracking()
            .Where(p => targetIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        return
        [
            .. outcomes.Select(o => new IntakeRequestOutcomeDto
            {
                Kind = o.Kind,
                TargetId = o.TargetId,
                Project = projects.TryGetValue(o.TargetId, out var p)
                    ? new DemandProjectNavigationDto { Id = p.Id, Key = p.Key.Value, Name = p.Name }
                    : null,
                Note = o.Note,
                AddedBy = o.AddedBy,
                AddedOn = o.AddedOn,
            }),
        ];
    }

    /// <summary>
    /// The request, named, when the reader may see it; otherwise null, so a request they cannot see is never
    /// named.
    /// </summary>
    private Task<IntakeRequestNavigationDto?> VisibleRequest(Guid id, IntakeRequestReader reader, CancellationToken cancellationToken) =>
        _dbContext.IntakeRequests
            .Where(r => r.Id == id)
            .Where(reader.CanSeeSummary(_dbContext.IntakeDesks))
            .Select(r => new IntakeRequestNavigationDto { Id = r.Id, Key = r.Key, Title = r.Title })
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Whether the reader may add participants, by the rule the add command applies.
    /// </summary>
    private async Task<bool> CanAddParticipants(IQueryable<IntakeRequest> visible, IntakeRequestReader reader, CancellationToken cancellationToken)
    {
        if (reader.EmployeeId is null)
            return false;

        var people = await visible
            .Select(r => new IntakeRequestPeople(r.DeskId, r.RequesterId, r.SubmittedById))
            .FirstAsync(cancellationToken);
        var desk = await _dbContext.IntakeDesks
            .AsNoTracking()
            .Where(d => d.Id == people.DeskId)
            .Select(d => new { d.Visibility, d.RequesterMayAddParticipants })
            .FirstAsync(cancellationToken);

        return reader.CanAddParticipants(people, desk.Visibility, desk.RequesterMayAddParticipants);
    }

    /// <summary>
    /// Whether the reader may edit the request, by the same rules the update command applies: a linked
    /// employee, the substance (the update carries the description), and the request's own edit lock.
    /// </summary>
    private static bool CanEdit(IntakeRequest record, IntakeRequestReader reader, bool canSeeSubstance) =>
        reader.EmployeeId is not null
        && canSeeSubstance
        && record.CanEdit(new IntakeRequestEditor(reader.EmployeeId, reader.IsAgentOn(record.DeskId), EventActor.Anonymous));
}
