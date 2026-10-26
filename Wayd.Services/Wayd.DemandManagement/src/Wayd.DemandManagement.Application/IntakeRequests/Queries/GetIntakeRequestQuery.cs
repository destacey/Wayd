using System.Linq.Expressions;
using Wayd.Common.Domain.Events;
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

        return new IntakeRequestDetailsDto
        {
            Summary = summary,
            Substance = substance,
            CanEdit = !_currentPrincipal.IsReadOnly && await CanEdit(visible, reader, substance is not null, cancellationToken),
            // The remove command needs a linked employee, so the flag does too.
            CanRemoveParticipants = !_currentPrincipal.IsReadOnly && reader.EmployeeId is not null && reader.IsAgentOn(summary.Desk.Id),
            CanDelete = !_currentPrincipal.IsReadOnly && reader.IsDeskAdminOn(summary.Desk.Id),
            // The reroute command needs a linked employee, so the flag does too.
            CanReroute = !_currentPrincipal.IsReadOnly && reader.EmployeeId is not null && reader.IsAgentOn(summary.Desk.Id)
                && (await visible.FirstAsync(cancellationToken)).IsOpen,
            CanAddParticipants = !_currentPrincipal.IsReadOnly && await CanAddParticipants(visible, reader, cancellationToken),
        };
    }

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
    private async Task<bool> CanEdit(IQueryable<IntakeRequest> visible, IntakeRequestReader reader, bool canSeeSubstance, CancellationToken cancellationToken)
    {
        if (reader.EmployeeId is null || !canSeeSubstance)
            return false;

        var record = await visible.FirstOrDefaultAsync(cancellationToken);

        return record is not null
            && record.CanEdit(new IntakeRequestEditor(reader.EmployeeId, reader.IsAgentOn(record.DeskId), EventActor.Anonymous));
    }
}
