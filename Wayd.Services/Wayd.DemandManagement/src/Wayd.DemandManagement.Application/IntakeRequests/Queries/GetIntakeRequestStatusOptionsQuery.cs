using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests.Dtos;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests.Queries;

/// <summary>
/// The statuses an agent can move an intake request to, from its desk's workflow, with what each move needs;
/// or null when the reader cannot see the request.
/// </summary>
/// <remarks>
/// In the workflow's order, which is how the desk laid its review out. The request's current status is left
/// out, and so is Withdrawn, which only the requester reaches. Anyone who is not an agent on the desk gets
/// none, by the rule the change command applies.
/// </remarks>
/// <param name="Id">The request.</param>
public sealed record GetIntakeRequestStatusOptionsQuery(Guid Id) : IQuery<IReadOnlyList<IntakeRequestStatusOptionDto>?>;

public sealed class GetIntakeRequestStatusOptionsQueryHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    IStatusResolver statusResolver)
    : IQueryHandler<GetIntakeRequestStatusOptionsQuery, IReadOnlyList<IntakeRequestStatusOptionDto>?>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IStatusResolver _statusResolver = statusResolver;

    public async Task<IReadOnlyList<IntakeRequestStatusOptionDto>?> Handle(GetIntakeRequestStatusOptionsQuery request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);

        var target = await _dbContext.IntakeRequests
            .Where(r => r.Id == request.Id)
            .Where(reader.CanSeeSummary(_dbContext.IntakeDesks))
            .Select(r => new { r.DeskId, r.StatusId, r.StatusWorkflowId, HasOutcomes = r.Outcomes.Any() })
            .FirstOrDefaultAsync(cancellationToken);
        if (target is null)
            return null;

        if (!reader.IsAgentOn(target.DeskId))
            return [];

        // An empty list rather than a failure: a desk whose workflow cannot be resolved is an administrator's
        // problem, and the change command reports it with the reason.
        var workflow = await _statusResolver.ForDesk(target.DeskId, cancellationToken);
        if (workflow.IsFailure || workflow.Value.Id != target.StatusWorkflowId)
            return [];

        return
        [
            .. workflow.Value.Statuses
                .Where(s => s.Id != target.StatusId && s.Alias != (int)IntakeRequestStatusAlias.Withdrawn)
                .Select(s => new IntakeRequestStatusOptionDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Category = s.Category,
                    Alias = s.Alias,
                    NeedsReason = IntakeRequest.NeedsReason(s.Category, (IntakeRequestStatusAlias)s.Alias, target.HasOutcomes),
                    NeedsSurvivingRequest = s.Alias == (int)IntakeRequestStatusAlias.Duplicate,
                }),
        ];
    }
}
