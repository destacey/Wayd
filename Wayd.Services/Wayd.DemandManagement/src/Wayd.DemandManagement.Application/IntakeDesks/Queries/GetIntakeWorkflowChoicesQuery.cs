using Wayd.Common.Application.StatusWorkflows;
using Wayd.DemandManagement.Application.IntakeDesks.Dtos;
using Wayd.DemandManagement.Domain;

namespace Wayd.DemandManagement.Application.IntakeDesks.Queries;

/// <summary>
/// The published intake request workflows a desk can follow, by name.
/// </summary>
/// <remarks>
/// Read here rather than from workflow administration, whose endpoints need a permission a Desk Admin usually
/// does not hold. Building and editing workflows stays in Settings.
/// </remarks>
public sealed record GetIntakeWorkflowChoicesQuery : IQuery<IReadOnlyList<IntakeWorkflowChoiceDto>>;

public sealed class GetIntakeWorkflowChoicesQueryHandler(IScopedWorkflowAssignments workflowAssignments)
    : IQueryHandler<GetIntakeWorkflowChoicesQuery, IReadOnlyList<IntakeWorkflowChoiceDto>>
{
    private readonly IScopedWorkflowAssignments _workflowAssignments = workflowAssignments;

    public async Task<IReadOnlyList<IntakeWorkflowChoiceDto>> Handle(GetIntakeWorkflowChoicesQuery request, CancellationToken cancellationToken) =>
        [
            .. (await _workflowAssignments.Published(DemandManagementWorkflowOwners.IntakeRequest.Key, cancellationToken))
                .Select(w => new IntakeWorkflowChoiceDto { Id = w.Id, Key = w.Key, Name = w.Name, Description = w.Description }),
        ];
}
