using Wayd.Common.Application.Requests.WorkManagement.Dtos;

namespace Wayd.Common.Application.Requests.WorkManagement.Queries;

/// <summary>
/// Gets the Work workflow each work type follows in a work process, which the Azure DevOps sync keeps up to
/// date; null when no work process has the id.
/// </summary>
/// <remarks>
/// For the sync only. These workflows decide nothing: work items resolve their statuses through the status
/// workflow engine. The sync still writes them until #1238 retires them.
/// </remarks>
/// <param name="WorkProcessId">The work process's id.</param>
public sealed record GetExternalWorkProcessWorkflowsQuery(Guid WorkProcessId) : IQuery<IReadOnlyList<ExternalWorkProcessWorkflowDto>?>;

/// <summary>Requires the work process id.</summary>
public sealed class GetExternalWorkProcessWorkflowsQueryValidator : CustomValidator<GetExternalWorkProcessWorkflowsQuery>
{
    public GetExternalWorkProcessWorkflowsQueryValidator()
    {
        RuleFor(q => q.WorkProcessId)
            .NotEmpty();
    }
}
