using System.Linq.Expressions;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.DemandEstimationScales;
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

        if (substance is not null)
        {
            var estimate = await visible
                .Where(r => r.Estimate != null)
                .Select(IntakeRequestProjections.Estimate(_dbContext))
                .FirstOrDefaultAsync(cancellationToken);
            substance = substance with { Estimate = estimate?.Estimate };
        }

        var record = await visible.FirstAsync(cancellationToken);

        // The estimate command needs a linked employee, so the flag does too. Without a scale the command can only
        // clear, so the flag needs an estimate to clear.
        var deskEstimates = await _dbContext.IntakeDesks.AnyAsync(d => d.Id == record.DeskId && d.EstimationScaleVersionId != null, cancellationToken);
        var canChangeEstimate = !_currentPrincipal.IsReadOnly && reader.EmployeeId is not null && reader.IsAgentOn(summary.Desk.Id)
            && record.IsOpen && (deskEstimates || record.Estimate is not null);
        var (pinnedVersionId, estimateValues) = canChangeEstimate
            ? await PinnedVersion(summary.Desk.Id, cancellationToken)
            : (null, []);
        var outcomes = (await IntakeRequestOutcomeReader.Read(_dbContext, visible, cancellationToken))
            .Select(r => r.Outcomes)
            .SingleOrDefault() ?? [];

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
            Outcomes = outcomes,
            DeliverablesMet = IntakeRequestOutcomeReader.DeliverablesMet(outcomes),
            CanChangeOutcomes = !_currentPrincipal.IsReadOnly && reader.EmployeeId is not null && reader.IsAgentOn(summary.Desk.Id)
                && record.StatusCategory == StatusCategory.Active,
            CanChangeEstimate = canChangeEstimate,
            EstimateValues = estimateValues,
            EstimationScaleVersionId = pinnedVersionId,
            CanWithdraw = !_currentPrincipal.IsReadOnly && reader.EmployeeId == record.RequesterId && record.IsOpen,
            DuplicateOf = record.DuplicateOfId is { } survivorId ? await VisibleRequest(survivorId, reader, cancellationToken) : null,
        };
    }

    /// <summary>
    /// The version the desk pins with its estimate values, in order, or neither when it pins none.
    /// </summary>
    private async Task<(Guid? VersionId, IReadOnlyList<string> Values)> PinnedVersion(Guid deskId, CancellationToken cancellationToken)
    {
        var desk = await _dbContext.IntakeDesks.AsNoTracking().FirstAsync(d => d.Id == deskId, cancellationToken);
        var scale = await _dbContext.FindPinnedScale(desk, cancellationToken);
        var version = scale?.Versions.Single(v => v.Id == desk.EstimationScaleVersionId);

        return version is null
            ? (null, [])
            : (version.Id, [.. version.Values.Where(v => v.IsEstimate).Select(v => v.Value)]);
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
